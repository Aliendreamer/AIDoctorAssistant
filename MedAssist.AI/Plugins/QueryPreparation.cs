using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Turns the user's question into a Bulgarian search query — the corpus language — so retrieval and
/// reranking work for English questions and follow-ups (change tune-query-pipeline). A Bulgarian first
/// question is used as-is; anything else costs one short LLM call.
/// </summary>
public static class QueryPreparation
{
    private const int _followUpMaxWords = 20;

    /// <summary>
    /// Fast-call settings: thinking off (qwen3 ignored <c>/no_think</c> here and emitted ~1,000 tokens),
    /// a small output cap, deterministic output, and the answer calls' <c>num_ctx</c> — a different value
    /// would make Ollama reload the model between calls.
    /// </summary>
    public static PromptExecutionSettings Settings { get; } = new()
    {
        ExtensionData = new Dictionary<string, object>
        {
            ["num_ctx"] = PromptConstants.ContextWindowTokens,
            ["think"] = false,
            ["num_predict"] = PromptConstants.QueryPrepMaxTokens,
            ["temperature"] = 0.0,
        },
    };

    public static bool NeedsPreparation(string query, IReadOnlyList<ChatMessageDto>? history) =>
        !IsCyrillic(query) || PreviousQuestion(query, history) is not null;

    public static ChatHistory BuildPrompt(string query, IReadOnlyList<ChatMessageDto>? history)
    {
        var chat = new ChatHistory();
        chat.AddSystemMessage(
            "You are a medical search query optimizer. Rewrite the question as a concise, self-contained " +
            "medical search query in Bulgarian. If a previous question is given, use it to make a follow-up " +
            "self-contained. Keep eponyms, drug names and ICD codes exactly as written. " +
            "Output ONLY the query — no explanation, no quotes.");

        var previous = PreviousQuestion(query, history);
        chat.AddUserMessage(previous is null
            ? $"Question: {query}\n\nBulgarian search query:\n\n/no_think"
            : $"Previous question: {previous}\n\nFollow-up: {query}\n\nBulgarian search query:\n\n/no_think");
        return chat;
    }

    /// <summary>
    /// Scores Bulgarian chunks against the Bulgarian search query and other-language chunks against the
    /// original question, then merges by score (same model, comparable scale).
    /// </summary>
    public static async Task<IReadOnlyList<ScoredChunk>> RerankByLanguageAsync(
        ICrossEncoderReranker reranker,
        string searchQuery,
        string originalQuery,
        IReadOnlyList<MedicalChunk> candidates,
        CancellationToken cancellationToken)
    {
        var scored = new List<ScoredChunk>(candidates.Count);
        foreach (var group in candidates.GroupBy(c => c.Language == LanguageCodes.Bulgarian))
        {
            var query = group.Key ? searchQuery : originalQuery;
            scored.AddRange(await reranker.RerankAsync(query, group.ToList(), cancellationToken));
        }

        return scored.OrderByDescending(s => s.Score).ToList();
    }

    // Purely-Latin alphabetic words (≥5 chars). Run on the Bulgarian search query, these are the terms
    // that survive translation — medical eponyms (Chiari, Wilson) and drug names — which should appear
    // verbatim in relevant chunks; generic English words are gone by then.
    public static IReadOnlyList<string> ExtractLatinTerms(string query) =>
        query
            .Split([' ', ',', '.', '!', '?', '(', ')', ':', ';', '\t', '\n', '/', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 5 && w.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

    private static bool IsCyrillic(string text) => text.Any(c => c is >= 'Ѐ' and <= 'ӿ');

    // A short question that differs from the last one asked, in a conversation, is treated as a follow-up.
    private static string? PreviousQuestion(string query, IReadOnlyList<ChatMessageDto>? history)
    {
        if (history is not { Count: >= 2 }
            || query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= _followUpMaxWords)
        {
            return null;
        }

        var lastUser = history.LastOrDefault(m => m.Role == "user")?.Content;
        return lastUser is null || lastUser == query ? null : lastUser;
    }
}
