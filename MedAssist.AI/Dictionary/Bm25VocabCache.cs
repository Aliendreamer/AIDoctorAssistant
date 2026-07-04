using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MedAssist.AI.Dictionary;

/// <summary>
/// Process-wide cache of the BM25 vocabulary snapshot (audit P1-9). The snapshot is effectively
/// static between ingest runs, but the sparse vectorizer was registered Scoped and reloaded the
/// entire <c>bm25_vocab</c> table from Postgres on every query. This singleton loads it once via a
/// short-lived scope (so it never captures a scoped <c>DbContext</c>) and exposes
/// <see cref="Invalidate"/> for the indexer to call after the vocabulary changes.
/// </summary>
public sealed class Bm25VocabCache(IServiceScopeFactory scopeFactory) : IBM25VocabStore
{
    private volatile BM25VocabSnapshot? _snapshot;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cached = _snapshot;
        if (cached is not null)
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is null)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IBM25VocabStore>();
                _snapshot = await store.LoadAsync(cancellationToken);
            }

            return _snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Drops the cached snapshot so the next <see cref="LoadAsync"/> reloads it.</summary>
    public void Invalidate() => _snapshot = null;

    public async Task ApplyBookContributionAsync(
        string bookId,
        IReadOnlyDictionary<string, int> termDocumentFrequencies,
        int chunkCount,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IBM25VocabStore>()
            .ApplyBookContributionAsync(bookId, termDocumentFrequencies, chunkCount, cancellationToken);
        Invalidate();
    }
}
