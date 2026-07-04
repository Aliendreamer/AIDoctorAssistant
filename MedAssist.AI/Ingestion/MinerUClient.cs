using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

/// <summary>
/// Client for the shared MinerU OCR service. Converts a PDF to markdown with a single synchronous
/// <c>POST {ServiceUrl}/file_parse</c> (multipart: <c>files</c>, <c>backend</c>, <c>parse_method</c>,
/// <c>return_md=true</c>) and returns the markdown read from <c>results.&lt;firstKey&gt;.md</c>.
/// Replaces the removed self-hosted Marker submit/poll client.
/// </summary>
public sealed class MinerUClient
{
    private readonly HttpClient _httpClient;
    private readonly string _backend;
    private readonly string _parseMethod;
    private readonly ILogger<MinerUClient> _logger;

    public MinerUClient(HttpClient httpClient, string backend, string parseMethod, ILogger<MinerUClient> logger)
    {
        _httpClient = httpClient;
        _backend = backend;
        _parseMethod = parseMethod;
        _logger = logger;
    }

    public async Task<string> ConvertToMarkdownAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("MinerU conversion started for {FileName} (backend={Backend}, method={Method})",
            fileName, _backend, _parseMethod);

        using var form = new MultipartFormDataContent();
        await using var pdfStream = File.OpenRead(filePath);
        var pdfContent = new StreamContent(pdfStream);
        pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdfContent, "files", fileName);
        form.Add(new StringContent(_backend), "backend");
        form.Add(new StringContent(_parseMethod), "parse_method");
        form.Add(new StringContent("true"), "return_md");
        form.Add(new StringContent("false"), "return_content_list");

        using var response = await _httpClient.PostAsync("/file_parse", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        // MinerU's /file_parse returns { "results": { "<file>": { "md": "..." } } }.
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!payload.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"MinerU response for '{fileName}' has no 'results' object.");
        }

        var firstResult = results.EnumerateObject().FirstOrDefault();
        if (firstResult.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"MinerU response for '{fileName}' contains an empty 'results' object.");
        }

        if (!firstResult.Value.TryGetProperty("md", out var mdEl) || mdEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"MinerU response for '{fileName}' is missing markdown ('md').");
        }

        var markdown = mdEl.GetString() ?? string.Empty;
        _logger.LogInformation("MinerU converted {FileName}: {Chars} markdown chars", fileName, markdown.Length);
        return markdown;
    }
}
