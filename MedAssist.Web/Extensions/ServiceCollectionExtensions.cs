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
        services.Configure<DoclingOptions>(configuration.GetSection("Docling"));

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

        services.AddScoped<ISparseVectorizer, SparseVectorizer>();

        services.AddSingleton<ICrossEncoderReranker>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ModelsOptions>>().Value;
            return new CrossEncoderReranker(opts.RerankerPath);
        });

        services.Configure<RagOptions>(configuration.GetSection("Rag"));
        services.AddScoped(sp => AI.Kernel.KernelFactory.Build(
            configuration,
            sp.GetRequiredService<IMedicalDictionary>(),
            sp.GetRequiredService<IVectorStore>(),
            sp.GetRequiredService<IEmbedder>(),
            sp.GetRequiredService<ISparseVectorizer>(),
            sp.GetRequiredService<ICrossEncoderReranker>(),
            sp.GetRequiredService<IOptions<RagOptions>>().Value));

        services.AddHttpClient<DoclingClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<DoclingOptions>>().Value;
            client.BaseAddress = new Uri(opts.Endpoint);
            client.Timeout = TimeSpan.FromMinutes(opts.TimeoutMinutes);
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
            options.MultipartBodyLengthLimit = 764 * 1024 * 1024;
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
