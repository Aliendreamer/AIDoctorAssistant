using MedAssist.Shared.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace MedAssist.AI.Embedding;

public sealed class MultilingualE5Embedder : IEmbedder, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private const int _maxTokens = 512;

    public MultilingualE5Embedder(string modelDirectory)
    {
        var modelPath = Path.Combine(modelDirectory, "model.onnx");
        var tokenizerPath = Path.Combine(modelDirectory, "tokenizer.json");

        _session = new InferenceSession(modelPath, new SessionOptions());
        _tokenizer = BertTokenizer.Create(tokenizerPath);
    }

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed("query: " + text));

    public Task<float[]> EmbedPassageAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed("passage: " + text));

    private float[] Embed(string text)
    {
        var encoding = _tokenizer.EncodeToIds(text, _maxTokens, out _, out _);
        var inputIds = encoding.Select(id => (long)id).ToArray();
        var attentionMask = inputIds.Select(_ => 1L).ToArray();
        var tokenTypeIds = inputIds.Select(_ => 0L).ToArray();

        int seqLen = inputIds.Length;
        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, seqLen]);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, seqLen]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
        };

        using var outputs = _session.Run(inputs);
        var lastHiddenState = outputs.First(o => o.Name == "last_hidden_state").AsEnumerable<float>().ToArray();

        // Mean pooling over sequence dimension
        int hiddenSize = lastHiddenState.Length / seqLen;
        var pooled = new float[hiddenSize];
        for (var i = 0; i < seqLen; i++)
        {
            for (var j = 0; j < hiddenSize; j++)
            {
                pooled[j] += lastHiddenState[(i * hiddenSize) + j];
            }
        }

        for (var j = 0; j < hiddenSize; j++)
        {
            pooled[j] /= seqLen;
        }

        return Normalize(pooled);
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm < 1e-8f)
        {
            return vector;
        }

        return vector.Select(v => v / norm).ToArray();
    }

    public void Dispose() => _session.Dispose();
}
