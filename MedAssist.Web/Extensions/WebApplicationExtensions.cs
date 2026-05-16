using MedAssist.AI.Embedding;

namespace MedAssist.Web.Extensions;

internal static class WebApplicationExtensions
{
    internal static async Task EnsureModelsReadyAsync(this WebApplication app)
    {
        var modelsPath = app.Configuration["Models:Path"] ?? "models";
        var modelDir = Path.Combine(modelsPath, "multilingual-e5-large");
        await app.Services.GetRequiredService<ModelInitializer>().EnsureModelReadyAsync(modelDir);
    }
}
