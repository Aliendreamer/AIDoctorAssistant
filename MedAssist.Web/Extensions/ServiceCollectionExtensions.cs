using FastEndpoints;
using FastEndpoints.Swagger;
using MedAssist.AI.Dictionary;
using MedAssist.AI.Embedding;
using MedAssist.AI.Ingestion;
using MedAssist.AI.Reranker;
using MedAssist.AI.VectorStore;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using MedAssist.Web.Data;
using MedAssist.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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

        services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
        services.AddTransient<UserRepository>();

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

        var ragOptions = new RagOptions
        {
            ConfidenceThreshold = configuration.GetValue<float>("Rag:ConfidenceThreshold", 0.0f),
            MaxIterations = Math.Min(configuration.GetValue<int>("Rag:MaxIterations", 2), 5)
        };

        services.AddSingleton(sp => AI.Kernel.KernelFactory.Build(
            configuration,
            sp.GetRequiredService<IMedicalDictionary>(),
            sp.GetRequiredService<IVectorStore>(),
            sp.GetRequiredService<IEmbedder>(),
            sp.GetRequiredService<ISparseVectorizer>(),
            sp.GetRequiredService<ICrossEncoderReranker>(),
            ragOptions));

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

        services.AddScoped<AdminApiClient>();
        services.AddHttpClient<AdminApiClient>();

        services.AddScoped<AdminBookService>();
        services.AddScoped<AdminUserService>();

        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 300 * 1024 * 1024; // 300 MB
            options.ValueLengthLimit = int.MaxValue;
        });

        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            options.AddPolicy("upload", new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromMinutes(5)
            });
        });

        services.AddResponseCompression(opts =>
        {
            opts.EnableForHttps = true;
            opts.Providers.Add<BrotliCompressionProvider>();
            opts.Providers.Add<GzipCompressionProvider>();
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/json", "text/html"]);
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
        var expiryMinutes = configuration.GetValue<int>("Auth:Jwt:ExpiryMinutes", 480);

        // Policy scheme: API paths use JWT bearer; all other paths (Blazor) use cookies.
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Smart";
            options.DefaultChallengeScheme = "Smart";
        })
        .AddPolicyScheme("Smart", displayName: null, options =>
        {
            options.ForwardDefaultSelector = ctx =>
                ctx.Request.Path.StartsWithSegments("/api")
                    ? JwtBearerDefaults.AuthenticationScheme
                    : CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(expiryMinutes);
            options.SlidingExpiration = true;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };
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
