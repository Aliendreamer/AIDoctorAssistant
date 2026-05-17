using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.AI.Dictionary;

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

    public async Task<int> GetTotalDocumentsAsync(CancellationToken cancellationToken = default)
    {

        return await _medAssistDbContext.Bm25Stats
            .Where(s => s.Id == 1)
            .Select(s => s.TotalDocuments)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertTermsAsync(
        IReadOnlyDictionary<string, int> termDfs,
        int totalDocs,
        CancellationToken cancellationToken = default)
    {

        await using var tx = await _medAssistDbContext.Database.BeginTransactionAsync(cancellationToken);

        // Update global stats — one row, no full-table scan
        var stats = await _medAssistDbContext.Bm25Stats.FindAsync([1], cancellationToken);
        if (stats is null)
        {
            _medAssistDbContext.Bm25Stats.Add(new Bm25StatsEntity
            {
                Id = 1,
                TotalDocuments = totalDocs,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            stats.TotalDocuments = totalDocs;
            stats.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var termKeys = termDfs.Keys.ToList();
        var existingMap = await _medAssistDbContext.Bm25Vocab
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
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                toUpdate.Add(existing);
            }
            else
            {
                toAdd.Add(new Bm25VocabEntity
                {
                    Term = term,
                    DocumentFrequency = df,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (toUpdate.Count > 0)
        {
            _medAssistDbContext.Bm25Vocab.UpdateRange(toUpdate);
        }

        if (toAdd.Count > 0)
        {
            _medAssistDbContext.Bm25Vocab.AddRange(toAdd);
        }

        await _medAssistDbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static float ComputeIdf(int df, int n) =>
        n > 0 ? MathF.Log(1f + (n - df + 0.5f) / (df + 0.5f)) : 0f;
}
