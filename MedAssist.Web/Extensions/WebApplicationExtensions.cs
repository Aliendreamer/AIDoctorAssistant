using MedAssist.AI.Embedding;

namespace MedAssist.Web.Extensions;

internal static class WebApplicationExtensions
{
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
