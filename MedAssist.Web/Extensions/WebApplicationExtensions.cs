using MedAssist.AI.Embedding;
using MedAssist.Data;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Extensions;

internal static class WebApplicationExtensions
{
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
