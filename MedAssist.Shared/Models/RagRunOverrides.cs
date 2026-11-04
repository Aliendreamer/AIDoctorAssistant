namespace MedAssist.Shared.Models;

/// <summary>
/// Per-run replacements for the pipeline's model and context settings. A <c>null</c> value keeps the
/// production default, so one evaluation run can try a different model or context shape without a
/// redeploy and without touching live queries.
/// </summary>
public sealed record RagRunOverrides
{
    /// <summary>Ollama model for the query-preparation and answer calls.</summary>
    public string? ModelId { get; init; }

    /// <summary><c>num_ctx</c> sent to Ollama.</summary>
    public int? ContextWindowTokens { get; init; }

    /// <summary>Tokens kept free in the context window for the generated answer.</summary>
    public int? AnswerReserveTokens { get; init; }

    /// <summary>Most excerpts offered to the answer prompt, before the token budget is applied.</summary>
    public int? MaxExcerpts { get; init; }

    /// <summary><c>topK</c> of the first retrieval pass.</summary>
    public int? InitialTopK { get; init; }

    /// <summary>Character budget for conversation history re-sent to the answer model.</summary>
    public int? HistoryCharBudget { get; init; }

    /// <summary>Sampling temperature of the answer call.</summary>
    public double? Temperature { get; init; }

    /// <summary>Skips every web search, so results depend only on the books and the pipeline.</summary>
    public bool BookOnly { get; init; }
}
