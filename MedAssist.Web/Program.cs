using FastEndpoints;
using FastEndpoints.Swagger;
using MedAssist.AI.Extensions;
using MedAssist.Web.Components;
using MedAssist.Web.Extensions;
using MedAssist.Web.Startup;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

int cpuCount = Environment.ProcessorCount;
ThreadPool.SetMinThreads(workerThreads: 4 * cpuCount, completionPortThreads: 4 * cpuCount);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(5));
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(65);
});
builder.Configuration.AddSharedConfiguration();
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddAiServices(builder.Configuration);
builder.Services.AddQueryServices();
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddApiDocs();
builder.Services.AddObservability(builder.Configuration, builder.Environment);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

await app.MigrateDbAsync();
await app.SeedUsersAsync();
await app.EnsureModelsReadyAsync();

app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapHealthChecks("/health");

app.UseSerilogRequestLogging(opts =>
{
    opts.GetLevel = (ctx, _, _) =>
        ctx.Request.Path.StartsWithSegments("/metrics") || ctx.Request.Path.StartsWithSegments("/health")
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
});

app.UseSwaggerGen();
app.MapScalarApiReference(o => o
    .WithTitle("MedAssist API")
    .WithOpenApiRoutePattern("/swagger/v1/swagger.json")
    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

app.UseResponseCompression();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();
app.UseFastEndpoints();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
