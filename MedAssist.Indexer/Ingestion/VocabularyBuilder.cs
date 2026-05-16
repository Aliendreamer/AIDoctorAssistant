using System.Text.RegularExpressions;
using MedAssist.Indexer.Repositories;

namespace MedAssist.Indexer.Ingestion;

public sealed partial class VocabularyBuilder
{
    private readonly BM25VocabRepository _repo;
    private readonly Dictionary<string, int> _termDfs = [];
    private int _totalChunks;

    [GeneratedRegex(@"\p{L}+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokensRegex();

    public VocabularyBuilder(BM25VocabRepository repo) => _repo = repo;

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

    public async Task FlushAsync(int existingTotal, CancellationToken cancellationToken = default)
    {
        if (_termDfs.Count == 0)
        {
            return;
        }

        var cumulativeTotal = existingTotal + _totalChunks;
        await _repo.UpsertTermsAsync(_termDfs, cumulativeTotal, cancellationToken);
        _termDfs.Clear();
        _totalChunks = 0;
    }
}
