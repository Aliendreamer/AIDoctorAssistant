using MedAssist.AI.Evaluation;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Tests;

public sealed class EvalScoringTests
{
    private static EvalCaseResult Good() => new()
    {
        CaseId = "G1",
        Language = "BG",
        Answer = "Отговор [1].",
        Outcome = RagOutcome.Answered,
        Format = new FormatScore { Markers = 1, HasMarkers = true, MarkersInRange = true, LanguageOk = true },
        Retrieval = new RetrievalScore { HitAtK = true },
    };

    [Theory]
    [InlineData("BG", "Кавазаки е васкулит [1].", true)]
    [InlineData("BG", "Kawasaki is a vasculitis [1].", false)]
    [InlineData("EN", "Kawasaki is a vasculitis [1].", true)]
    [InlineData("EN", "Кавазаки е васкулит [1].", false)]
    public void ScoreFormat_ChecksTheAnswerLanguage(string language, string answer, bool expected)
    {
        Assert.Equal(expected, CaseScorer.ScoreFormat(language, answer, 1).LanguageOk);
    }

    [Fact]
    public void ScoreFormat_ReadsMarkerGroupsAndRange()
    {
        var score = CaseScorer.ScoreFormat("EN", "One [1]. Two [2, 3]. Three [4].", 3);

        Assert.Equal(4, score.Markers);
        Assert.True(score.HasMarkers);
        Assert.False(score.MarkersInRange);
    }

    [Theory]
    [InlineData("Plain prose.", false)]
    [InlineData("- a bullet", true)]
    [InlineData("1. numbered", true)]
    [InlineData("## heading", true)]
    [InlineData("Some **bold** text", true)]
    public void ScoreFormat_DetectsLeakage(string answer, bool expected)
    {
        Assert.Equal(expected, CaseScorer.ScoreFormat("EN", answer, 0).Leakage);
    }

    [Fact]
    public void IsRelevant_NeedsAMatchInEveryGroup()
    {
        var evalCase = new EvalCase { RelevantTerms = [["гърч", "конвулс"], ["фебрил", "темпер"]] };

        Assert.True(CaseScorer.IsRelevant(evalCase, new TracedChunk { Text = "Фебрилни гърчове при деца" }));
        Assert.False(CaseScorer.IsRelevant(evalCase, new TracedChunk { Text = "Гърчове при епилепсия" }));
    }

    [Fact]
    public void IsRelevant_LooksAtSectionTitles()
    {
        var evalCase = new EvalCase { RelevantTerms = [["кавазаки"]] };

        Assert.True(CaseScorer.IsRelevant(evalCase, new TracedChunk { SectionTitle = "Болест на Кавазаки", Text = "Лечение" }));
    }

    [Fact]
    public void IsRelevant_LeadingSpaceKeepsAStemFromMatchingInsideAnotherWord()
    {
        var evalCase = new EvalCase { RelevantTerms = [[" отит"]] };

        Assert.True(CaseScorer.IsRelevant(evalCase, new TracedChunk { Text = "Остър отит при деца" }));
        Assert.False(CaseScorer.IsRelevant(evalCase, new TracedChunk { Text = "Епидемичен паротит при деца" }));
    }

    [Fact]
    public void IsRelevant_WithoutTermsIsFalse()
    {
        Assert.False(CaseScorer.IsRelevant(new EvalCase(), new TracedChunk { Text = "x" }));
    }

    [Fact]
    public void ScoreRetrieval_PerfectRanking()
    {
        var score = CaseScorer.ScoreRetrieval([true, true, false], [true, true]);

        Assert.True(score.HitAt1);
        Assert.True(score.HitAtK);
        Assert.Equal(1, score.ReciprocalRank);
        Assert.Equal(1, score.Ndcg, 6);
        Assert.Equal(1, score.ExcerptPrecision);
    }

    [Fact]
    public void ScoreRetrieval_LateHit()
    {
        var score = CaseScorer.ScoreRetrieval([false, false, true], [false, true]);

        Assert.False(score.HitAt1);
        Assert.True(score.HitAtK);
        Assert.Equal(1.0 / 3, score.ReciprocalRank, 6);
        Assert.Equal(0.5, score.Ndcg, 6); // 1/log2(4) over an ideal of 1/log2(2)
        Assert.Equal(0.5, score.ExcerptPrecision);
    }

    [Fact]
    public void ScoreRetrieval_HitBeyondTheDepthDoesNotCount()
    {
        var score = CaseScorer.ScoreRetrieval([false, false, false, false, false, true], []);

        Assert.False(score.HitAtK);
        Assert.Equal(1.0 / 6, score.ReciprocalRank, 6);
        Assert.Equal(0, score.Ndcg);
    }

    [Fact]
    public void ScoreRetrieval_NothingRanked()
    {
        var score = CaseScorer.ScoreRetrieval([], []);

        Assert.False(score.HitAtK);
        Assert.Equal(0, score.ReciprocalRank);
        Assert.Equal(0, score.ExcerptPrecision);
    }

    [Theory]
    [InlineData("The indexed books do not cover the long-term prognosis.")]
    [InlineData("The indexed books do not provide detailed information on treatment beyond these points [2].")]
    [InlineData("Индексираните книги не обхващат лечението на заболяването.")]
    [InlineData("Откъсите не описват прогнозата при деца.")]
    public void ExtractClaims_SkipsSentencesSayingTheBooksDoNotCoverSomething(string gap)
    {
        var claims = CaseScorer.ExtractClaims($"Kawasaki disease is a systemic vasculitis [1]. {gap}");

        Assert.Equal(["Kawasaki disease is a systemic vasculitis ."], claims.Select(c => c.Text));
    }

