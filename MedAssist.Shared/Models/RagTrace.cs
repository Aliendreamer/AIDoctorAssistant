namespace MedAssist.Shared.Models;

/// <summary>How the RAG pipeline ended for one question.</summary>
public enum RagOutcome
{
    /// <summary>The pipeline did not report an outcome (it failed before finishing).</summary>
    Unknown,
    Answered,
    NoCandidates,
    WebFallback,
    BelowAnswerFloor,
    LatinTermGuard,
    ConditionNotInExcerpts,
}

/// <summary>One retrieved chunk as the pipeline ranked it.</summary>
public sealed record TracedChunk
{
    public string BookTitle { get; init; } = string.Empty;
    public string ChapterTitle { get; init; } = string.Empty;
    public string SectionTitle { get; init; } = string.Empty;
    public int PageStart { get; init; }
    public int PageEnd { get; init; }
    public float Score { get; init; }
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// What one question did inside the pipeline. Filled in by the pipeline when a run scope is active, and
/// kept off <see cref="QueryResult"/> so retrieved text never reaches the public API.
/// </summary>
public sealed class RagTrace
{
    public RagOutcome Outcome { get; set; }
    public string? SearchQuery { get; set; }

    /// <summary>Non-summary chunks in final rerank order, before the excerpt cap and token budget.</summary>
    public IReadOnlyList<TracedChunk> Ranked { get; set; } = [];

    /// <summary>The excerpts actually placed in the answer prompt, in citation order.</summary>
    public IReadOnlyList<TracedChunk> Excerpts { get; set; } = [];

    /// <summary>Excerpts removed to fit the token budget.</summary>
    public int ExcerptsDropped { get; set; }

    public long PromptTokens { get; set; }
    public bool PromptTokensEstimated { get; set; }
    public long CompletionTokens { get; set; }

    /// <summary>The exception message when <c>QueryService</c> turned a failure into a generic answer.</summary>
    public string? Error { get; set; }
}
