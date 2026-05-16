using FastEndpoints;
using FastEndpoints.Swagger;
using MedAssist.AI.Extensions;
using Scalar.AspNetCore;
using MedAssist.Web.Components;
using MedAssist.Web.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .WriteTo.Console(new CompactJsonFormatter())
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddSharedConfiguration();
builder.Host.UseSerilog();

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

app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
