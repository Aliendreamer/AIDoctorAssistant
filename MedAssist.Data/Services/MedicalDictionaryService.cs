using System.Collections.Frozen;
using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Services;

public sealed class MedicalDictionaryService(MedAssistDbContext medAssistDbContext) : IMedicalDictionary
{
    private readonly MedAssistDbContext _medAssistDbContext = medAssistDbContext;
    public async Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default)
    {

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };

        // Add meaningful keywords as independent search terms so dense+sparse search runs on each
        // (e.g. "болест на гравес" → also search with "гравес" alone)
        foreach (var word in ExtractKeywords(query))
        {
            terms.Add(word);
        }

        // Expand all terms against the dictionary in ONE query instead of one round-trip per term
        // (audit P2-17). Contains(...) translates to a SQL IN (...).
        var termLowers = terms.Select(t => t.ToLowerInvariant()).ToHashSet();
        var matches = await _medAssistDbContext.Illnesses
            .AsNoTracking()
            .Include(i => i.Aliases)
            .Where(i =>
                termLowers.Contains(i.NameEn.ToLower()) ||
                termLowers.Contains(i.NameBg.ToLower()) ||
                i.Aliases.Any(a => termLowers.Contains(a.Alias.ToLower())))
            .ToListAsync(cancellationToken);

        foreach (var illness in matches)
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

    // FrozenSet: built once, read-only, optimized for fast repeated Contains lookups (.NET 8+).
    private static readonly FrozenSet<string> _stopwords = new[]
    {
        "на", "е", "и", "с", "в", "от", "до", "се", "за", "при", "са", "ли",
        "да", "не", "то", "ще", "или", "но", "само", "те", "тя", "си",
        "болест", "заболяване", "симптом", "признак",
        "the", "a", "an", "of", "and", "or", "is", "are", "was", "what", "which", "how"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ExtractKeywords(string query)
        => query.Split([' ', ',', '.', '?', '!', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !_stopwords.Contains(w));

    public async Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken cancellationToken = default)
    {

        var icdUpper = icdCode.ToUpperInvariant();

        var illness = await _medAssistDbContext.Illnesses
            .AsNoTracking()
            .Include(i => i.Aliases)
            .FirstOrDefaultAsync(i => i.IcdCode.ToUpper() == icdUpper, cancellationToken);

        return illness is null ? null : MapToEntry(illness);
    }

    public async Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();
        var queryUpper = query.ToUpperInvariant();

        var illnesses = await _medAssistDbContext.Illnesses
            .AsNoTracking()
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
        var illnesses = await _medAssistDbContext.Illnesses
            .AsNoTracking()
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
