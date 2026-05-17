using MedAssist.Shared.Constants;
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
    private readonly SemaphoreSlim _inferenceGate = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private const int _maxTokens = 512;

    public CrossEncoderReranker(string modelDirectory)
    {
        var modelPath = Path.Combine(modelDirectory, OnnxConstants.Files.ModelOnnx);
        var vocabPath = Path.Combine(modelDirectory, OnnxConstants.Files.VocabTxt);

        _session = new InferenceSession(modelPath, new SessionOptions());
        _tokenizer = BertTokenizer.Create(vocabPath);
    }

    public async Task<IReadOnlyList<ScoredChunk>> RerankAsync(
        string query,
        IReadOnlyList<MedicalChunk> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var scores = new float[candidates.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, candidates.Count),
            new ParallelOptions { CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                await _inferenceGate.WaitAsync(ct).ConfigureAwait(false);
                try { scores[i] = Score(query, candidates[i].Text); }
                finally { _inferenceGate.Release(); }
            });

        return candidates
            .Select((chunk, i) => new ScoredChunk(chunk, scores[i]))
            .OrderByDescending(x => x.Score)
            .ToList();
    }

    private float Score(string query, string passage)
    {
        var queryIds = _tokenizer.EncodeToIds(query, _maxTokens, out _, out _);
        var passageBudget = Math.Max(3, _maxTokens - queryIds.Count + 1);
        var passageIds = _tokenizer.EncodeToIds(passage, passageBudget, out _, out _);

        var combined = queryIds.Concat(passageIds.Skip(1)).ToArray();
        var seqLen = combined.Length;

        var inputIds = combined.Select(id => (long)id).ToArray();
        var attentionMask = inputIds.Select(_ => 1L).ToArray();

        var tokenTypeIds = new long[seqLen];
        for (var i = queryIds.Count; i < seqLen; i++)
        {
            tokenTypeIds[i] = 1L;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(OnnxConstants.Inputs.InputIds, new DenseTensor<long>(inputIds, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor(OnnxConstants.Inputs.AttentionMask, new DenseTensor<long>(attentionMask, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor(OnnxConstants.Inputs.TokenTypeIds, new DenseTensor<long>(tokenTypeIds, [1, seqLen])),
        };

        using var outputs = _session.Run(inputs);
        return outputs.First(o => o.Name == OnnxConstants.Outputs.Logits).AsEnumerable<float>().First();
    }

    public void Dispose()
    {
        _session.Dispose();
        _inferenceGate.Dispose();
    }
}
