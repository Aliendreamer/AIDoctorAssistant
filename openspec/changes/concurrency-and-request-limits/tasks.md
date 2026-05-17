## Tasks

### 1. Thread pool pre-warm (Program.cs)
- [x] Add `int cpuCount = Environment.ProcessorCount;` and `ThreadPool.SetMinThreads(4 * cpuCount, 4 * cpuCount)` before `WebApplication.CreateBuilder`

### 2. Kestrel limits (Program.cs)
- [x] Add `builder.WebHost.ConfigureKestrel(...)` with `MinRequestBodyDataRate = 100 B/s / 5s grace`, `KeepAliveTimeout = 65s` (RequestBodyTimeout does not exist on KestrelServerLimits — removed)

### 3. Request timeout middleware (ServiceCollectionExtensions.cs)
- [x] Add `services.AddRequestTimeouts(...)` with 30 s default policy and named `"upload"` policy (5 min)

### 4. Wire UseRequestTimeouts (Program.cs / WebApplicationExtensions.cs)
- [x] Add `app.UseRequestTimeouts()` in middleware pipeline after auth, before endpoints

### 5. Annotate upload endpoint
- [x] Add `[RequestTimeout("upload")]` attribute to `UploadBookEndpoint`

### 6. ONNX semaphore — MultilingualE5Embedder
- [x] Add `SemaphoreSlim _inferenceGate = new(Environment.ProcessorCount, Environment.ProcessorCount)`
- [x] Wrap `session.Run(...)` with `await _inferenceGate.WaitAsync(ct)` / `finally Release()`
- [x] Converted `EmbedQueryAsync` and `EmbedPassageAsync` to async

### 7. ONNX semaphore — CrossEncoderReranker
- [x] Add `SemaphoreSlim _inferenceGate = new(Environment.ProcessorCount, Environment.ProcessorCount)`
- [x] Wrap `session.Run(...)` with `await _inferenceGate.WaitAsync(ct)` / `finally Release()`
- [x] Replaced explicit `MaxDegreeOfParallelism` with semaphore gating in `Parallel.ForEachAsync`

### 8. Response compression (ServiceCollectionExtensions.cs)
- [x] Add `services.AddResponseCompression(...)` with Brotli + Gzip providers, enable for HTTPS, add `application/json` and `text/html` MIME types

### 9. Wire UseResponseCompression (Program.cs / WebApplicationExtensions.cs)
- [x] Add `app.UseResponseCompression()` before auth and endpoint mapping

### 10. Npgsql pool ceiling (appsettings.shared.json)
- [x] Append `Maximum Pool Size=50` to the connection string

### 11. Verify build
- [x] `dotnet build MedAssist.slnx` — 0 errors, 0 warnings