    [Fact]
    public void ExtractClaims_KeepsClinicalSentencesThatMentionTheBooks()
    {
        var claims = CaseScorer.ExtractClaims("The books describe aspirin as first-line treatment [1].");

        Assert.Single(claims);
    }

    [Fact]
    public void ExtractClaims_SplitsSentencesAndKeepsTheirCitations()
    {
        var claims = CaseScorer.ExtractClaims(
            "Болестта на Кавазаки е остър системен васкулит [1]. Лечението е с имуноглобулин и аспирин [2, 3]. Да.");

        Assert.Equal(2, claims.Count);
        Assert.Equal("Болестта на Кавазаки е остър системен васкулит.", claims[0].Text.Replace(" .", ".", StringComparison.Ordinal));
        Assert.Equal([1], claims[0].CitedExcerpts);
        Assert.Equal([2, 3], claims[1].CitedExcerpts);
    }

    [Fact]
    public void ExtractClaims_MarkerAfterThePeriodBelongsToThePrecedingSentence()
    {
        var claims = CaseScorer.ExtractClaims(
            "Болестта на Кавазаки е остър системен васкулит. [1] Лечението е с имуноглобулин и аспирин. [2][3] Край.");

        Assert.Equal([1], claims[0].CitedExcerpts);
        Assert.Equal([2, 3], claims[1].CitedExcerpts);
    }

    [Fact]
    public void Decide_UnconfirmedAbstentionIsInconclusiveNotAPass()
    {
        var result = CaseScorer.Decide(new EvalCaseResult { Expectation = EvalExpectation.Abstain, Outcome = RagOutcome.Answered });

        Assert.True(result.Inconclusive);
        Assert.False(result.ConfirmedPass);
    }

    [Fact]
    public void Decide_JudgeErrorIsInconclusive_ButARealFailureStaysAFailure()
    {
        Assert.True(CaseScorer.Decide(Good() with { JudgeError = "bad" }).Inconclusive);

        var broken = CaseScorer.Decide(Good() with { JudgeError = "bad", Format = new FormatScore() });

        Assert.False(broken.Passed);
        Assert.False(broken.Inconclusive);
    }

    [Fact]
    public void Decide_CleanAnswerPasses()
    {
        var result = CaseScorer.Decide(Good());

        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
    }

    [Fact]
    public void Decide_PipelineErrorFails()
    {
        var result = CaseScorer.Decide(Good() with { Error = "ollama down" });

        Assert.False(result.Passed);
        Assert.Contains("ollama down", result.FailureReasons[0]);
    }

    [Fact]
    public void Decide_NoBookAnswerFails()
    {
        Assert.False(CaseScorer.Decide(Good() with { Outcome = RagOutcome.WebFallback }).Passed);
    }

    [Fact]
    public void Decide_UnsafeClaimFailsEvenWhenEverythingElseIsFine()
    {
        var result = CaseScorer.Decide(Good() with { ForbiddenStated = ["Антибиотиците са рутинни."] });

        Assert.False(result.Passed);
        Assert.Contains(result.FailureReasons, r => r.Contains("Unsafe claim", StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_LowFactRecallFails()
    {
        var facts = new[]
        {
            new FactCheck { Fact = "a", InAnswer = true },
            new FactCheck { Fact = "b", InAnswer = false },
            new FactCheck { Fact = "c", InAnswer = false },
        };

        Assert.False(CaseScorer.Decide(Good() with { Facts = facts }).Passed);
    }

    [Fact]
    public void Decide_UnjudgedSentencesCountNeitherWay()
    {
        var result = Good() with
        {
            Claims =
            [
                new ClaimCheck { Supported = true },
                new ClaimCheck { JudgeError = "broken" },
                new ClaimCheck { JudgeError = "broken" },
            ],
        };

        Assert.True(CaseScorer.Decide(result).Passed);
    }

    [Fact]
    public void Summarize_LeavesUnjudgedSentencesOutOfTheRatesAndCountsThem()
    {
        var result = Good() with
        {
            Judged = true,
            Claims =
            [
                new ClaimCheck { Supported = true, CitationOk = true },
                new ClaimCheck { JudgeError = "broken" },
            ],
        };

        var summary = RunSummarizer.Summarize([result], 8_192);

        Assert.Equal(1.0, summary.Faithfulness);
        Assert.Equal(1.0, summary.CitationAccuracy);
        Assert.Equal(1, summary.UnjudgedClaims);
    }

    [Fact]
    public void Decide_LowFaithfulnessFails()
    {
        var claims = new[]
        {
            new ClaimCheck { Claim = "a", Supported = true },
            new ClaimCheck { Claim = "b", Supported = false },
        };

        Assert.False(CaseScorer.Decide(Good() with { Claims = claims }).Passed);
    }

    [Fact]
    public void Decide_UnjudgedFactsDoNotFail()
    {
        var facts = new[] { new FactCheck { Fact = "a" } };

        Assert.True(CaseScorer.Decide(Good() with { Facts = facts }).Passed);
    }

    [Theory]
    [InlineData(RagOutcome.BelowAnswerFloor, null, true)]
    [InlineData(RagOutcome.WebFallback, null, true)]
    [InlineData(RagOutcome.Answered, true, true)]
    [InlineData(RagOutcome.Answered, false, false)]
    public void Decide_AbstainCases(RagOutcome outcome, bool? abstained, bool expected)
    {
        var result = new EvalCaseResult { Expectation = EvalExpectation.Abstain, Outcome = outcome, Abstained = abstained };

        Assert.Equal(expected, CaseScorer.Decide(result).Passed);
    }
}
