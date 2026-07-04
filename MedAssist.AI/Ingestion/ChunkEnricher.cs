using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Ingestion;

public sealed class ChunkEnricher
{
    private readonly IMedicalDictionary _dictionary;
    private IReadOnlyList<IllnessEntry>? _illnesses;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ChunkEnricher(IMedicalDictionary dictionary) => _dictionary = dictionary;

    public async Task<IReadOnlyList<string>> GetIcdCodesAsync(string chunkText, CancellationToken cancellationToken = default)
    {
        // Load the dictionary once per instance (one enricher per index run) instead of a full-table
        // reload on every chunk — the dominant ingestion cost (audit P1-10).
        var allIllnesses = await EnsureLoadedAsync(cancellationToken);
        var matchedCodes = new HashSet<string>();

        foreach (var illness in allIllnesses)
        {
            if (ContainsTerm(chunkText, illness.NameEn) ||
                ContainsTerm(chunkText, illness.NameBg) ||
                illness.Aliases.Any(alias => ContainsTerm(chunkText, alias)))
            {
                matchedCodes.Add(illness.IcdCode);
            }
        }

        return [.. matchedCodes];
    }

    private async Task<IReadOnlyList<IllnessEntry>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_illnesses is not null)
        {
            return _illnesses;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            _illnesses ??= await _dictionary.GetAllAsync(cancellationToken);
            return _illnesses;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static bool ContainsTerm(string text, string term) =>
        !string.IsNullOrWhiteSpace(term) &&
        text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
