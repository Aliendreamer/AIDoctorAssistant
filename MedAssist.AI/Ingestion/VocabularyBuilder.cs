using System.Text.RegularExpressions;
using MedAssist.Shared.Interfaces;

namespace MedAssist.AI.Ingestion;

public sealed partial class VocabularyBuilder
{
    private readonly IBM25VocabStore _vocabStore;
    private readonly Dictionary<string, int> _termDfs = [];
    private int _totalChunks;

    [GeneratedRegex(@"\p{L}+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokensRegex();

    public VocabularyBuilder(IBM25VocabStore vocabStore) => _vocabStore = vocabStore;

    public void AddChunk(string text)
    {
        _totalChunks++;
        var seen = new HashSet<string>();

        foreach (Match match in TokensRegex().Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length <= 1 || !seen.Add(token))
            {
                continue;
            }

            _termDfs[token] = _termDfs.GetValueOrDefault(token) + 1;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_termDfs.Count == 0)
        {
            return;
        }

        var existingTotal = await _vocabStore.GetTotalDocumentsAsync(cancellationToken);
        var cumulativeTotal = existingTotal + _totalChunks;
        await _vocabStore.UpsertTermsAsync(_termDfs, cumulativeTotal, cancellationToken);
        _termDfs.Clear();
        _totalChunks = 0;
    }
}
