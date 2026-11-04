namespace MedAssist.Shared.Models.Evaluation;

public enum EvalExpectation
{
    /// <summary>The books cover this; the system should answer from them.</summary>
    Answer,

    /// <summary>The books cannot cover this; the system should say so instead of inventing an answer.</summary>
    Abstain,
}

/// <summary>One golden question and the reference it is scored against.</summary>
public sealed record EvalCase
{
    public string Id { get; init; } = string.Empty;

    /// <summary><c>BG</c> or <c>EN</c>: the language the answer must be in.</summary>
    public string Language { get; init; } = string.Empty;

    public QueryType QueryType { get; init; }
    public string Question { get; init; } = string.Empty;
    public EvalExpectation Expectation { get; init; }

    /// <summary>Id of the case whose conversation this one continues.</summary>
    public string? FollowUpOf { get; init; }

    /// <summary>True once a physician has checked the reference facts against the books.</summary>
    public bool Reviewed { get; init; }

    /// <summary>
    /// A chunk is relevant when, for every group, it contains at least one of the group's lowercase
    /// stems. Stems stay Bulgarian even for English questions because the corpus is Bulgarian.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> RelevantTerms { get; init; } = [];

    public IReadOnlyList<string> RequiredFacts { get; init; } = [];

    /// <summary>Claims a safe answer must not make.</summary>
    public IReadOnlyList<string> ForbiddenClaims { get; init; } = [];
}

/// <summary>The golden questions plus a content hash, so runs on different references are not compared blindly.</summary>
public sealed record GoldenSet(IReadOnlyList<EvalCase> Cases, string Hash)
{
    public int ReviewedCount => Cases.Count(c => c.Reviewed);
}
