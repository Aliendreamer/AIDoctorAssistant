namespace MedAssist.Shared.Models.Evaluation;

/// <summary>Run-level metrics. Judged values are <c>null</c> when nothing was judged.</summary>
public sealed record EvalRunSummary
{
    public int Cases { get; init; }
    public int Passed { get; init; }

    /// <summary>Questions a missing check left undecided; they are outside <see cref="PassRate"/>.</summary>
    public int Inconclusive { get; init; }

    public int Errors { get; init; }
    public int JudgeFailures { get; init; }
    public double PassRate { get; init; }

    public double? FactRecall { get; init; }
    public int? ForbiddenClaims { get; init; }
    public double AnswerRate { get; init; }
    public double? AbstainAccuracy { get; init; }

    public double? Faithfulness { get; init; }
    public double? CitationAccuracy { get; init; }

    public double HitAt1 { get; init; }
    public double HitAtK { get; init; }
    public double Mrr { get; init; }
    public double Ndcg { get; init; }
    public double ExcerptPrecision { get; init; }
    public double? ContextFactCoverage { get; init; }

    /// <summary>Reference facts never retrieved and missing from the answer.</summary>
    public int? FactsLostInRetrieval { get; init; }

    /// <summary>Reference facts retrieved but dropped by the model.</summary>
    public int? FactsLostInGeneration { get; init; }

    /// <summary>Reference facts in the answer but not the excerpts, so not from the books.</summary>
    public int? FactsNotFromExcerpts { get; init; }

    public double CitationRate { get; init; }
    public double MarkersInRange { get; init; }
    public double LanguageOk { get; init; }
    public double Leakage { get; init; }

    public double MeanLatencySeconds { get; init; }
    public double P95LatencySeconds { get; init; }
    public double MeanPromptTokens { get; init; }
    public long MaxPromptTokens { get; init; }

    /// <summary>Questions whose prompt reached the context window, so Ollama may have truncated it.</summary>
    public int ContextOverflows { get; init; }

    public int ExcerptsDropped { get; init; }
}
