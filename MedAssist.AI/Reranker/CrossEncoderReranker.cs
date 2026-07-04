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

    /// <summary>
    /// Builds the model input ids: <c>[CLS] query [SEP]</c> followed by the passage without its own
    /// leading <c>[CLS]</c>, hard-capped at <paramref name="maxTokens"/> so the sequence can never
    /// exceed the position-embedding limit (audit P2-5). Pure and unit-testable.
    /// </summary>
    public static long[] CombineInputIds(IReadOnlyList<int> queryIds, IReadOnlyList<int> passageIds, int maxTokens)
    {
        var combined = new List<long>(queryIds.Count + passageIds.Count);
        foreach (var id in queryIds)
        {
            combined.Add(id);
        }

        for (var i = 1; i < passageIds.Count; i++) // skip the passage's own [CLS]
        {
            combined.Add(passageIds[i]);
        }

        if (combined.Count > maxTokens)
        {
            combined.RemoveRange(maxTokens, combined.Count - maxTokens);
        }

        return [.. combined];
    }

    private float Score(string query, string passage)
    {
        var queryIds = _tokenizer.EncodeToIds(query, _maxTokens, out _, out _);

        // Leave exactly enough room for the passage; encode nothing if the query already fills the
        // budget. CombineInputIds still hard-caps as a final safety net.
        var passageBudget = _maxTokens - queryIds.Count;
        IReadOnlyList<int> passageIds = passageBudget > 1
            ? _tokenizer.EncodeToIds(passage, passageBudget, out _, out _)
            : [];

        var inputIds = CombineInputIds(queryIds, passageIds, _maxTokens);
        var seqLen = inputIds.Length;

        var attentionMask = new long[seqLen];
        Array.Fill(attentionMask, 1L);

        var tokenTypeIds = new long[seqLen];
        for (var i = Math.Min(queryIds.Count, seqLen); i < seqLen; i++)
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
