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

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };

        // Add meaningful keywords as independent search terms so dense+sparse search runs on each
        // (e.g. "болест на гравес" → also search with "гравес" alone)
        foreach (var word in ExtractKeywords(query))
        {
            terms.Add(word);
        }

        // Expand any term via dictionary if entries exist
        foreach (var term in terms.ToList())
        {
            var termLower = term.ToLowerInvariant();
            var illnesses = await db.Illnesses
                .Include(i => i.Aliases)
                .Where(i =>
                    i.NameEn.ToLower() == termLower ||
                    i.NameBg.ToLower() == termLower ||
                    i.Aliases.Any(a => a.Alias.ToLower() == termLower))
                .ToListAsync(cancellationToken);

            foreach (var illness in illnesses)
            {
                terms.Add(illness.NameEn);
                terms.Add(illness.NameBg);
                foreach (var alias in illness.Aliases)
                {
                    terms.Add(alias.Alias);
                }
            }
        }

        return [.. terms];
    }

    private static readonly HashSet<string> _stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "на", "е", "и", "с", "в", "от", "до", "се", "за", "при", "са", "ли",
        "да", "не", "то", "ще", "или", "но", "само", "те", "тя", "си",
        "болест", "заболяване", "симптом", "признак",
        "the", "a", "an", "of", "and", "or", "is", "are", "was", "what", "which", "how"
    };

    private static IEnumerable<string> ExtractKeywords(string query)
        => query.Split([' ', ',', '.', '?', '!', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !_stopwords.Contains(w));

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
