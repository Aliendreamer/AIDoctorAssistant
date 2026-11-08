using MedAssist.AI.Evaluation;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using MedAssist.Web.Services.Evaluation;

namespace MedAssist.Tests;

public sealed class EvalRunAnalysisTests
{
    private static EvalCaseResult Answer(string id = "G1") => new()
    {
        CaseId = id,
        Outcome = RagOutcome.Answered,
        LatencySeconds = 10,
        PromptTokens = 1_000,
        Passed = true,
        Judged = true,
        Format = new FormatScore { HasMarkers = true, MarkersInRange = true, LanguageOk = true },
        Retrieval = new RetrievalScore { HitAt1 = true, HitAtK = true, ReciprocalRank = 1, Ndcg = 1, ExcerptPrecision = 1 },
        Facts =
        [
            new FactCheck { Fact = "kept", InExcerpts = true, InAnswer = true },
            new FactCheck { Fact = "dropped by model", InExcerpts = true, InAnswer = false },
            new FactCheck { Fact = "never retrieved", InExcerpts = false, InAnswer = false },
            new FactCheck { Fact = "from memory", InExcerpts = false, InAnswer = true },
        ],
        Claims =
        [
            new ClaimCheck { Claim = "a", Supported = true, CitationOk = true },
            new ClaimCheck { Claim = "b", Supported = false, CitationOk = false },
            new ClaimCheck { Claim = "c", Supported = true },
        ],
    };

    private static EvalRunSummary Summary(double passRate = 1, int errors = 0, int? forbidden = 0, double latency = 10, double? recall = 0.9) => new()
    {
        Cases = 20,
        PassRate = passRate,
        Errors = errors,
        ForbiddenClaims = forbidden,
        MeanLatencySeconds = latency,
        FactRecall = recall,
        AnswerRate = 1,
        HitAtK = 1,
        LanguageOk = 1,
    };

    private static EvalRunSnapshot Snapshot(string model = "qwen3:8b", int ctx = 8_192, string hash = "abc") =>
        new() { Model = model, JudgeModel = "judge:7b", ContextWindowTokens = ctx, GoldenSetHash = hash };

    private static EvalComparison Compare(EvalRunSummary current, EvalRunSummary? baseline) =>
        RunComparer.Compare(current, Snapshot(), [], baseline, baseline is null ? null : Snapshot(), []);

    private static MetricDelta Metric(EvalComparison comparison, string label) => comparison.Metrics.Single(m => m.Label == label);

    [Fact]
    public void Summarize_AttributesEachLostFactToItsStage()
    {
        var summary = RunSummarizer.Summarize([Answer()], 8_192);

        Assert.Equal(1, summary.FactsLostInRetrieval);
        Assert.Equal(1, summary.FactsLostInGeneration);
        Assert.Equal(1, summary.FactsNotFromExcerpts);
        Assert.Equal(0.5, summary.FactRecall);
        Assert.Equal(0.5, summary.ContextFactCoverage);
    }

    [Fact]
    public void Summarize_CitationAccuracyCountsOnlyCitedClaims()
    {
        var summary = RunSummarizer.Summarize([Answer()], 8_192);

        Assert.Equal(2.0 / 3, summary.Faithfulness!.Value, 6);
        Assert.Equal(0.5, summary.CitationAccuracy);
    }

    [Fact]
    public void Summarize_WithoutJudgeLeavesJudgedMetricsEmpty()
    {
        var unjudged = Answer() with { Judged = false, Facts = [], Claims = [] };

        var summary = RunSummarizer.Summarize([unjudged], 8_192);

        Assert.Null(summary.FactRecall);
        Assert.Null(summary.Faithfulness);
        Assert.Null(summary.ForbiddenClaims);
        Assert.Equal(1, summary.HitAtK);
    }

