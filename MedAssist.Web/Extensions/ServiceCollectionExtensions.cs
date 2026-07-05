using System.Text;
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
using MedAssist.Data.Services;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using MedAssist.Web.Data;
using MedAssist.Web.Options;
using MedAssist.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qdrant.Client;

namespace MedAssist.Web.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MedAssistDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("Postgres"),
                o => o.SetPostgresVersion(17, 0).MapEnum<BookStatus>("book_status"))
               .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<BookCatalogService>();
        services.AddScoped<IMedicalDictionary, MedicalDictionaryService>();
        services.AddScoped<IBM25VocabStore, BM25VocabService>();

        services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
        services.AddScoped<UserRepository>();

        return services;
    }

    internal static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ModelsOptions>(configuration.GetSection("Models"));
        services.Configure<QdrantOptions>(configuration.GetSection("VectorStore:Qdrant"));
        services.Configure<MinerUOptions>(configuration.GetSection("MinerU"));

        services.AddHttpClient<ModelInitializer>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ModelsOptions>>().Value;
            client.Timeout = TimeSpan.FromMinutes(opts.InitializerTimeoutMinutes);
        });

        services.AddSingleton<IEmbedder>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ModelsOptions>>().Value;
            var modelDir = Path.Combine(opts.Path, "multilingual-e5-large");
            return new MultilingualE5Embedder(modelDir);
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            var uri = new Uri(opts.Endpoint);
            return new QdrantClient(uri.Host, uri.Port);
        });
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        // BM25 vocab snapshot is process-wide and effectively static between ingest runs. The cache
        // loads it once (via a short-lived scope, so no captive DbContext); the vectorizer is a
        // stateless singleton over it. Previously both were Scoped → full-table reload every query (P1-9).
        services.AddSingleton<Bm25VocabCache>();
        services.AddSingleton<ISparseVectorizer>(sp => new SparseVectorizer(sp.GetRequiredService<Bm25VocabCache>()));

        services.AddSingleton<ICrossEncoderReranker>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ModelsOptions>>().Value;
            return new CrossEncoderReranker(opts.RerankerPath);
        });

        // Ollama chat client — a bounded timeout so a hung/slow Ollama can't block a request forever
        // (LLM generations are legitimately long, so the timeout is generous). Endpoint carried on
        // the client's BaseAddress and consumed by KernelFactory via the HttpClient overload.
        services.AddHttpClient("ollama", (sp, client) =>
        {
            var endpoint = configuration["AI:Ollama:Endpoint"]
                ?? throw new InvalidOperationException("AI:Ollama:Endpoint configuration is required.");
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.Configure<RagOptions>(configuration.GetSection("Rag"));
        services.AddScoped(sp => AI.Kernel.KernelFactory.Build(
            configuration,
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama"),
            sp.GetRequiredService<IMedicalDictionary>(),
            sp.GetRequiredService<IVectorStore>(),
            sp.GetRequiredService<IEmbedder>(),
            sp.GetRequiredService<ISparseVectorizer>(),
            sp.GetRequiredService<ICrossEncoderReranker>(),
            sp.GetRequiredService<IOptions<RagOptions>>().Value,
            sp.GetRequiredService<ILoggerFactory>()));

        // Shared MinerU OCR service (migrate-marker-to-mineru): one synchronous /file_parse call per
        // PDF, so the client needs a long timeout (the whole OCR runs inside the request).
        services.AddHttpClient("mineru", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MinerUOptions>>().Value;
            client.BaseAddress = new Uri(opts.ServiceUrl);
            client.Timeout = TimeSpan.FromMinutes(opts.ConversionTimeoutMinutes);
        });
        services.AddTransient<MinerUClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MinerUOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("mineru");
            var logger = sp.GetRequiredService<ILogger<MinerUClient>>();
            return new MinerUClient(httpClient, opts.Backend, opts.Method, logger);
        });
        services.AddTransient<MarkdownChunker>();
        services.AddTransient<ChunkEnricher>();
        services.AddTransient<VocabularyBuilder>();
        services.AddTransient<BookRepository>();
        services.AddTransient<CheckpointRepository>();
        // Interface views of the ingestion repos so the AI layer (BookIndexer) depends on Shared
        // abstractions, not the EF repositories directly (audit P2-13).
        services.AddTransient<IBookRepository>(sp => sp.GetRequiredService<BookRepository>());
        services.AddTransient<ICheckpointRepository>(sp => sp.GetRequiredService<CheckpointRepository>());
        services.AddTransient<ChatHistoryRepository>();
        services.AddTransient<ExtractionStatusRepository>();
        services.AddTransient<BookIndexer>();
        services.AddSingleton<ExtractionTracker>();

        return services;
    }

    internal static IServiceCollection AddQueryServices(this IServiceCollection services)
    {
        // Host-managed ingestion: endpoints enqueue jobs; a single BackgroundService drains them
        // off the request thread and stops cleanly on shutdown (audit P1-8).
        services.AddSingleton<IngestionQueue>();
        services.AddHostedService<IngestionWorker>();

        // AddHttpClient<T> already registers T (as a typed client). The earlier AddScoped<T> was dead
        // and only muddied the lifetime, so it's removed (audit P2-14).
        services.AddHttpClient<QueryService>()
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(300);
            });

        // Admin operations run in-process (audit P1-12): the Blazor admin pages and the REST endpoints
        // share these application services directly — no loopback API hop through AdminApiClient.
        services.AddScoped<BookApplicationService>();
        services.AddScoped<UserApplicationService>();
        services.AddScoped<AdminBookService>();
        services.AddScoped<AdminUserService>();

        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 764 * 1024 * 1024;
            options.ValueLengthLimit = int.MaxValue;
        });

        services.AddRequestTimeouts(options =>
        {
            // Default covers all endpoints. It's generous (3 min) because a RAG query runs the
            // reranker + one or two local-Ollama LLM generations over the retrieved book context,
            // which legitimately exceeds a tight timeout. Per-endpoint [RequestTimeout(...)] named
            // policies are NOT reliably applied here — FastEndpoints resolves its route inside
            // UseFastEndpoints(), after UseRequestTimeouts() runs, so the middleware only ever sees
            // the default policy. Keeping the ceiling on the default keeps it simple and correct.
            options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromMinutes(3)
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
        services.Configure<JwtOptions>(configuration.GetSection("Auth:Jwt"));

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
            options.SlidingExpiration = true;
        })
        .AddJwtBearer();

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((cookie, jwt) =>
            {
                cookie.ExpireTimeSpan = TimeSpan.FromMinutes(jwt.Value.ExpiryMinutes);
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var opts = jwt.Value;
                if (string.IsNullOrEmpty(opts.SecretKey))
                {
                    throw new InvalidOperationException("Auth:Jwt:SecretKey is not configured");
                }

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opts.Issuer,
                    ValidAudience = opts.Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // Pin the algorithm so a token can't be validated under an unexpected alg (P2-11).
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SecretKey))
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
        services.Configure<OtelOptions>(configuration.GetSection("OpenTelemetry"));

        services.AddHealthChecks();

        var otelOpts = configuration.GetSection("OpenTelemetry").Get<OtelOptions>() ?? new OtelOptions();

        var resource = ResourceBuilder.CreateDefault().AddService(
            serviceName: otelOpts.ServiceName,
            serviceVersion: typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0");

        var otlpEndpoint = new Uri(otelOpts.Endpoint);

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MedAssist.Web")
                .AddMeter("MedAssist.AI")
                .AddPrometheusExporter()
                .AddOtlpExporter(o => o.Endpoint = otlpEndpoint))
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
                    .AddOtlpExporter(o => o.Endpoint = otlpEndpoint);
            });

        return services;
    }
}
