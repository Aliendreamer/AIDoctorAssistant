using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

public sealed class MarkerClient
{
    private readonly HttpClient _httpClient;
    private readonly bool _useLlm;
    private readonly ILogger<MarkerClient> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    public MarkerClient(HttpClient httpClient, bool useLlm, ILogger<MarkerClient> logger)
    {
        _httpClient = httpClient;
        _useLlm = useLlm;
        _logger = logger;
    }

    /// <summary>
    /// Submits a conversion job and returns the job ID immediately.
    /// </summary>
    public async Task<string> StartConversionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var url = _useLlm ? "/convert-by-path?use_llm=true" : "/convert-by-path";
        _logger.LogInformation("Submitting Marker job for '{FilePath}' (use_llm={UseLlm})", filePath, _useLlm);

        using var response = await _httpClient.PostAsJsonAsync(url, new { file_path = filePath }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JobSubmitResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Marker returned an empty response.");

        return result.JobId
            ?? throw new InvalidOperationException("Marker response did not contain a job_id.");
    }

    /// <summary>
    /// Polls /status/{jobId} every 30 s until done or failed. Returns markdown on success.
    /// </summary>
    public async Task<string> PollStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        for (var poll = 1; !cancellationToken.IsCancellationRequested; poll++)
        {
            await Task.Delay(PollInterval, cancellationToken);

            _logger.LogInformation("Marker poll {Poll} for job {JobId}", poll, jobId);

            JobStatusResponse status;
            try
            {
                using var response = await _httpClient.GetAsync($"/status/{jobId}", cancellationToken);
                response.EnsureSuccessStatusCode();
                status = await response.Content.ReadFromJsonAsync<JobStatusResponse>(cancellationToken: cancellationToken)
                    ?? throw new InvalidOperationException("Empty status response.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Marker poll {Poll} failed for job {JobId} — will retry", poll, jobId);
                continue;
            }

            switch (status.State)
            {
                case "done":
                    return status.Markdown
                        ?? throw new InvalidOperationException("Job done but markdown is missing.");

                case "failed":
                    throw new InvalidOperationException($"Marker job {jobId} failed: {status.Error}");

                default:
                    var elapsed = status.ElapsedSeconds is > 0
                        ? $"{status.ElapsedSeconds / 60}m {status.ElapsedSeconds % 60}s"
                        : "unknown";
                    _logger.LogInformation("Marker job {JobId} still running — elapsed {Elapsed} (poll {Poll})", jobId, elapsed, poll);
                    break;
            }
        }

        throw new OperationCanceledException($"Marker polling for job {jobId} was cancelled.");
    }

    public async Task<string> ConvertPdfToMarkdownAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(pdfStream);
        content.Add(fileContent, "file", fileName);

        var url = _useLlm ? "/convert?use_llm=true" : "/convert";
        _logger.LogInformation("Sending '{FileName}' to Marker (use_llm={UseLlm})", fileName, _useLlm);

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MarkerResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Marker returned an empty response.");

        return result.Markdown
            ?? throw new InvalidOperationException("Marker response did not contain markdown content.");
    }

    private sealed class JobSubmitResponse
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; init; }
    }

    private sealed class JobStatusResponse
    {
        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("markdown")]
        public string? Markdown { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("elapsed_seconds")]
        public int? ElapsedSeconds { get; init; }
    }

    private sealed class MarkerResponse
    {
        [JsonPropertyName("markdown")]
        public string? Markdown { get; init; }
    }
}
