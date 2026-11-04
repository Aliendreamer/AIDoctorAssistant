using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>Lists the models installed in Ollama, for the evaluation form and its pre-flight check.</summary>
public sealed class OllamaModelCatalog(IHttpClientFactory httpClientFactory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    /// <summary>Installed model names, or an empty list when Ollama cannot be reached.</summary>
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            var client = httpClientFactory.CreateClient("ollama");
            var tags = await client.GetFromJsonAsync<TagsResponse>("api/tags", timeout.Token);
            return tags?.Models.Select(m => m.Name).Order().ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private sealed record TagsResponse([property: JsonPropertyName("models")] IReadOnlyList<TagsModel> Models);

    private sealed record TagsModel([property: JsonPropertyName("name")] string Name);
}
