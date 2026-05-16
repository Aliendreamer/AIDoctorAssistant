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
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(pdfStream);
        content.Add(fileContent, "files", fileName);

        using var submitResponse = await _httpClient.PostAsync(
            "/v1/convert/file/async", content, cancellationToken);
        submitResponse.EnsureSuccessStatusCode();

        var task = await submitResponse.Content.ReadFromJsonAsync<DoclingTaskStatus>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Docling returned an empty task response.");

        _logger.LogInformation("Docling task {TaskId} queued for '{FileName}'", task.TaskId, fileName);

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            using var pollResponse = await _httpClient.GetAsync(
                $"/v1/status/poll/{task.TaskId}", cancellationToken);
            pollResponse.EnsureSuccessStatusCode();

            var status = await pollResponse.Content.ReadFromJsonAsync<DoclingTaskStatus>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Docling returned an empty poll response.");

            _logger.LogInformation("Docling task {TaskId} status: {Status}", task.TaskId, status.Status);

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
            $"/v1/result/{task.TaskId}", cancellationToken);
        resultResponse.EnsureSuccessStatusCode();

        var result = await resultResponse.Content.ReadFromJsonAsync<DoclingConvertResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Docling returned an empty result response.");

        return result.Document?.MdContent
            ?? throw new InvalidOperationException("Docling response did not contain markdown content.");
    }

    private sealed class DoclingTaskStatus
    {
        [JsonPropertyName("task_id")]
        public string TaskId { get; init; } = string.Empty;

        [JsonPropertyName("task_status")]
        public string Status { get; init; } = string.Empty;
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
