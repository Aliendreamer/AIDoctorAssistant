using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Ambient per-question context for evaluation runs. While a scope is active on the current async flow
/// the pipeline honours its <see cref="Overrides"/> and records into its <see cref="Trace"/>. Live
/// queries have no scope, so they keep the production settings and record nothing.
/// </summary>
public sealed class RagRunScope : IDisposable
{
    private static readonly AsyncLocal<RagRunScope?> _current = new();

    private readonly RagRunScope? _previous;

    private RagRunScope(RagRunOverrides overrides)
    {
        Overrides = overrides;
        _previous = _current.Value;
        _current.Value = this;
    }

    public static RagRunScope? Current => _current.Value;

    public RagRunOverrides Overrides { get; }

    public RagTrace Trace { get; } = new();

    public static RagRunScope Begin(RagRunOverrides overrides) => new(overrides);

    public static int ContextWindowTokens =>
        Current?.Overrides.ContextWindowTokens ?? PromptConstants.ContextWindowTokens;

    public static int HistoryCharBudget =>
        Current?.Overrides.HistoryCharBudget ?? PromptConstants.HistoryCharBudget;

    public static int MaxExcerpts => Current?.Overrides.MaxExcerpts ?? PromptConstants.MaxExcerpts;

    public static int InitialTopK => Current?.Overrides.InitialTopK ?? PromptConstants.InitialTopK;

    /// <summary>Token budget for the answer prompt: the context window minus the answer reserve.</summary>
    public static int PromptBudgetTokens =>
        ContextWindowTokens - (Current?.Overrides.AnswerReserveTokens ?? PromptConstants.AnswerReserveTokens);

    public void Dispose() => _current.Value = _previous;
}
