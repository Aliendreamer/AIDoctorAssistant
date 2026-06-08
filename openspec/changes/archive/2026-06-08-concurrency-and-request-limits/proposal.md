## Why

The server currently has no concurrency tuning, no request timeout, and no body-abort for rejected requests. Two concrete problems:

1. A client sending a large PDF upload to an unauthenticated endpoint (or wrong credentials) gets a 401 immediately from the auth middleware, but the server keeps the TCP connection open and reads the full body before closing it — causing a multi-minute hang in the client.
2. Under sustained concurrent load, the ONNX inference sessions (embedder + reranker) and the thread pool have no concurrency limits configured, and the Kestrel request pipeline has no timeouts. Many simultaneous query requests can saturate the thread pool or starve each other.

## What Changes

**Request lifecycle hardening**
- Kestrel `RequestBodyTimeout`: abort body reads that take longer than 60 s
- Kestrel `MinRequestBodyDataRate`: disconnect clients sending below 100 B/s (catches stalled uploads)
- Kestrel `KeepAliveTimeout`: reduce from default 130 s to 65 s to free idle connections faster
- `app.UseRequestTimeouts()` (ASP.NET Core 8+ `RequestTimeouts` middleware): 30 s default timeout on all requests; 5 min override for the upload endpoint

**Thread pool**
- `ThreadPool.SetMinThreads(workerThreads: 4 * cpuCount, completionPortThreads: 4 * cpuCount)` at startup to eliminate cold-start stalls under burst traffic

**ONNX inference concurrency**
- Add `SemaphoreSlim` to `MultilingualE5Embedder` and `CrossEncoderReranker` to cap parallel ONNX inference at `cpuCount` threads, preventing thread-pool exhaustion under heavy load

**Response compression**
- `AddResponseCompression` + `UseResponseCompression` for Brotli + Gzip on JSON API responses and Blazor payloads

**PostgreSQL connection pool**
- Add `Maximum Pool Size=50` to the Npgsql connection string via appsettings

## Capabilities

### Modified Capabilities
- None — all changes are infrastructure-level; no API contracts change

## Impact

- `MedAssist.Web/Program.cs` — thread pool config, Kestrel limits, middleware order
- `MedAssist.Web/Extensions/ServiceCollectionExtensions.cs` — `AddResponseCompression`, `AddRequestTimeouts`
- `MedAssist.AI/Embedding/MultilingualE5Embedder.cs` — `SemaphoreSlim` for ONNX calls
- `MedAssist.AI/Reranker/CrossEncoderReranker.cs` — `SemaphoreSlim` for ONNX calls
- `config/appsettings.shared.json` — `Maximum Pool Size` in connection string
