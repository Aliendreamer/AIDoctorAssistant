## Capability: kestrel-limits

Kestrel body/connection lifecycle hardening and per-request timeout middleware.

### Changes

**`MedAssist.Web/Program.cs`**

1. At the top of `Program.cs`, before `WebApplication.CreateBuilder`, add thread pool pre-warm:
   ```csharp
   int cpuCount = Environment.ProcessorCount;
   ThreadPool.SetMinThreads(workerThreads: 4 * cpuCount, completionPortThreads: 4 * cpuCount);
   ```

2. Configure Kestrel limits via `builder.WebHost.ConfigureKestrel`:
   ```csharp
   builder.WebHost.ConfigureKestrel(options =>
   {
       options.Limits.RequestBodyTimeout = TimeSpan.FromSeconds(60);
       options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(5));
       options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(65);
   });
   ```

3. Register request timeout middleware in `AddApplicationServices` (or directly in Program.cs):
   ```csharp
   services.AddRequestTimeouts(options =>
   {
       options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(30) };
       options.AddPolicy("upload", new RequestTimeoutPolicy { Timeout = TimeSpan.FromMinutes(5) });
   });
   ```

4. Add `app.UseRequestTimeouts()` in the middleware pipeline — after `UseAuthentication`/`UseAuthorization`, before `MapFastEndpoints`.

**`MedAssist.Web/Endpoints/Books/UploadBookEndpoint.cs`**

Add the timeout attribute to the upload endpoint class:
```csharp
[RequestTimeout("upload")]
```

### Behaviour

- Body reads exceeding 60 s → Kestrel resets the TCP connection (RST), client receives connection closed.
- Clients sending < 100 B/s for more than 5 s → Kestrel drops the connection.
- All requests that exceed 30 s wall-clock → `HttpContext.RequestAborted` is cancelled; FastEndpoints propagates this through `ct` parameters.
- Upload endpoint gets 5 min instead of 30 s.
