namespace MedAssist.Shared.Constants;

public static class PromptConstants
{
    /// <summary>
    /// Character budget for conversation history re-sent to the answer model, on top of the
    /// message-count window. Oldest user/assistant pairs are dropped first; the latest pair is always
    /// kept (change streamline-rag-prompts).
    /// </summary>
    public const int HistoryCharBudget = 6_000;

    /// <summary>
    /// Context window (<c>num_ctx</c>) requested from Ollama on every chat call. Ollama's 4096 default
    /// was too small: RAG prompts reached 4095 tokens, got their start (system prompt + language line)
    /// clipped, and then lost half their context to a mid-answer context shift.
    /// </summary>
    public const int ContextWindowTokens = 8_192;

    /// <summary>Output cap for the query-preparation call — a one-line search query, never reasoning.</summary>
    public const int QueryPrepMaxTokens = 96;

    /// <summary>
    /// Tokens kept free in the context window for the generated answer. Golden run 3 answers decoded
    /// 360–1,100 tokens, with one at 2,116.
    /// </summary>
    public const int AnswerReserveTokens = 2_048;

    /// <summary>
    /// Conservative chars-per-token estimate for budgeting the answer prompt. Measured with the qwen3
    /// tokenizer: Bulgarian 2.1–2.3, English ~4.6 — prompts are mostly Bulgarian excerpts, so 2 slightly
    /// over-counts (the safe direction). 3 under-counted Cyrillic by ~40% and prompts overflowed.
    /// </summary>
    public const int EstimatedCharsPerToken = 2;
}
