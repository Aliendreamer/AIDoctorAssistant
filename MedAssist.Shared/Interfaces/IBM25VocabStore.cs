using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface IBM25VocabStore
{
    Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a single book's BM25 contribution (its per-term document frequencies and chunk count)
    /// and folds the <em>delta</em> versus the book's previously stored contribution into the global
    /// <c>bm25_vocab</c> / <c>bm25_stats</c> aggregates. Re-applying the same contribution is a no-op,
    /// so re-index and resume never inflate document frequencies (audit P2-1/P2-2).
    /// </summary>
    Task ApplyBookContributionAsync(
        string bookId,
        IReadOnlyDictionary<string, int> termDocumentFrequencies,
        int chunkCount,
        CancellationToken cancellationToken = default);
}
