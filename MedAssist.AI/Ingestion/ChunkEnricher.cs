using MedAssist.Shared.Interfaces;

namespace MedAssist.AI.Ingestion;

public sealed class ChunkEnricher
{
    private readonly IMedicalDictionary _dictionary;

    public ChunkEnricher(IMedicalDictionary dictionary) => _dictionary = dictionary;

    public async Task<IReadOnlyList<string>> GetIcdCodesAsync(string chunkText, CancellationToken cancellationToken = default)
    {
        var allIllnesses = await _dictionary.GetAllAsync(cancellationToken);
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

    private static bool ContainsTerm(string text, string term) =>
        !string.IsNullOrWhiteSpace(term) &&
        text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
