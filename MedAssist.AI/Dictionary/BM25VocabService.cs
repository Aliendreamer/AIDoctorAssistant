using MedAssist.Data;
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

    private static float ComputeIdf(int df, int n) =>
        n > 0 ? MathF.Log(1f + (n - df + 0.5f) / (df + 0.5f)) : 0f;
}
