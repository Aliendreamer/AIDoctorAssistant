using System.Text.Json.Serialization;

namespace MedAssist.Shared.Models.Evaluation;

public sealed record FormatScore
{
    public int Markers { get; init; }
    public bool HasMarkers { get; init; }
    public bool MarkersInRange { get; init; }
    public bool LanguageOk { get; init; }
    public double CyrillicShare { get; init; }
    public bool Leakage { get; init; }
}

public sealed record RetrievalScore
{
    public bool HitAt1 { get; init; }
    public bool HitAtK { get; init; }
    public double ReciprocalRank { get; init; }
    public double Ndcg { get; init; }

    /// <summary>Share of the excerpts sent to the model that are relevant.</summary>
    public double ExcerptPrecision { get; init; }
}

/// <summary>A reference fact, checked separately against the excerpts and against the answer.</summary>
public sealed record FactCheck
{
    public string Fact { get; init; } = string.Empty;
    public bool? InExcerpts { get; init; }
    public bool? InAnswer { get; init; }
}

public sealed record ClaimCheck
{
    public string Claim { get; init; } = string.Empty;
    public IReadOnlyList<int> CitedExcerpts { get; init; } = [];
    public bool Supported { get; init; }

    /// <summary>Whether the cited excerpts back the sentence; <c>null</c> when it cites nothing.</summary>
    public bool? CitationOk { get; init; }

    /// <summary>The excerpt sentences the verdict rests on.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>The parts of the sentence that evidence does not state.</summary>
    public IReadOnlyList<string> UnstatedFacts { get; init; } = [];

    /// <summary>The first quotes fell short, and the sentence passed on quotes from its cited excerpts.</summary>
    public bool Rechecked { get; init; }

    /// <summary>Set when the judge could not check this sentence, which then counts neither way.</summary>
    public string? JudgeError { get; init; }
}

public sealed record EvalExcerpt
{
    public int Number { get; init; }
    public string Source { get; init; } = string.Empty;

    /// <summary>The section heading the answer model saw; often the only place the disease is named.</summary>
    public string Section { get; init; } = string.Empty;

    public bool Relevant { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed record EvalRankedChunk
{
    public string Source { get; init; } = string.Empty;
    public float Score { get; init; }
    public bool Relevant { get; init; }
}

/// <summary>Everything recorded for one question in one run; stored as JSON on the result row.</summary>
public sealed record EvalCaseResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public QueryType QueryType { get; init; }
    public string Question { get; init; } = string.Empty;
    public EvalExpectation Expectation { get; init; }

    public string Answer { get; init; } = string.Empty;
    public string? Error { get; init; }
    public RagOutcome Outcome { get; init; }
    public string? SearchQuery { get; init; }
    public double LatencySeconds { get; init; }

    public long PromptTokens { get; init; }
    public bool PromptTokensEstimated { get; init; }
    public long CompletionTokens { get; init; }
    public int ExcerptsDropped { get; init; }

    public IReadOnlyList<EvalExcerpt> Excerpts { get; init; } = [];
    public IReadOnlyList<EvalRankedChunk> Ranked { get; init; } = [];

    public FormatScore Format { get; init; } = new();

    /// <summary><c>null</c> for questions the system should abstain from.</summary>
    public RetrievalScore? Retrieval { get; init; }

    public IReadOnlyList<FactCheck> Facts { get; init; } = [];
    public IReadOnlyList<string> ForbiddenStated { get; init; } = [];
    public bool? Abstained { get; init; }
    public IReadOnlyList<ClaimCheck> Claims { get; init; } = [];

    /// <summary>True once the judge finished this question.</summary>
    public bool Judged { get; init; }

    public string? JudgeError { get; init; }

    /// <summary>No check failed. See <see cref="Inconclusive"/> before reading this as a real pass.</summary>
    public bool Passed { get; init; }

    public IReadOnlyList<string> FailureReasons { get; init; } = [];

    /// <summary>
    /// Nothing failed, but a check that decides this question never ran, so it counts as neither a pass
    /// nor a failure: a dead judge must not look like success, and a flaky one must not look like a regression.
    /// </summary>
    [JsonIgnore]
    public bool Inconclusive => Passed && (JudgeError is not null || AbstentionUnknown);

    private bool AbstentionUnknown =>
        Expectation == EvalExpectation.Abstain && Outcome == RagOutcome.Answered && Abstained is null;

    /// <summary>A pass backed by every check.</summary>
    [JsonIgnore]
    public bool ConfirmedPass => Passed && !Inconclusive;
}