    [Fact]
    public void Summarize_UnjudgedCaseDoesNotCountTowardFactRecall()
    {
        var unjudged = Answer("G2") with { Judged = false, JudgeError = "bad", Facts = [], Claims = [] };

        var summary = RunSummarizer.Summarize([Answer(), unjudged], 8_192);

        Assert.Equal(0.5, summary.FactRecall);
        Assert.Equal(1, summary.JudgeFailures);
    }

    [Fact]
    public void Summarize_CountsPromptsThatReachTheContextWindow()
    {
        var summary = RunSummarizer.Summarize([Answer() with { PromptTokens = 8_192 }, Answer("G2")], 8_192);

        Assert.Equal(1, summary.ContextOverflows);
        Assert.Equal(8_192, summary.MaxPromptTokens);
    }

    [Fact]
    public void Summarize_ErrorsAreCountedAndExcludedFromLatency()
    {
        var failed = new EvalCaseResult { CaseId = "G2", Error = "boom", LatencySeconds = 500, Retrieval = new RetrievalScore() };

        var summary = RunSummarizer.Summarize([Answer(), failed], 8_192);

        Assert.Equal(1, summary.Errors);
        Assert.Equal(10, summary.MeanLatencySeconds);
        Assert.Equal(0.5, summary.AnswerRate);
    }

    [Fact]
    public void Summarize_AbstentionByRefusalNeedsNoJudge()
    {
        var refused = new EvalCaseResult { CaseId = "N1", Expectation = EvalExpectation.Abstain, Outcome = RagOutcome.BelowAnswerFloor };
        var fabricated = new EvalCaseResult { CaseId = "N2", Expectation = EvalExpectation.Abstain, Outcome = RagOutcome.Answered, Abstained = false };

        Assert.Equal(0.5, RunSummarizer.Summarize([refused, fabricated], 8_192).AbstainAccuracy);
    }

    [Fact]
    public void Summarize_EmptyRunDoesNotThrow()
    {
        var summary = RunSummarizer.Summarize([], 8_192);

        Assert.Equal(0, summary.Cases);
        Assert.Equal(0, summary.PassRate);
    }

    [Fact]
    public void Compare_WithoutBaselineHasNoVerdict()
    {
        var comparison = Compare(Summary(), null);

        Assert.Equal(EvalVerdict.NoBaseline, comparison.Verdict);
        Assert.All(comparison.Metrics, m => Assert.Equal(MetricChange.Unchanged, m.Change));
    }

    [Fact]
    public void Compare_OneQuestionOfNoiseIsNotARegression()
    {
        var comparison = Compare(Summary(passRate: 0.95), Summary(passRate: 1));

        Assert.Equal(EvalVerdict.NoRegression, comparison.Verdict);
    }

    [Fact]
    public void Compare_RealDropIsARegression()
    {
        var comparison = Compare(Summary(passRate: 0.8), Summary(passRate: 1));

        Assert.Equal(EvalVerdict.Regressed, comparison.Verdict);
        Assert.Equal(MetricChange.Worse, Metric(comparison, "Questions passed").Change);
    }

    [Fact]
    public void Compare_ASingleNewUnsafeClaimIsARegression()
    {
        var comparison = Compare(Summary(forbidden: 1), Summary(forbidden: 0));

        Assert.Equal(EvalVerdict.Regressed, comparison.Verdict);
    }

    [Fact]
    public void Compare_ASingleNewPipelineErrorIsARegression()
    {
        Assert.Equal(EvalVerdict.Regressed, Compare(Summary(errors: 1), Summary()).Verdict);
    }

    [Fact]
    public void Compare_SlowerIsReportedButDoesNotGate()
    {
        var comparison = Compare(Summary(latency: 30), Summary(latency: 10));

        Assert.Equal(MetricChange.Worse, Metric(comparison, "Mean latency").Change);
        Assert.Equal(EvalVerdict.NoRegression, comparison.Verdict);
    }

    [Fact]
    public void Compare_LowerIsBetterMetricsImproveDownward()
    {
        var comparison = Compare(Summary(latency: 5), Summary(latency: 20));

        Assert.Equal(MetricChange.Improved, Metric(comparison, "Mean latency").Change);
    }

