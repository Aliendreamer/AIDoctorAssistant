using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using MedAssist.AI.Dictionary;
using MedAssist.AI.Embedding;
using MedAssist.AI.Ingestion;
using MedAssist.AI.Reranker;
using MedAssist.AI.VectorStore;
using MedAssist.Data;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Interfaces;
using MedAssist.Web.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qdrant.Client;

namespace MedAssist.Web.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is not configured");

        services.AddDbContextFactory<MedAssistDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton<BookCatalogService>();
        services.AddSingleton<IMedicalDictionary, MedicalDictionaryService>();
        services.AddSingleton<IBM25VocabStore, BM25VocabService>();

        return services;
    }

    internal static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var modelsPath = configuration["Models:Path"] ?? "models";
        var modelDir = Path.Combine(modelsPath, "multilingual-e5-large");

        services.AddHttpClient<ModelInitializer>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(15);
        });
        services.AddSingleton<IEmbedder>(_ => new MultilingualE5Embedder(modelDir));

        var qdrantEndpoint = configuration["VectorStore:Qdrant:Endpoint"] ?? "http://localhost:6333";
        var qdrantUri = new Uri(qdrantEndpoint);
        services.AddSingleton(_ => new QdrantClient(qdrantUri.Host, qdrantUri.Port));
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        services.AddSingleton<ISparseVectorizer, SparseVectorizer>();

        var rerankerPath = configuration["Models:RerankerPath"] ?? "models/ms-marco-MiniLM-L-6-v2";
        services.AddSingleton<ICrossEncoderReranker>(_ => new CrossEncoderReranker(rerankerPath));

        services.AddSingleton(sp => AI.Kernel.KernelFactory.Build(
            configuration,
            sp.GetRequiredService<IMedicalDictionary>(),
            sp.GetRequiredService<IVectorStore>(),
            sp.GetRequiredService<IEmbedder>(),
            sp.GetRequiredService<ISparseVectorizer>(),
            sp.GetRequiredService<ICrossEncoderReranker>()));

        // Indexing pipeline
        var doclingEndpoint = configuration["Docling:Endpoint"] ?? "http://docling:5001";
        services.AddHttpClient<DoclingClient>(client =>
        {
            client.BaseAddress = new Uri(doclingEndpoint);
            client.Timeout = TimeSpan.FromMinutes(30);
        });
        services.AddTransient<MarkdownChunker>();
        services.AddTransient<ChunkEnricher>();
        services.AddTransient<VocabularyBuilder>();
        services.AddTransient<BookRepository>();
        services.AddTransient<CheckpointRepository>();
        services.AddTransient<BookIndexer>();

        return services;
    }

    internal static IServiceCollection AddQueryServices(this IServiceCollection services)
    {
        services.AddScoped<QueryService>();
        services.AddHttpClient<QueryService>()
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(300);
            });
        services.AddFastEndpoints();
        return services;
    }

    internal static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var secretKey = configuration["Auth:Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Auth:Jwt:SecretKey is not configured");
        var issuer = configuration["Auth:Jwt:Issuer"] ?? "medassist";
        var audience = configuration["Auth:Jwt:Audience"] ?? "medassist-api";

        services.AddAuthenticationJwtBearer(
            signingOptions: s => { s.SigningKey = secretKey; },
            bearerOptions: o =>
            {
                o.TokenValidationParameters.ValidIssuer = issuer;
                o.TokenValidationParameters.ValidAudience = audience;
                o.TokenValidationParameters.ValidateIssuer = true;
                o.TokenValidationParameters.ValidateAudience = true;
            });

        services.AddAuthorization();
        return services;
    }

    internal static IServiceCollection AddApiDocs(this IServiceCollection services)
    {
        services.SwaggerDocument(o =>
        {
            o.DocumentSettings = s =>
            {
                s.Title = "MedAssist API";
                s.Description = "Bilingual EN/BG RAG medical assistant — hybrid dense+sparse search";
                s.Version = "v1";
            };
        });

        return services;
    }

    internal static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHealthChecks();

        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "medassist-web";
        var otlpEndpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

        var resource = ResourceBuilder.CreateDefault().AddService(
            serviceName: serviceName,
            serviceVersion: typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0");

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MedAssist.Web")
                .AddPrometheusExporter()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resource)
                    .AddSource("MedAssist.Web")
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/metrics") &&
                            !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
