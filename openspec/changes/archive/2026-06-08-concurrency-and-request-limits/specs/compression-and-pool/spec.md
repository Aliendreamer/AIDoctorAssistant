## Capability: compression-and-pool

Response compression (Brotli + Gzip) and Npgsql connection pool ceiling.

### Changes

**`MedAssist.Web/Extensions/ServiceCollectionExtensions.cs`**

In the `AddApplicationServices` extension method, add:
```csharp
services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "text/html"]);
});
```

Required usings:
```csharp
using Microsoft.AspNetCore.ResponseCompression;
```

**`MedAssist.Web/Extensions/WebApplicationExtensions.cs`** (or `Program.cs` if extension doesn't exist)

Add `app.UseResponseCompression()` early in the middleware pipeline — before `UseStaticFiles` and before `MapFastEndpoints`.

**`config/appsettings.shared.json`**

Append `Maximum Pool Size=50;` to the Npgsql connection string value. Example:
```json
"ConnectionString": "Host=localhost;Database=medassist;Username=medassist;Password=medassist;Maximum Pool Size=50"
```

### Behaviour

- JSON API responses and Blazor HTML payloads are Brotli-compressed when the client sends `Accept-Encoding: br`; Gzip is the fallback.
- Npgsql will open at most 50 connections to PostgreSQL per process.