    [Fact]
    public void Compare_RunWithoutJudgeAgainstAJudgedBaselineIsInconclusive()
    {
        var comparison = Compare(Summary(recall: null, forbidden: null), Summary(recall: 0.9));

        Assert.Equal(EvalVerdict.Inconclusive, comparison.Verdict);
        Assert.Equal(MetricChange.NotMeasured, Metric(comparison, "Fact recall").Change);
    }

    [Fact]
    public void Compare_ARealDropBeatsMissingEvidence()
    {
        var comparison = Compare(Summary(passRate: 0.5, recall: null), Summary(recall: 0.9));

        Assert.Equal(EvalVerdict.Regressed, comparison.Verdict);
    }

    [Fact]
    public void Compare_TooManyJudgeFailuresIsInconclusive()
    {
        var comparison = Compare(Summary() with { JudgeFailures = 3 }, Summary());

        Assert.Equal(EvalVerdict.Inconclusive, comparison.Verdict);
    }

    [Fact]
    public void Compare_AnOccasionalJudgeFailureIsTolerated()
    {
        var comparison = Compare(Summary() with { JudgeFailures = 1 }, Summary());

        Assert.Equal(EvalVerdict.NoRegression, comparison.Verdict);
    }

    [Fact]
    public void Compare_BothRunsWithoutJudgeCanStillBeCompared()
    {
        var unjudged = Summary(recall: null, forbidden: null);

        Assert.Equal(EvalVerdict.NoRegression, Compare(unjudged, unjudged).Verdict);
    }

    [Fact]
    public void Compare_FlagsADifferentJudge()
    {
        var comparison = RunComparer.Compare(
            Summary(), Snapshot() with { JudgeModel = "other" }, [], Summary(), Snapshot(), []);

        Assert.True(comparison.JudgeChanged);
    }

    [Fact]
    public void Compare_FlagsAChangedJudgeMethod()
    {
        var comparison = RunComparer.Compare(
            Summary(), Snapshot() with { JudgeMethod = "older" }, [], Summary(), Snapshot(), []);

        Assert.True(comparison.JudgeChanged);
    }

    [Fact]
    public void Compare_UndecidedQuestionsAreNotFlips()
    {
        var before = new[] { Answer("G1") };
        var after = new[] { Answer("G1") with { Passed = false, FailureReasons = ["x"] } };
        var undecidedBefore = new[] { Answer("G1") with { JudgeError = "bad" } };

        var real = RunComparer.Compare(Summary(), Snapshot(), after, Summary(), Snapshot(), before);
        var undecided = RunComparer.Compare(Summary(), Snapshot(), before, Summary(), Snapshot(), undecidedBefore);

        Assert.Single(real.Cases);
        Assert.Empty(undecided.Cases);
    }

