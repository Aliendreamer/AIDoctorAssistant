using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedAssist.AI.Ingestion;

public sealed class DoclingClient
{
    private readonly HttpClient _httpClient;

    public DoclingClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> ConvertPdfToMarkdownAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(pdfStream);
        content.Add(fileContent, "files", fileName);

        using var response = await _httpClient.PostAsync("/v1alpha/convert/file", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<DoclingResponse>(responseStream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Docling returned an empty response.");

        return result.Document?.MdContent
            ?? throw new InvalidOperationException("Docling response did not contain markdown content.");
    }

    private sealed class DoclingResponse
    {
        [JsonPropertyName("document")]
        public DoclingDocument? Document { get; init; }
    }

    private sealed class DoclingDocument
    {
        [JsonPropertyName("md_content")]
        public string? MdContent { get; init; }
    }
}
