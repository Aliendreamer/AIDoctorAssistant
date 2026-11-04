using MedAssist.Shared.Constants;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

/// <summary>Compares a run with a baseline: metric movement, setting differences and flipped questions.</summary>
public static class RunComparer
{
    private const string _correctness = "Correctness";
    private const string _grounding = "Grounding";
    private const string _retrieval = "Retrieval";
    private const string _format = "Format";
    private const string _reliability = "Reliability and cost";

    private sealed record Metric(
        string Group,
        string Label,
        string Description,
        MetricUnit Unit,
        Func<EvalRunSummary, double?> Read,
        bool HigherIsBetter,
        double Tolerance,
        bool Gates);

    private static readonly Metric[] _metrics =
    [
        Rate(_correctness, "Questions passed", "Questions that cleared every check.", s => s.PassRate),
        Judged(_correctness, "Fact recall", "Reference facts stated in the answers.", s => s.FactRecall),
        new(_correctness, "Unsafe claims", "Forbidden claims the answers made.", MetricUnit.Count, s => s.ForbiddenClaims, false, 0, true),
        Rate(_correctness, "Answer rate", "Answerable questions that got an answer from the books.", s => s.AnswerRate),
        Judged(_correctness, "Abstention accuracy", "Unanswerable questions correctly declined.", s => s.AbstainAccuracy),

        Judged(_grounding, "Faithfulness", "Answer sentences supported by the excerpts.", s => s.Faithfulness),
        Judged(_grounding, "Citation accuracy", "Cited sentences whose cited excerpts really support them.", s => s.CitationAccuracy),
        Info(_grounding, "Facts not from excerpts", "Reference facts in an answer but not its excerpts: model memory, not the books.", s => s.FactsNotFromExcerpts),

        Rate(_retrieval, "Hit@1", "The top-ranked chunk is relevant.", s => s.HitAt1),
        Rate(_retrieval, "Hit@5", "A relevant chunk is in the top five.", s => s.HitAtK),
        Rate(_retrieval, "MRR", "Mean reciprocal rank of the first relevant chunk.", s => s.Mrr),
        Rate(_retrieval, "nDCG@5", "Ranking quality of the top five.", s => s.Ndcg),
        Rate(_retrieval, "Excerpt precision", "Excerpts sent to the model that are relevant.", s => s.ExcerptPrecision),
        Judged(_retrieval, "Fact coverage", "Reference facts present in the excerpts: the recall ceiling for answers.", s => s.ContextFactCoverage),
        Info(_retrieval, "Facts lost in retrieval", "Facts never retrieved and absent from the answer.", s => s.FactsLostInRetrieval),
        Info(_retrieval, "Facts lost in generation", "Facts retrieved but dropped by the model.", s => s.FactsLostInGeneration),

        Rate(_format, "Citation markers", "Answers with at least one [n] marker.", s => s.CitationRate),
        Rate(_format, "Markers in range", "Answers whose markers all point at a real source.", s => s.MarkersInRange),
        Rate(_format, "Correct language", "Answers in the question's language.", s => s.LanguageOk),
        new(_format, "Formatting leakage", "Answers containing lists or markdown.", MetricUnit.Rate, s => s.Leakage, false, EvaluationConstants.RateTolerance, true),

        new(_reliability, "Pipeline errors", "Questions that failed with an exception.", MetricUnit.Count, s => s.Errors, false, 0, true),
        new(_reliability, "Context overflows", "Prompts that reached the context window.", MetricUnit.Count, s => s.ContextOverflows, false, 0, true),
        new(_reliability, "Excerpts dropped", "Excerpts removed to fit the token budget.", MetricUnit.Count, s => s.ExcerptsDropped, false, 0, false),
        new(_reliability, "Judge failures", "Questions the judge could not score.", MetricUnit.Count, s => s.JudgeFailures, false, 0, false),
        new(_reliability, "Mean latency", "Mean seconds per question.", MetricUnit.Seconds, s => s.MeanLatencySeconds, false, 3, false),
        new(_reliability, "P95 latency", "95th percentile seconds per question.", MetricUnit.Seconds, s => s.P95LatencySeconds, false, 5, false),
        new(_reliability, "Mean prompt tokens", "Mean answer-prompt size.", MetricUnit.Tokens, s => s.MeanPromptTokens, false, 300, false),
        new(_reliability, "Largest prompt", "Largest answer prompt.", MetricUnit.Tokens, s => s.MaxPromptTokens, false, 300, false),
    ];

