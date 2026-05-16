using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MedAssist.AI.Plugins;

public sealed class WebSearchPlugin
{
    private readonly HttpClient _httpClient;
    private readonly string _searchEndpoint;
    private readonly string _siteFilter;

    public WebSearchPlugin(HttpClient httpClient, string searchEndpoint, IReadOnlyList<string> allowedDomains)
    {
        _httpClient = httpClient;
        _searchEndpoint = searchEndpoint.TrimEnd('/');
        _siteFilter = allowedDomains.Count > 0
            ? " " + string.Join(" OR ", allowedDomains.Select(d => $"site:{d}"))
            : string.Empty;
    }

    [KernelFunction, System.ComponentModel.Description("Search trusted medical websites when book sources are insufficient.")]
    public async Task<IReadOnlyList<SourceCitation>> SearchAsync(
        [System.ComponentModel.Description("Medical query to search")] string query,
        [System.ComponentModel.Description("Language preference: en, bg, both")] string language = "en",
        CancellationToken cancellationToken = default)
    {
        var fullQuery = query + _siteFilter;
        var url = $"{_searchEndpoint}/search?q={Uri.EscapeDataString(fullQuery)}&format=json&categories=general&language={MapLanguage(language)}";

        var response = await _httpClient.GetFromJsonAsync<SearXNGResponse>(url, cancellationToken);
        if (response?.Results is not { Count: > 0 })
        {
            return [];
        }

        return response.Results
            .Take(5)
            .Select(r => new SourceCitation
            {
                SourceType = SourceType.Web,
                SourceName = r.Engine ?? "Web",
                ArticleTitle = r.Title ?? r.Url ?? "Untitled",
                Url = r.Url ?? string.Empty
            })
            .ToList();
    }

    private static string MapLanguage(string language) => language.ToLowerInvariant() switch
    {
        "bg" => "bg",
        "en" => "en",
        _ => "all"
    };

    private sealed class SearXNGResponse
    {
        [JsonPropertyName("results")]
        public List<SearXNGResult>? Results { get; init; }
    }

    private sealed class SearXNGResult
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("engine")]
        public string? Engine { get; init; }
    }
}
