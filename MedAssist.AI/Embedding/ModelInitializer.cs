using MedAssist.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Embedding;

public sealed class ModelInitializer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModelInitializer> _logger;

    private const string _hfBase = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main";
    private const string _rerankerHfBase = "https://huggingface.co/cross-encoder/ms-marco-MiniLM-L-6-v2/resolve/main";

    private static readonly (string RemotePath, string LocalFile)[] _modelFiles =
    [
        ($"{_hfBase}/onnx/{OnnxConstants.Files.ModelOnnx}", OnnxConstants.Files.ModelOnnx),
        ($"{_hfBase}/onnx/{OnnxConstants.Files.ModelOnnxData}", OnnxConstants.Files.ModelOnnxData),
        ($"{_hfBase}/{OnnxConstants.Files.TokenizerJson}", OnnxConstants.Files.TokenizerJson),
        ($"{_hfBase}/{OnnxConstants.Files.TokenizerConfigJson}", OnnxConstants.Files.TokenizerConfigJson),
        ($"{_hfBase}/{OnnxConstants.Files.SpecialTokensMapJson}", OnnxConstants.Files.SpecialTokensMapJson),
    ];

    private static readonly (string RemotePath, string LocalFile)[] _rerankerModelFiles =
    [
        ($"{_rerankerHfBase}/onnx/{OnnxConstants.Files.ModelOnnx}", OnnxConstants.Files.ModelOnnx),
        ($"{_rerankerHfBase}/{OnnxConstants.Files.TokenizerJson}", OnnxConstants.Files.TokenizerJson),
        ($"{_rerankerHfBase}/{OnnxConstants.Files.TokenizerConfigJson}", OnnxConstants.Files.TokenizerConfigJson),
        ($"{_rerankerHfBase}/{OnnxConstants.Files.SpecialTokensMapJson}", OnnxConstants.Files.SpecialTokensMapJson),
    ];

    public ModelInitializer(HttpClient httpClient, ILogger<ModelInitializer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task EnsureModelReadyAsync(string modelDirectory, CancellationToken cancellationToken = default)
        => EnsureFilesAsync(modelDirectory, _modelFiles, cancellationToken);

    public Task EnsureRerankerReadyAsync(string modelDirectory, CancellationToken cancellationToken = default)
        => EnsureFilesAsync(modelDirectory, _rerankerModelFiles, cancellationToken);

    private async Task EnsureFilesAsync(
        string modelDirectory,
        (string RemotePath, string LocalFile)[] files,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(modelDirectory);

        foreach (var (remoteUrl, localFile) in files)
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
