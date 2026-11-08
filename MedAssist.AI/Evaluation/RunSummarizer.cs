using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

public static class RunSummarizer
{
    public static EvalRunSummary Summarize(IReadOnlyList<EvalCaseResult> results, int contextWindowTokens)
    {
        var answerable = results.Where(r => r.Expectation == EvalExpectation.Answer).ToList();
        var abstain = results.Where(r => r.Expectation == EvalExpectation.Abstain).ToList();
        var answered = answerable.Where(r => r.Outcome == RagOutcome.Answered && r.Error is null).ToList();
        var judged = answerable.Where(r => r.Judged).ToList();
        var facts = judged.SelectMany(r => r.Facts).ToList();
        var allClaims = judged.SelectMany(r => r.Claims).ToList();
        var claims = allClaims.Where(c => c.JudgeError is null).ToList();
        var citedClaims = claims.Where(c => c.CitationOk is not null).ToList();
        var retrieval = answerable.Where(r => r.Retrieval is not null).Select(r => r.Retrieval!).ToList();
        var latencies = results.Where(r => r.Error is null).Select(r => r.LatencySeconds).Order().ToList();
        var prompts = results.Where(r => r.PromptTokens > 0).Select(r => r.PromptTokens).ToList();
        // A pure pipeline refusal is a correct abstention and needs no judge; an unjudged generated
        // answer stays unknown rather than counting either way.
        var abstainKnown = abstain.Where(r => r.Error is null && (r.Outcome != RagOutcome.Answered || r.Abstained is not null)).ToList();
        var hasFacts = facts.Count > 0;
        var decided = results.Where(r => !r.Inconclusive).ToList();

        return new EvalRunSummary
        {
            Cases = results.Count,
            Passed = decided.Count(r => r.Passed),
            Inconclusive = results.Count - decided.Count,
            Errors = results.Count(r => r.Error is not null),
            JudgeFailures = results.Count(r => r.JudgeError is not null),
            UnjudgedClaims = allClaims.Count - claims.Count,
            PassRate = Rate(decided, r => r.Passed),

            FactRecall = hasFacts ? Rate(facts, f => f.InAnswer == true) : null,
            ForbiddenClaims = judged.Count > 0 ? judged.Sum(r => r.ForbiddenStated.Count) : null,
            AnswerRate = Rate(answerable, r => r.Outcome == RagOutcome.Answered && r.Error is null),
            AbstainAccuracy = abstainKnown.Count > 0
                ? Rate(abstainKnown, r => r.Outcome != RagOutcome.Answered || r.Abstained == true)
                : null,

            Faithfulness = claims.Count > 0 ? Rate(claims, c => c.Supported) : null,
            CitationAccuracy = citedClaims.Count > 0 ? Rate(citedClaims, c => c.CitationOk == true) : null,

            HitAt1 = Rate(retrieval, s => s.HitAt1),
            HitAtK = Rate(retrieval, s => s.HitAtK),
            Mrr = Mean(retrieval, s => s.ReciprocalRank),
            Ndcg = Mean(retrieval, s => s.Ndcg),
            ExcerptPrecision = Mean(retrieval, s => s.ExcerptPrecision),
            ContextFactCoverage = hasFacts ? Rate(facts, f => f.InExcerpts == true) : null,

            FactsLostInRetrieval = hasFacts ? facts.Count(f => f.InExcerpts == false && f.InAnswer == false) : null,
            FactsLostInGeneration = hasFacts ? facts.Count(f => f.InExcerpts == true && f.InAnswer == false) : null,
            FactsNotFromExcerpts = hasFacts ? facts.Count(f => f.InExcerpts == false && f.InAnswer == true) : null,

            CitationRate = Rate(answered, r => r.Format.HasMarkers),
            MarkersInRange = Rate(answered, r => r.Format.MarkersInRange),
            LanguageOk = Rate(answered, r => r.Format.LanguageOk),
            Leakage = Rate(answered, r => r.Format.Leakage),

            MeanLatencySeconds = latencies.Count > 0 ? latencies.Average() : 0,
            P95LatencySeconds = latencies.Count > 0 ? latencies[(int)Math.Ceiling(latencies.Count * 0.95) - 1] : 0,
            MeanPromptTokens = prompts.Count > 0 ? prompts.Average() : 0,
            MaxPromptTokens = prompts.Count > 0 ? prompts.Max() : 0,
            ContextOverflows = prompts.Count(p => p >= contextWindowTokens),
            ExcerptsDropped = results.Sum(r => r.ExcerptsDropped),
        };
    }

    private static double Rate<T>(IReadOnlyCollection<T> items, Func<T, bool> predicate) =>
        items.Count > 0 ? items.Count(predicate) / (double)items.Count : 0;

    private static double Mean<T>(IReadOnlyCollection<T> items, Func<T, double> selector) =>
        items.Count > 0 ? items.Average(selector) : 0;
}