    public static EvalComparison Compare(
        EvalRunSummary current,
        EvalRunSnapshot currentSnapshot,
        IReadOnlyList<EvalCaseResult> currentCases,
        EvalRunSummary? baseline,
        EvalRunSnapshot? baselineSnapshot,
        IReadOnlyList<EvalCaseResult> baselineCases)
    {
        var deltas = _metrics.Select(m => ToDelta(m, current, baseline)).ToList();
        if (baseline is null || baselineSnapshot is null)
        {
            return new EvalComparison { Verdict = EvalVerdict.NoBaseline, Metrics = deltas };
        }

        return new EvalComparison
        {
            Verdict = DecideVerdict(deltas, current),
            Metrics = deltas,
            Settings = DiffSettings(baselineSnapshot, currentSnapshot),
            Cases = DiffCases(baselineCases, currentCases),
            GoldenSetChanged = baselineSnapshot.GoldenSetHash != currentSnapshot.GoldenSetHash,
            JudgeChanged = baselineSnapshot.RunJudge && currentSnapshot.RunJudge
                && baselineSnapshot.JudgeModel != currentSnapshot.JudgeModel,
        };
    }

    private static EvalVerdict DecideVerdict(IReadOnlyList<MetricDelta> deltas, EvalRunSummary current)
    {
        if (deltas.Any(d => d.Gates && d.Change == MetricChange.Worse))
        {
            return EvalVerdict.Regressed;
        }

        var tooManyUnjudged = current.JudgeFailures > current.Cases * EvaluationConstants.MaxJudgeFailureShare;
        return tooManyUnjudged || deltas.Any(d => d.Gates && d.Change == MetricChange.NotMeasured)
            ? EvalVerdict.Inconclusive
            : EvalVerdict.NoRegression;
    }

    private static Metric Rate(string group, string label, string description, Func<EvalRunSummary, double?> read) =>
        new(group, label, description, MetricUnit.Rate, read, true, EvaluationConstants.RateTolerance, true);

    private static Metric Judged(string group, string label, string description, Func<EvalRunSummary, double?> read) =>
        new(group, label, description, MetricUnit.Rate, read, true, EvaluationConstants.JudgedRateTolerance, true);

    private static Metric Info(string group, string label, string description, Func<EvalRunSummary, int?> read) =>
        new(group, label, description, MetricUnit.Count, s => read(s), false, 0, false);

    private static MetricDelta ToDelta(Metric metric, EvalRunSummary current, EvalRunSummary? baseline)
    {
        var now = metric.Read(current);
        var before = baseline is null ? null : metric.Read(baseline);
        var change = before is not null && now is null ? MetricChange.NotMeasured : MetricChange.Unchanged;
        if (now is { } n && before is { } b && Math.Abs(n - b) > metric.Tolerance)
        {
            change = n > b == metric.HigherIsBetter ? MetricChange.Improved : MetricChange.Worse;
        }

        return new MetricDelta(metric.Group, metric.Label, metric.Description, metric.Unit, now, before, change, metric.Gates);
    }

    private static List<SettingChange> DiffSettings(EvalRunSnapshot baseline, EvalRunSnapshot current) =>
        baseline.ToRows().Zip(current.ToRows())
            .Where(pair => pair.First.Value != pair.Second.Value)
            .Select(pair => new SettingChange(pair.First.Label, pair.First.Value, pair.Second.Value))
            .ToList();

    private static List<CaseChange> DiffCases(IReadOnlyList<EvalCaseResult> baseline, IReadOnlyList<EvalCaseResult> current)
    {
        var before = baseline.ToDictionary(c => c.CaseId);
        return current
            .Where(c => before.TryGetValue(c.CaseId, out var old) && !old.Inconclusive && !c.Inconclusive && old.Passed != c.Passed)
            .Select(c => new CaseChange(c.CaseId, c.Question, c.Passed, c.Passed ? [] : c.FailureReasons))
            .ToList();
    }
}
