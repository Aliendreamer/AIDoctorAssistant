using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace MedAssist.AI.Reranker;

public sealed class CrossEncoderReranker : ICrossEncoderReranker, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private const int _maxTokens = 512;

    public CrossEncoderReranker(string modelDirectory)
    {
        var modelPath = Path.Combine(modelDirectory, "model.onnx");
        var tokenizerPath = Path.Combine(modelDirectory, "tokenizer.json");

        _session = new InferenceSession(modelPath, new SessionOptions());
        _tokenizer = BertTokenizer.Create(tokenizerPath);
    }

    public async Task<IReadOnlyList<MedicalChunk>> RerankAsync(
        string query,
        IReadOnlyList<MedicalChunk> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var scores = new float[candidates.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, candidates.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                CancellationToken = cancellationToken
            },
            (i, _) =>
            {
                scores[i] = Score(query, candidates[i].Text);
                return ValueTask.CompletedTask;
            });

        return candidates
            .Select((chunk, i) => (chunk, scores[i]))
            .OrderByDescending(x => x.Item2)
            .Select(x => x.chunk)
            .ToList();
    }

    private float Score(string query, string passage)
    {
        // Encode as [CLS] query [SEP] passage [SEP], each part from BertTokenizer
        var queryIds = _tokenizer.EncodeToIds(query, _maxTokens, out _, out _);

        // passageBudget: passage encoding can use at most (_maxTokens - queryIds.Count + 1) tokens
        // because we skip the leading [CLS] from the passage encoding before combining
        var passageBudget = Math.Max(3, _maxTokens - queryIds.Count + 1);
        var passageIds = _tokenizer.EncodeToIds(passage, passageBudget, out _, out _);

        // Combined: queryIds + passageIds.Skip(1) = [CLS] q... [SEP] p... [SEP]
        var combined = queryIds.Concat(passageIds.Skip(1)).ToArray();
        var seqLen = combined.Length;

        var inputIds = combined.Select(id => (long)id).ToArray();
        var attentionMask = inputIds.Select(_ => 1L).ToArray();

        // Token type IDs: 0 for query segment, 1 for passage segment
        var tokenTypeIds = new long[seqLen];
        for (var i = queryIds.Count; i < seqLen; i++)
        {
            tokenTypeIds[i] = 1L;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, [1, seqLen])),
        };

        using var outputs = _session.Run(inputs);
        return outputs.First(o => o.Name == "logits").AsEnumerable<float>().First();
    }

    public void Dispose() => _session.Dispose();
}
