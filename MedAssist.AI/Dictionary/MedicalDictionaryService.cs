using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.AI.Dictionary;

public sealed class MedicalDictionaryService : IMedicalDictionary
{
    private readonly IDbContextFactory<MedAssistDbContext> _dbFactory;

    public MedicalDictionaryService(IDbContextFactory<MedAssistDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var queryLower = query.ToLowerInvariant();

        var illnesses = await db.Illnesses
            .Include(i => i.Aliases)
            .Where(i =>
                i.NameEn.ToLower() == queryLower ||
                i.NameBg.ToLower() == queryLower ||
                i.Aliases.Any(a => a.Alias.ToLower() == queryLower))
            .ToListAsync(cancellationToken);

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };
        foreach (var illness in illnesses)
        {
            terms.Add(illness.NameEn);
            terms.Add(illness.NameBg);
            foreach (var alias in illness.Aliases)
            {
                terms.Add(alias.Alias);
            }
        }

        return [.. terms];
    }

    public async Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var icdUpper = icdCode.ToUpperInvariant();

        var illness = await db.Illnesses
            .Include(i => i.Aliases)
            .FirstOrDefaultAsync(i => i.IcdCode.ToUpper() == icdUpper, cancellationToken);

        return illness is null ? null : MapToEntry(illness);
    }

    public async Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var queryLower = query.ToLowerInvariant();
        var queryUpper = query.ToUpperInvariant();

        var illnesses = await db.Illnesses
            .Include(i => i.Aliases)
            .Where(i =>
                i.NameEn.ToLower().Contains(queryLower) ||
                i.NameBg.ToLower().Contains(queryLower) ||
                i.IcdCode.ToUpper().StartsWith(queryUpper))
            .OrderBy(i => i.NameEn)
            .Take(50)
            .ToListAsync(cancellationToken);

        return illnesses.Select(MapToEntry).ToList();
    }

    public async Task<IReadOnlyList<IllnessEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var illnesses = await db.Illnesses
            .Include(i => i.Aliases)
            .OrderBy(i => i.NameEn)
            .ToListAsync(cancellationToken);
        return illnesses.Select(MapToEntry).ToList();
    }

    private static IllnessEntry MapToEntry(IllnessEntity illness) =>
        new()
        {
            Id = illness.Id,
            IcdCode = illness.IcdCode,
            NameEn = illness.NameEn,
            NameBg = illness.NameBg,
            Aliases = illness.Aliases.Select(a => a.Alias).ToArray()
        };
}
