using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

public sealed class DoclingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DoclingClient> _logger;

    public DoclingClient(HttpClient httpClient, ILogger<DoclingClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ConvertPdfToMarkdownAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms, cancellationToken);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var submitPayload = new DoclingSubmitRequest(
            [new DoclingFileSource(base64, fileName)]);

        using var submitResponse = await _httpClient.PostAsJsonAsync(
            "/v1alpha/convert/source/async", submitPayload, cancellationToken);
        submitResponse.EnsureSuccessStatusCode();

        var task = await submitResponse.Content.ReadFromJsonAsync<DoclingTaskStatus>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Docling returned an empty task response.");

        _logger.LogInformation("Docling task {TaskId} queued for '{FileName}', polling for completion", task.TaskId, fileName);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var pollResponse = await _httpClient.GetAsync(
                $"/v1alpha/status/poll/{task.TaskId}?wait=30", cancellationToken);
            pollResponse.EnsureSuccessStatusCode();

            var status = await pollResponse.Content.ReadFromJsonAsync<DoclingTaskStatus>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Docling returned an empty poll response.");

            _logger.LogInformation("Docling task {TaskId} status: {Status} (queue position: {Position})",
                status.TaskId, status.Status, status.Position);

            if (status.Status == "success")
            {
                break;
            }

            if (status.Status == "failure")
            {
                throw new InvalidOperationException($"Docling conversion failed for '{fileName}'.");
            }
        }

        using var resultResponse = await _httpClient.GetAsync(
            $"/v1alpha/result/{task.TaskId}", cancellationToken);
        resultResponse.EnsureSuccessStatusCode();

        var result = await resultResponse.Content.ReadFromJsonAsync<DoclingConvertResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Docling returned an empty result response.");

        return result.Document?.MdContent
            ?? throw new InvalidOperationException("Docling response did not contain markdown content.");
    }

    private sealed record DoclingSubmitRequest(
        [property: JsonPropertyName("file_sources")] IReadOnlyList<DoclingFileSource> FileSources);

    private sealed record DoclingFileSource(
        [property: JsonPropertyName("base64_string")] string Base64String,
        [property: JsonPropertyName("filename")] string Filename);

    private sealed class DoclingTaskStatus
    {
        [JsonPropertyName("task_id")]
        public string TaskId { get; init; } = string.Empty;

        [JsonPropertyName("task_status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("task_position")]
        public int? Position { get; init; }
    }

    private sealed class DoclingConvertResponse
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
