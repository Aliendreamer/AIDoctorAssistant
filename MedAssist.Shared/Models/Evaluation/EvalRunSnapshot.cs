namespace MedAssist.Shared.Models.Evaluation;

/// <summary>What an admin chooses when starting a run.</summary>
public sealed record EvalRunRequest
{
    public string Note { get; init; } = string.Empty;
    public RagRunOverrides Overrides { get; init; } = new();
    public string? JudgeModel { get; init; }
    public bool RunJudge { get; init; } = true;
}

/// <summary>
/// The effective configuration of a run: chosen overrides resolved against production defaults, plus
/// whatever else can change results between runs. Diffing two snapshots shows what a comparison tests.
/// </summary>
public sealed record EvalRunSnapshot
{
    public string Model { get; init; } = string.Empty;
    public string? JudgeModel { get; init; }

    /// <summary>How answers were graded; empty on runs from before this was recorded.</summary>
    public string JudgeMethod { get; init; } = string.Empty;

    public int ContextWindowTokens { get; init; }
    public int AnswerReserveTokens { get; init; }
    public int MaxExcerpts { get; init; }
    public int InitialTopK { get; init; }
    public int HistoryCharBudget { get; init; }
    public double? Temperature { get; init; }
    public bool BookOnly { get; init; }

    public float ConfidenceThreshold { get; init; }
    public float MinAnswerScore { get; init; }
    public float MinRetryScore { get; init; }
    public int MaxIterations { get; init; }

    /// <summary>Hash of the composed prompts: changes whenever prompt text changes in code.</summary>
    public string PromptFingerprint { get; init; } = string.Empty;

    public string Embedder { get; init; } = string.Empty;
    public string Reranker { get; init; } = string.Empty;
    public int IndexedBooks { get; init; }
    public int IndexedChunks { get; init; }
    public string AppVersion { get; init; } = string.Empty;
    public string GoldenSetHash { get; init; } = string.Empty;
    public int ReviewedCases { get; init; }
    public int TotalCases { get; init; }

    /// <summary>Overrides to replay in the pipeline: null means keep the production value.</summary>
    public RagRunOverrides Overrides { get; init; } = new();

    public bool RunJudge { get; init; } = true;

    public IReadOnlyList<(string Label, string Value)> ToRows() =>
    [
        ("Answer model", Model),
        ("Judge model", RunJudge ? JudgeModel ?? Model : "off"),
        ("Judge method", RunJudge ? JudgeMethod : "off"),
        ("Context window", ContextWindowTokens.ToString()),
        ("Answer reserve", AnswerReserveTokens.ToString()),
        ("Max excerpts", MaxExcerpts.ToString()),
        ("Retrieval top-K", InitialTopK.ToString()),
        ("History budget (chars)", HistoryCharBudget.ToString()),
        ("Temperature", Temperature?.ToString("0.##") ?? "model default"),
        ("Web search", BookOnly ? "off" : "on"),
        ("Reference lists and indexes", Overrides.KeepReferenceMatter ? "kept" : "dropped"),
        ("Confidence threshold", ConfidenceThreshold.ToString("0.##")),
        ("Min answer score", MinAnswerScore.ToString("0.##")),
        ("Min retry score", MinRetryScore.ToString("0.##")),
        ("Max iterations", MaxIterations.ToString()),
        ("Prompt fingerprint", PromptFingerprint),
        ("Embedder", Embedder),
        ("Reranker", Reranker),
        ("Indexed books", IndexedBooks.ToString()),
        ("Indexed chunks", IndexedChunks.ToString()),
        ("App version", AppVersion),
        ("Golden set", GoldenSetHash),
    ];
}
