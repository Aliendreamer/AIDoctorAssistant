using System.Text.RegularExpressions;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Embedding;

public sealed partial class SparseVectorizer : ISparseVectorizer
{
    private readonly IBM25VocabStore _vocabStore;
    private BM25VocabSnapshot? _snapshot;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private const float _k1 = 1.5f;

    [GeneratedRegex(@"\p{L}+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokensRegex();

    public SparseVectorizer(IBM25VocabStore vocabStore) => _vocabStore = vocabStore;

    public async Task<SparseVector> VectorizePassageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SparseVector.Empty;
        }

        var vocab = await EnsureLoadedAsync(cancellationToken);
        if (vocab.TermIds.Count == 0)
        {
            return SparseVector.Empty;
        }

        return Compute(text, vocab);
    }

    public async Task<SparseVector> VectorizeQueryAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SparseVector.Empty;
        }

        var vocab = await EnsureLoadedAsync(cancellationToken);
        if (vocab.TermIds.Count == 0)
        {
            return SparseVector.Empty;
        }

        return Compute(text, vocab);
    }

    private async Task<BM25VocabSnapshot> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_snapshot is not null)
        {
            return _snapshot;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            _snapshot ??= await _vocabStore.LoadAsync(cancellationToken);
            return _snapshot;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static SparseVector Compute(string text, BM25VocabSnapshot vocab)
    {
        var tf = new Dictionary<uint, int>();

        foreach (Match match in TokensRegex().Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length <= 1)
            {
                continue;
            }

            if (vocab.TermIds.TryGetValue(token, out var termId))
            {
                tf[termId] = tf.GetValueOrDefault(termId) + 1;
            }
        }

        if (tf.Count == 0)
        {
            return SparseVector.Empty;
        }

        var entries = new Dictionary<uint, float>(tf.Count);
        foreach (var (termId, freq) in tf)
        {
            if (!vocab.IdfWeights.TryGetValue(termId, out var idf))
            {
                continue;
            }

            // BM25 with b=0 (no document-length normalisation)
            entries[termId] = idf * (freq * (_k1 + 1f)) / (freq + _k1);
        }

        return entries.Count > 0 ? new SparseVector { Entries = entries } : SparseVector.Empty;
    }
}
