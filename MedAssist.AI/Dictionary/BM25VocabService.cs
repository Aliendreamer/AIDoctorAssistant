using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.AI.Dictionary;

public sealed class BM25VocabService : IBM25VocabStore
{
    private readonly IDbContextFactory<MedAssistDbContext> _dbFactory;

    public BM25VocabService(IDbContextFactory<MedAssistDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var totalDocs = await db.Bm25Vocab
            .MaxAsync(v => (int?)v.TotalDocuments, cancellationToken) ?? 0;

        var vocab = await db.Bm25Vocab
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

    public async Task<int> GetTotalDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bm25Vocab.MaxAsync(v => (int?)v.TotalDocuments, cancellationToken) ?? 0;
    }

    public async Task UpsertTermsAsync(
        IReadOnlyDictionary<string, int> termDfs,
        int totalDocs,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Bm25Vocab.ExecuteUpdateAsync(
            s => s.SetProperty(v => v.TotalDocuments, totalDocs)
                   .SetProperty(v => v.UpdatedAt, DateTimeOffset.UtcNow),
            cancellationToken);

        var termKeys = termDfs.Keys.ToList();
        var existingMap = await db.Bm25Vocab
            .AsNoTracking()
            .Where(v => termKeys.Contains(v.Term))
            .ToDictionaryAsync(v => v.Term, cancellationToken);

        var toUpdate = new List<Bm25VocabEntity>();
        var toAdd = new List<Bm25VocabEntity>();

        foreach (var (term, df) in termDfs)
        {
            if (existingMap.TryGetValue(term, out var existing))
            {
                existing.DocumentFrequency += df;
                existing.TotalDocuments = totalDocs;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                toUpdate.Add(existing);
            }
            else
            {
                toAdd.Add(new Bm25VocabEntity
                {
                    Term = term,
                    DocumentFrequency = df,
                    TotalDocuments = totalDocs,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (toUpdate.Count > 0)
        {
            db.Bm25Vocab.UpdateRange(toUpdate);
        }

        if (toAdd.Count > 0)
        {
            db.Bm25Vocab.AddRange(toAdd);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static float ComputeIdf(int df, int n) =>
        n > 0 ? MathF.Log(1f + (n - df + 0.5f) / (df + 0.5f)) : 0f;
}
