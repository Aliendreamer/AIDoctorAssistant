using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>Rejects settings that could only produce a meaningless run.</summary>
public static class EvalFormRules
{
    private const int _minContextWindow = 1_024;

    /// <summary>The problem with these overrides, or <c>null</c> when they are usable.</summary>
    public static string? Validate(RagRunOverrides overrides)
    {
        var window = overrides.ContextWindowTokens ?? PromptConstants.ContextWindowTokens;
        var reserve = overrides.AnswerReserveTokens ?? PromptConstants.AnswerReserveTokens;

        if (window < _minContextWindow)
        {
            return $"The context window must be at least {_minContextWindow} tokens.";
        }

        if (reserve <= 0 || reserve >= window)
        {
            return "The answer reserve must be above zero and smaller than the context window.";
        }

        if (overrides.MaxExcerpts is < 1 || overrides.InitialTopK is < 1)
        {
            return "Max excerpts and retrieval top-K must be at least 1.";
        }

        if (overrides.HistoryCharBudget is < 0)
        {
            return "The history budget cannot be negative.";
        }

        return overrides.Temperature is < 0 or > 2 ? "Temperature must be between 0 and 2." : null;
    }
}
