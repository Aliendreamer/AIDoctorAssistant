using System.Text.RegularExpressions;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

/// <summary>
/// The parts of grounding a small judge model gets wrong and code gets right: whether a quote really is in
/// the excerpts, which excerpt it came from, and whether a statement's numbers appear in its evidence.
/// </summary>
internal static partial class ClaimEvidence
{
    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\d+(?:[.,]\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"(?<=[.!?;])\s+|\n+")]
    private static partial Regex SentenceBreakRegex();

    /// <summary>
    /// The excerpts holding a quote. It matches on most of its words, not exactly, because OCR text has
    /// stray characters the model tidies up when copying.
    /// </summary>
    public static IReadOnlyList<int> FindExcerpts(string quote, IReadOnlyList<EvalExcerpt> excerpts)
    {
        var words = Words(quote);
        if (words.Count < EvaluationConstants.MinQuoteWords)
        {
            return [];
        }

        return excerpts
            .Where(excerpt =>
            {
                var excerptWords = Words(excerpt.Text).ToHashSet();
                return words.Count(excerptWords.Contains) >= words.Count * EvaluationConstants.MinQuoteWordShare;
            })
            .Select(excerpt => excerpt.Number)
            .ToList();
    }

    /// <summary>
    /// The evidence a claim is checked against: the quotes that really are in the excerpts, plus the
    /// sentences holding any of the claim's numbers the quotes missed. No real quote means no evidence.
    /// </summary>
    public static IReadOnlyList<string> Gather(
        string claim, IReadOnlyList<string> quotes, IReadOnlyList<EvalExcerpt> excerpts)
    {
        var verified = quotes.Where(quote => FindExcerpts(quote, excerpts).Count > 0).ToList();
        if (verified.Count == 0)
        {
            return [];
        }

        var wanted = MissingNumbers(claim, verified);
        if (wanted.Count == 0)
        {
            return verified;
        }

        var added = excerpts
            .SelectMany(excerpt => SentenceBreakRegex().Split(excerpt.Text))
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length >= EvaluationConstants.MinEvidenceSentenceChars && !verified.Contains(sentence))
            .Select(sentence => (Sentence: sentence, Matches: Numbers(sentence).Intersect(wanted).Count()))
            .Where(candidate => candidate.Matches > 0)
            .OrderByDescending(candidate => candidate.Matches)
            .Take(EvaluationConstants.MaxAddedEvidenceSentences)
            .Select(candidate => candidate.Sentence);

        return [.. verified, .. added];
    }

    /// <summary>Numbers in the claim that the evidence never states.</summary>
    public static IReadOnlyList<string> MissingNumbers(string claim, IReadOnlyList<string> evidence)
    {
        var stated = Numbers(string.Join(' ', evidence));
        return Numbers(claim).Where(number => !stated.Contains(number)).ToList();
    }

    /// <summary>Numbers as written, with decimal commas normalised; digits inside names such as G6PD are skipped.</summary>
    public static IReadOnlySet<string> Numbers(string text) =>
        NumberRegex().Matches(text).Select(match => match.Value.Replace(',', '.')).ToHashSet();

    private static List<string> Words(string text) =>
        WordRegex().Matches(text).Select(match => match.Value.ToLowerInvariant()).ToList();
}
