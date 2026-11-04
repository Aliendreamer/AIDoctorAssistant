namespace MedAssist.Shared.Constants;

public static class EvaluationConstants
{
    /// <summary>Temperature evaluation runs default to, so a re-run differs only by what was changed.</summary>
    public const double DefaultTemperature = 0;

    /// <summary>Ranks the retrieval metrics look at (the answer prompt's excerpt cap).</summary>
    public const int RetrievalDepth = PromptConstants.MaxExcerpts;

    /// <summary>Ranked chunks stored per question.</summary>
    public const int StoredRankedChunks = 10;

    /// <summary>A question fails below this share of its reference facts in the answer.</summary>
    public const double MinFactRecall = 0.5;

    /// <summary>A question fails below this share of answer sentences supported by the excerpts.</summary>
    public const double MinFaithfulness = 0.7;

    /// <summary>Extra judge context on top of the run's window: the judge also reads the answer.</summary>
    public const int JudgeContextHeadroomTokens = 4_096;

    public const int JudgeMaxOutputTokens = 1_024;

    /// <summary>Answer sentences checked for grounding, which bounds the judge's output.</summary>
    public const int MaxJudgedClaims = 15;

    /// <summary>Shorter fragments are connective text rather than claims.</summary>
    public const int MinClaimChars = 25;

    public const int JudgeAttempts = 2;

    /// <summary>A rate must move by more than this (just over one question in 18) to count as a change.</summary>
    public const double RateTolerance = 0.06;

    /// <summary>Judged rates get more room, because the judge itself adds noise.</summary>
    public const double JudgedRateTolerance = 0.08;

    /// <summary>Above this share of unjudged questions a run cannot rule out a regression.</summary>
    public const double MaxJudgeFailureShare = 0.1;

    public const string EvalUserPrefix = "eval:";
}
