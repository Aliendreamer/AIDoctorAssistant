using System.Text.RegularExpressions;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

/// <summary>The checks that need no model: answer format, retrieval ranks, and the pass/fail decision.</summary>
public static partial class CaseScorer
{
    [GeneratedRegex(@"\[\s*(\d+(?:\s*,\s*\d+)*)\s*\]")]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"^\s*([-*•]|\d+[.)]|#{1,6})\s", RegexOptions.Multiline)]
    private static partial Regex LeakRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"([.!?])\s*((?:\[\s*\d+(?:\s*,\s*\d+)*\s*\]\s*)+)")]
    private static partial Regex TrailingMarkerRegex();

    public static FormatScore ScoreFormat(string language, string answer, int bookSources)
    {
        var numbers = CitedNumbers(answer);
        var letters = answer.Where(char.IsLetter).ToList();
        var cyrillic = letters.Count(c => c is >= 'Ѐ' and <= 'ӿ') / (double)Math.Max(1, letters.Count);

        return new FormatScore
        {
            Markers = numbers.Count,
            HasMarkers = numbers.Count > 0,
            MarkersInRange = numbers.All(n => n >= 1 && n <= bookSources),
            LanguageOk = language == "BG" ? cyrillic > 0.5 : cyrillic < 0.1,
            CyrillicShare = Math.Round(cyrillic, 2),
            Leakage = LeakRegex().IsMatch(answer) || answer.Contains("**", StringComparison.Ordinal),
        };
    }

    public static bool IsRelevant(EvalCase evalCase, TracedChunk chunk)
    {
        var haystack = $"{chunk.ChapterTitle} {chunk.SectionTitle} {chunk.Text}".ToLowerInvariant();
        return evalCase.RelevantTerms.Count > 0
            && evalCase.RelevantTerms.All(group => group.Any(term => haystack.Contains(term, StringComparison.Ordinal)));
    }

    /// <summary>Scores binary relevance flags in rank order. nDCG's ideal is limited to what was ranked.</summary>
    public static RetrievalScore ScoreRetrieval(IReadOnlyList<bool> ranked, IReadOnlyList<bool> excerpts)
    {
        var depth = EvaluationConstants.RetrievalDepth;
        var firstHit = ranked.ToList().IndexOf(true);
        var dcg = ranked.Take(depth).Select((relevant, i) => relevant ? Gain(i) : 0).Sum();
        var ideal = Enumerable.Range(0, Math.Min(depth, ranked.Count(r => r))).Sum(Gain);

        return new RetrievalScore
        {
            HitAt1 = firstHit == 0,
            HitAtK = firstHit >= 0 && firstHit < depth,
            ReciprocalRank = firstHit >= 0 ? 1.0 / (firstHit + 1) : 0,
            Ndcg = ideal > 0 ? dcg / ideal : 0,
            ExcerptPrecision = excerpts.Count > 0 ? excerpts.Count(e => e) / (double)excerpts.Count : 0,
        };

        static double Gain(int rank) => 1 / Math.Log2(rank + 2);
    }

    /// <summary>Splits an answer into the sentences the judge checks, each with the excerpts it cites.</summary>
    public static IReadOnlyList<AnswerClaim> ExtractClaims(string answer)
    {
        // "Claim. [1] Next" cites the sentence before the marker, so pull the marker inside it first.
        var normalized = TrailingMarkerRegex().Replace(answer, m => $" {m.Groups[2].Value.Trim()}{m.Groups[1].Value} ");

        var claims = new List<AnswerClaim>();
        foreach (var sentence in SentenceBoundaryRegex().Split(normalized))
        {
            var text = MarkerRegex().Replace(sentence, string.Empty).Trim();
            if (text.Length >= EvaluationConstants.MinClaimChars)
            {
                claims.Add(new AnswerClaim(text, CitedNumbers(sentence).Distinct().ToList()));
            }
        }

        return claims.Take(EvaluationConstants.MaxJudgedClaims).ToList();
    }

    /// <summary>Sets the pass flag and reasons from whatever has been scored so far.</summary>
    public static EvalCaseResult Decide(EvalCaseResult result)
    {
        var reasons = result.Expectation == EvalExpectation.Abstain ? AbstainFailures(result) : AnswerFailures(result);
        return result with { Passed = reasons.Count == 0, FailureReasons = reasons };
    }

    private static List<string> AbstainFailures(EvalCaseResult r)
    {
        if (r.Error is not null)
        {
            return [$"Pipeline error: {r.Error}"];
        }

        return r.Outcome == RagOutcome.Answered && r.Abstained == false ? ["Answered instead of abstaining"] : [];
    }

    private static List<string> AnswerFailures(EvalCaseResult r)
    {
        if (r.Error is not null)
        {
            return [$"Pipeline error: {r.Error}"];
        }

        if (r.Outcome != RagOutcome.Answered)
        {
            return [$"No answer from the books ({r.Outcome})"];
        }

        var reasons = new List<string>();
        AddIf(!r.Format.HasMarkers, "No citation markers");
        AddIf(!r.Format.MarkersInRange, "Citation marker out of range");
        AddIf(!r.Format.LanguageOk, "Wrong answer language");
        AddIf(r.Format.Leakage, "List or markdown leakage");
        AddIf(r.Retrieval is { HitAtK: false }, "No relevant passage in the top ranks");

        var checkedFacts = r.Facts.Where(f => f.InAnswer is not null).ToList();
        if (checkedFacts.Count > 0)
        {
            var found = checkedFacts.Count(f => f.InAnswer == true);
            AddIf(found / (double)checkedFacts.Count < EvaluationConstants.MinFactRecall,
                $"Only {found} of {checkedFacts.Count} reference facts in the answer");
        }

        foreach (var claim in r.ForbiddenStated)
        {
            reasons.Add($"Unsafe claim: {claim}");
        }

        if (r.Claims.Count > 0)
        {
            var supported = r.Claims.Count(c => c.Supported);
            AddIf(supported / (double)r.Claims.Count < EvaluationConstants.MinFaithfulness,
                $"Only {supported} of {r.Claims.Count} sentences supported by the excerpts");
        }

        return reasons;

        void AddIf(bool condition, string reason)
        {
            if (condition)
            {
                reasons.Add(reason);
            }
        }
    }

    private static List<int> CitedNumbers(string text) =>
        MarkerRegex().Matches(text)
            .SelectMany(m => m.Groups[1].Value.Split(','))
            .Select(n => int.Parse(n.Trim()))
            .ToList();
}

/// <summary>One answer sentence and the excerpt numbers it cites.</summary>
public sealed record AnswerClaim(string Text, IReadOnlyList<int> CitedExcerpts);