    [Fact]
    public void Summarize_JudgeDownDoesNotInflateThePassRate()
    {
        var unjudged = Answer("G2") with { Judged = false, JudgeError = "down", Facts = [], Claims = [] };
        var failed = Answer("G3") with { Passed = false };

        var summary = RunSummarizer.Summarize([Answer(), unjudged, failed], 8_192);

        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Inconclusive);
        Assert.Equal(0.5, summary.PassRate);
    }

    [Fact]
    public void Compare_ListsChangedSettingsAndFlippedQuestions()
    {
        var before = new[] { Answer("G1"), Answer("G2") with { Passed = false } };
        var after = new[] { Answer("G1") with { Passed = false, FailureReasons = ["No citation markers"] }, Answer("G2") };

        var comparison = RunComparer.Compare(
            Summary(), Snapshot(model: "other:7b", ctx: 16_384), after, Summary(), Snapshot(), before);

        Assert.Contains(comparison.Settings, s => s is { Label: "Answer model", Baseline: "qwen3:8b", Current: "other:7b" });
        Assert.Contains(comparison.Settings, s => s.Label == "Context window");
        Assert.Equal(2, comparison.Settings.Count);
        Assert.Contains(comparison.Cases, c => c is { CaseId: "G1", NowPasses: false });
        Assert.Contains(comparison.Cases, c => c is { CaseId: "G2", NowPasses: true });
    }

    [Fact]
    public void Compare_FlagsAChangedGoldenSet()
    {
        var comparison = RunComparer.Compare(Summary(), Snapshot(hash: "new"), [], Summary(), Snapshot(hash: "old"), []);

        Assert.True(comparison.GoldenSetChanged);
    }

    [Fact]
    public void Snapshot_SurvivesTheJsonColumn()
    {
        var snapshot = Snapshot() with { Overrides = new RagRunOverrides { ModelId = "m", BookOnly = true, Temperature = 0 } };

        var restored = EvalJson.Deserialize<EvalRunSnapshot>(EvalJson.Serialize(snapshot));

        Assert.Equal(snapshot.Overrides, restored.Overrides);
        Assert.Equal(snapshot.ToRows(), restored.ToRows());
    }

    [Fact]
    public void CaseResult_SurvivesTheJsonColumn()
    {
        var result = Answer() with { QueryType = QueryType.Treatment, Expectation = EvalExpectation.Abstain };

        var restored = EvalJson.Deserialize<EvalCaseResult>(EvalJson.Serialize(result));

        Assert.Equal(RagOutcome.Answered, restored.Outcome);
        Assert.Equal(QueryType.Treatment, restored.QueryType);
        Assert.Equal(4, restored.Facts.Count);
    }

    [Theory]
    [InlineData(null, null, null, null, null)]
    [InlineData(500, null, null, null, "at least")]
    [InlineData(4_096, 4_096, null, null, "reserve")]
    [InlineData(null, null, 0, null, "at least 1")]
    [InlineData(null, null, null, 3.0, "Temperature")]
    public void FormRules_RejectMeaninglessSettings(int? window, int? reserve, int? excerpts, double? temperature, string? expected)
    {
        var problem = EvalFormRules.Validate(new RagRunOverrides
        {
            ContextWindowTokens = window,
            AnswerReserveTokens = reserve,
            MaxExcerpts = excerpts,
            Temperature = temperature,
        });

        if (expected is null)
        {
            Assert.Null(problem);
        }
        else
        {
            Assert.Contains(expected, problem);
        }
    }

    [Fact]
    public void Format_MetricValues()
    {
        Assert.Equal("83%", EvalFormat.Value(MetricUnit.Rate, 0.83));
        Assert.Equal("17.6 s", EvalFormat.Value(MetricUnit.Seconds, 17.6));
        Assert.Equal("5,221", EvalFormat.Value(MetricUnit.Tokens, 5_221));
        Assert.Equal("—", EvalFormat.Value(MetricUnit.Rate, null));
    }

    [Fact]
    public void Format_DeltaUsesPointsForRates()
    {
        var delta = new MetricDelta("g", "l", "d", MetricUnit.Rate, 0.8, 0.9, MetricChange.Worse, true);

        Assert.Equal("-10 pts", EvalFormat.Delta(delta));
    }

    [Fact]
    public void Coordinator_AdmitsOneRunAtATime()
    {
        var sut = new EvaluationCoordinator();
        var first = Guid.NewGuid();

        Assert.True(sut.TryReserve(first));
        Assert.False(sut.TryReserve(Guid.NewGuid()));

        sut.Release(first);

        Assert.True(sut.TryReserve(Guid.NewGuid()));
    }

    [Fact]
    public void Coordinator_CancelReachesTheRunToken()
    {
        var sut = new EvaluationCoordinator();
        var run = Guid.NewGuid();
        sut.TryReserve(run);
        var token = sut.TokenFor(run);

        Assert.False(sut.Cancel(Guid.NewGuid()));
        Assert.False(token.IsCancellationRequested);
        Assert.True(sut.Cancel(run));
        Assert.True(token.IsCancellationRequested);
    }
}
