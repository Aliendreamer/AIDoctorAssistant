using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

/// <summary>Loads and validates the golden set embedded in this assembly.</summary>
public static class GoldenSetLoader
{
    private const string _resourceName = "MedAssist.AI.Evaluation.golden-set.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<GoldenSet> _embedded = new(LoadEmbedded);

    public static GoldenSet Embedded => _embedded.Value;

    public static GoldenSet Parse(byte[] json)
    {
        var file = JsonSerializer.Deserialize<GoldenSetFile>(json, _options)
            ?? throw new InvalidDataException("The golden set is empty.");
        Validate(file.Cases);
        return new GoldenSet(file.Cases, Convert.ToHexStringLower(SHA256.HashData(json))[..12]);
    }

    private static GoldenSet LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{_resourceName}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(buffer.ToArray());
    }

    private static void Validate(IReadOnlyList<EvalCase> cases)
    {
        var seen = new HashSet<string>();
        foreach (var c in cases)
        {
            if (!seen.Add(c.Id))
            {
                throw new InvalidDataException($"Duplicate golden case id '{c.Id}'.");
            }

            // Follow-ups run in file order, so the parent's conversation must already exist.
            if (c.FollowUpOf is not null && (c.FollowUpOf == c.Id || !seen.Contains(c.FollowUpOf)))
            {
                throw new InvalidDataException($"Golden case '{c.Id}' must follow an earlier case, not '{c.FollowUpOf}'.");
            }

            if (c.Language is not ("BG" or "EN"))
            {
                throw new InvalidDataException($"Golden case '{c.Id}' has unknown language '{c.Language}'.");
            }

            if (c.Expectation == EvalExpectation.Answer && (c.RequiredFacts.Count == 0 || c.RelevantTerms.Count == 0))
            {
                throw new InvalidDataException($"Golden case '{c.Id}' needs reference facts and relevance terms.");
            }

            if (c.RelevantTerms.SelectMany(g => g).Any(t => t != t.ToLowerInvariant()))
            {
                throw new InvalidDataException($"Golden case '{c.Id}' relevance terms must be lowercase.");
            }
        }
    }

    private sealed record GoldenSetFile(IReadOnlyList<EvalCase> Cases);
}
