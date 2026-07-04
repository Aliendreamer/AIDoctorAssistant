using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Services;

public sealed class BM25VocabService(MedAssistDbContext medAssistDbContext) : IBM25VocabStore
{
    private readonly MedAssistDbContext _medAssistDbContext = medAssistDbContext;


    public async Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {

        var totalDocs = await _medAssistDbContext.Bm25Stats
            .Where(s => s.Id == 1)
            .Select(s => s.TotalDocuments)
            .FirstOrDefaultAsync(cancellationToken);

        var vocab = await _medAssistDbContext.Bm25Vocab
            .AsNoTracking()
            .Where(v => v.DocumentFrequency >= 2)
            .OrderBy(v => v.Id)
            .Select(v => new { v.Id, v.Term, v.DocumentFrequency })
            .ToListAsync(cancellationToken);

        var termIds = new Dictionary<string, uint>(vocab.Count);
        var idfWeights = new Dictionary<uint, float>(vocab.Count);

        foreach (var entry in vocab)
        {
            var termId = (uint)entry.Id;
            termIds[entry.Term] = termId;
            idfWeights[termId] = ComputeIdf(entry.DocumentFrequency, totalDocs);
        }

        return new BM25VocabSnapshot(termIds, idfWeights, totalDocs);
    }

    public async Task ApplyBookContributionAsync(
        string bookId,
        IReadOnlyDictionary<string, int> termDocumentFrequencies,
        int chunkCount,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _medAssistDbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // The book's previously stored contribution — the baseline every delta is measured against.
        var oldBookRows = await _medAssistDbContext.Bm25BookTerms
            .Where(t => t.BookId == bookId)
            .ToListAsync(cancellationToken);
        var oldTermDfs = oldBookRows.ToDictionary(t => t.Term, t => t.DocumentFrequency);

        var oldStats = await _medAssistDbContext.Bm25BookStats
            .FirstOrDefaultAsync(s => s.BookId == bookId, cancellationToken);
        var oldChunkCount = oldStats?.ChunkCount ?? 0;

        // 1. Corpus size: apply only the change in this book's chunk count.
        var totalDelta = chunkCount - oldChunkCount;
        if (totalDelta != 0)
        {
            var stats = await _medAssistDbContext.Bm25Stats.FindAsync([1], cancellationToken);
            if (stats is null)
            {
                _medAssistDbContext.Bm25Stats.Add(new Bm25StatsEntity
                {
                    Id = 1,
                    TotalDocuments = totalDelta,
                    UpdatedAt = now
                });
            }
            else
            {
                stats.TotalDocuments += totalDelta;
                stats.UpdatedAt = now;
            }
        }

        // 2. Global document frequencies: fold in each term's (new − old) delta.
        var affectedTerms = new HashSet<string>(oldTermDfs.Keys);
        affectedTerms.UnionWith(termDocumentFrequencies.Keys);

        var deltas = new Dictionary<string, int>();
        foreach (var term in affectedTerms)
        {
            var delta = termDocumentFrequencies.GetValueOrDefault(term) - oldTermDfs.GetValueOrDefault(term);
            if (delta != 0)
            {
                deltas[term] = delta;
            }
        }

        if (deltas.Count > 0)
        {
            var deltaTerms = deltas.Keys.ToList();
            var globalRows = await _medAssistDbContext.Bm25Vocab
                .Where(v => deltaTerms.Contains(v.Term))
                .ToDictionaryAsync(v => v.Term, cancellationToken);

            foreach (var (term, delta) in deltas)
            {
                if (globalRows.TryGetValue(term, out var row))
                {
                    row.DocumentFrequency += delta;
                    row.UpdatedAt = now;
                    // The last contributor left — drop the row rather than keep a zero (or negative,
                    // were the accounting ever off) entry lingering in the corpus vocabulary.
                    if (row.DocumentFrequency <= 0)
                    {
                        _medAssistDbContext.Bm25Vocab.Remove(row);
                    }
                }
                else if (delta > 0)
                {
                    _medAssistDbContext.Bm25Vocab.Add(new Bm25VocabEntity
                    {
                        Term = term,
                        DocumentFrequency = delta,
                        UpdatedAt = now
                    });
                }
            }
        }

        // 3. Replace the book's stored contribution in place (update/insert/delete per term) so a
        //    delete+insert of the same (book_id, term) key never collides in one SaveChanges.
        var oldByTerm = oldBookRows.ToDictionary(t => t.Term);
        foreach (var (term, df) in termDocumentFrequencies)
        {
            if (df <= 0)
            {
                continue;
            }

            if (oldByTerm.Remove(term, out var existing))
            {
                existing.DocumentFrequency = df;
            }
            else
            {
                _medAssistDbContext.Bm25BookTerms.Add(new Bm25BookTermEntity
                {
                    BookId = bookId,
                    Term = term,
                    DocumentFrequency = df
                });
            }
        }

        // Terms the book no longer contains.
        if (oldByTerm.Count > 0)
        {
            _medAssistDbContext.Bm25BookTerms.RemoveRange(oldByTerm.Values);
        }

        if (oldStats is null)
        {
            _medAssistDbContext.Bm25BookStats.Add(new Bm25BookStatsEntity
            {
                BookId = bookId,
                ChunkCount = chunkCount,
                UpdatedAt = now
            });
        }
        else
        {
            oldStats.ChunkCount = chunkCount;
            oldStats.UpdatedAt = now;
        }

        await _medAssistDbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static float ComputeIdf(int df, int n) =>
        n > 0 ? MathF.Log(1f + (n - df + 0.5f) / (df + 0.5f)) : 0f;
}
