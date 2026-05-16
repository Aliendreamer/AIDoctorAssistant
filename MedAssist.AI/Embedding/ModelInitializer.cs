using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Embedding;

public sealed class ModelInitializer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModelInitializer> _logger;

    private const string _hfBase = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main";

    private static readonly (string RemotePath, string LocalFile)[] _modelFiles =
    [
        ($"{_hfBase}/onnx/model.onnx", "model.onnx"),
        ($"{_hfBase}/onnx/model.onnx_data", "model.onnx_data"),
        ($"{_hfBase}/tokenizer.json", "tokenizer.json"),
        ($"{_hfBase}/tokenizer_config.json", "tokenizer_config.json"),
        ($"{_hfBase}/special_tokens_map.json", "special_tokens_map.json"),
    ];

    public ModelInitializer(HttpClient httpClient, ILogger<ModelInitializer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task EnsureModelReadyAsync(string modelDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(modelDirectory);

        foreach (var (remoteUrl, localFile) in _modelFiles)
        {
            var localPath = Path.Combine(modelDirectory, localFile);
            if (File.Exists(localPath))
            {
                continue;
            }

            _logger.LogInformation("Downloading model file {File} from HuggingFace...", localFile);
            await DownloadFileAsync(remoteUrl, localPath, cancellationToken);
            _logger.LogInformation("Downloaded {File}", localFile);
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tmpPath = destinationPath + ".tmp";
        await using (var fileStream = File.Create(tmpPath))
        await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await httpStream.CopyToAsync(fileStream, cancellationToken);
        }

        File.Move(tmpPath, destinationPath, overwrite: true);
    }
}
