using MedAssist.Shared.Interfaces;

namespace MedAssist.AI.Ingestion;

/// <summary>
/// Clears a single book's indexed vectors ahead of a (re)index.
///
/// Scoped to one book by design: it must NEVER drop the whole Qdrant collection or truncate the
/// shared BM25 vocab/stats tables. Those BM25 tables are corpus-global aggregates, so clearing
/// them for one book erased every other book — the P0-1 data-loss bug this replaces.
///
/// This cleaner deliberately does NOT touch the BM25 tables. Per-book document-frequency accounting
/// is owned by the indexer's Pass A (<see cref="BookIndexer"/> → <c>VocabularyBuilder.FlushAsync</c>
/// → <c>IBM25VocabStore.ApplyBookContributionAsync</c>), which folds in the book's new contribution
/// as a delta against its previously stored one — so a force re-index self-corrects the global DF
/// idempotently without ever clearing another book's terms (audit P2-1/P2-2).
/// </summary>
public sealed class BookReindexCleaner(IVectorStore vectorStore)
{
    private readonly IVectorStore _vectorStore = vectorStore;

    /// <summary>
    /// When <paramref name="force"/> is set, removes only <paramref name="bookId"/>'s vectors so the
    /// rebuild does not duplicate them. Non-force re-index leaves existing vectors in place.
    /// </summary>
    public async Task ClearBookAsync(string bookId, bool force, CancellationToken cancellationToken = default)
    {
        if (force)
        {
            await _vectorStore.DeleteByBookAsync(bookId, cancellationToken);
        }
    }
}
