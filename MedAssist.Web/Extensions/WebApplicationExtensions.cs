using MedAssist.AI.Embedding;
using MedAssist.Data;
using MedAssist.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Extensions;

internal static class WebApplicationExtensions
{
    /// <summary>
    /// Fails fast at startup if the JWT signing key is unusable — in Production, the built-in
    /// placeholder or a too-short key is rejected so it can't be shipped silently (audit P0-2).
    /// No-op outside Production.
    /// </summary>
    internal static void ValidateJwtSigningKey(this WebApplication app)
    {
        var reason = JwtKeyPolicy.Validate(app.Configuration["Auth:Jwt:SecretKey"], app.Environment.IsProduction());
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
        }
    }

    internal static async Task MigrateDbAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MedAssistDbContext>();
        await db.Database.MigrateAsync();
    }

    internal static async Task EnsureModelsReadyAsync(this WebApplication app)
    {
        var initializer = app.Services.GetRequiredService<ModelInitializer>();

        var modelsPath = app.Configuration["Models:Path"] ?? "models";
        var embedderDir = Path.Combine(modelsPath, "multilingual-e5-large");
        await initializer.EnsureModelReadyAsync(embedderDir);

        var rerankerDir = app.Configuration["Models:RerankerPath"] ?? "models/ms-marco-MiniLM-L-6-v2";
        await initializer.EnsureRerankerReadyAsync(rerankerDir);
    }
}
