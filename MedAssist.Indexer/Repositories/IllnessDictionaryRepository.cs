using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Indexer.Repositories;

public sealed class IllnessDictionaryRepository
{
    private readonly MedAssistDbContext _db;

    public IllnessDictionaryRepository(MedAssistDbContext db) => _db = db;

    public async Task AddAsync(string icdCode, string nameEn, string nameBg, CancellationToken cancellationToken = default)
    {
        var icdUpper = icdCode.ToUpperInvariant();
        var existing = await _db.Illnesses
            .FirstOrDefaultAsync(i => i.IcdCode.ToUpper() == icdUpper, cancellationToken);

        if (existing is not null)
        {
            existing.NameEn = nameEn;
            existing.NameBg = nameBg;
        }
        else
        {
            _db.Illnesses.Add(new IllnessEntity
            {
                IcdCode = icdCode,
                NameEn = nameEn,
                NameBg = nameBg
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IllnessEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var illnesses = await _db.Illnesses
            .Include(i => i.Aliases)
            .OrderBy(i => i.NameEn)
            .ToListAsync(cancellationToken);

        return illnesses.Select(MapToEntry).ToList();
    }

    public async Task<IllnessEntry?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var nameLower = name.ToLowerInvariant();
        var illness = await _db.Illnesses
            .Include(i => i.Aliases)
            .FirstOrDefaultAsync(i =>
                i.NameEn.ToLower() == nameLower ||
                i.NameBg.ToLower() == nameLower ||
                i.Aliases.Any(a => a.Alias.ToLower() == nameLower),
                cancellationToken);

        return illness is null ? null : MapToEntry(illness);
    }

    public async Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var entry = await FindByNameAsync(query, cancellationToken);
        if (entry is null)
        {
            return [query];
        }

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };
        terms.Add(entry.NameEn);
        terms.Add(entry.NameBg);
        foreach (var alias in entry.Aliases)
        {
            terms.Add(alias);
        }

        return [.. terms];
    }

    private static IllnessEntry MapToEntry(IllnessEntity illness) => new()
    {
        Id = illness.Id,
        IcdCode = illness.IcdCode,
        NameEn = illness.NameEn,
        NameBg = illness.NameBg,
        Aliases = illness.Aliases.Select(a => a.Alias).ToArray()
    };
}
