## Context

MedAssist.Web hosts both a FastEndpoints REST API and Blazor Server in the same ASP.NET Core 10 process on Kestrel. The server currently has no request timeouts, no body-rate enforcement, no thread-pool pre-warming, no ONNX concurrency cap, and no response compression.

Two acute problems:
1. A client that sends an unauthorized PDF upload receives a 401 immediately but then hangs for ~2 minutes while the server drains the body before closing the connection.
2. Concurrent RAG queries drive CPU-bound ONNX inference on the thread pool with no cap, which can saturate the pool under burst load.

## Goals / Non-Goals

**Goals:**
- Abort unauthorized large-body uploads within 60 s (Kestrel body timeout)
- Disconnect clients stalling below 100 B/s (Kestrel min data rate)
- Add a per-request wall-clock timeout (30 s default, 5 min for the upload endpoint)
- Pre-warm the thread pool to eliminate cold-start stalls under burst traffic
- Cap parallel ONNX inference to `Environment.ProcessorCount` to prevent thread-pool exhaustion
- Compress JSON + Blazor payloads with Brotli/Gzip
- Increase Npgsql pool ceiling to 50 for sustained concurrent load

**Non-Goals:**
- Rate-limiting per IP or per user (a separate future concern)
- HTTP/2 or HTTP/3 migration
- Circuit breaking for Ollama / Qdrant / Docling calls

## Decisions

**D1 — Kestrel body limits via `ConfigureKestrel`**
Set `RequestBodyTimeout = 60 s`, `MinRequestBodyDataRate = 100 B/s over 5 s`, and `KeepAliveTimeout = 65 s` in `Program.cs` via `builder.WebHost.ConfigureKestrel(...)`. These apply globally and cause Kestrel to forcibly reset the connection, which unblocks the client immediately rather than waiting for the application layer to drain.

**D2 — `AddRequestTimeouts` / `UseRequestTimeouts` middleware (ASP.NET Core 8+)**
Register a 30 s default policy and a named `"upload"` policy (5 min) via `services.AddRequestTimeouts(...)`. Apply `[RequestTimeout("upload")]` to `UploadBookEndpoint`. This is distinct from Kestrel body limits: it cancels the `HttpContext.RequestAborted` token so the application layer stops processing even if the body was already read.

**D3 — Thread pool pre-warming**
`ThreadPool.SetMinThreads(workerThreads: 4 * cpuCount, completionPortThreads: 4 * cpuCount)` at the very top of `Program.cs`, before `WebApplication.CreateBuilder`. Keeps the pool from doing one-thread-at-a-time hill-climbing during a cold burst. Has no effect once the pool has grown to size naturally.

**D4 — `SemaphoreSlim` in ONNX services**
Both `MultilingualE5Embedder` and `CrossEncoderReranker` are singletons. Each gets a `SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount)` field. Every call to the public inference method acquires the semaphore before calling `session.Run(...)` and releases it in a `finally` block. `InferenceSession.Run` is thread-safe by design; the semaphore prevents spawning more threads than CPU cores can usefully run.

**D5 — Response compression**
`services.AddResponseCompression(opts => { opts.EnableForHttps = true; opts.Providers.Add<BrotliCompressionProvider>(); opts.Providers.Add<GzipCompressionProvider>(); })` and `app.UseResponseCompression()` placed before `UseStaticFiles`. Applied to `application/json` and Blazor `text/html` MIME types. Reduces payload sizes 60–80% for JSON API responses.

**D6 — Npgsql connection pool ceiling**
Append `Maximum Pool Size=50` to the connection string in `config/appsettings.shared.json`. Default Npgsql pool ceiling is 100; setting 50 is a conservative limit that prevents runaway connection creation under concurrent load while leaving headroom for postgres's default `max_connections=100`.

## Risks / Trade-offs

- `RequestBodyTimeout` of 60 s may interrupt legitimate slow-network clients uploading large PDFs. Mitigated by the 5-min `RequestTimeout` on the upload endpoint — that policy timeout fires first for authenticated uploads; the 60 s body timeout only bites if auth is rejected early and the client keeps streaming.
- ONNX semaphore set to `ProcessorCount` may underuse hyperthreads on Intel CPUs. Chosen conservatively; can be tuned via config later.
- Response compression for `text/html` (Blazor) requires `EnableForHttps = true` — acceptable here because the app runs behind a Docker reverse-proxy in a trusted internal network.
