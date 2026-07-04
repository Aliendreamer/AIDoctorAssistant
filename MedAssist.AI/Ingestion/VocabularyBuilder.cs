using System.Text.RegularExpressions;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;

namespace MedAssist.AI.Ingestion;

public sealed partial class VocabularyBuilder
{
    private readonly IBM25VocabStore _vocabStore;
    private readonly Dictionary<string, int> _termDfs = [];
    private int _totalChunks;

    [GeneratedRegex(TokenizationConstants.WordPattern, RegexOptions.None, matchTimeoutMilliseconds: 1000)]
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

    /// <summary>
    /// Persists the accumulated contribution for <paramref name="bookId"/> as a delta against its
    /// previously stored contribution, then resets so the builder can accumulate the next book.
    /// Always forwards — even with no terms — so re-indexing a book down to nothing clears its old
    /// contribution instead of leaving it stale.
    /// </summary>
    public async Task FlushAsync(string bookId, CancellationToken cancellationToken = default)
    {
        await _vocabStore.ApplyBookContributionAsync(bookId, _termDfs, _totalChunks, cancellationToken);
        _termDfs.Clear();
        _totalChunks = 0;
    }
}
