using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Prompt-token cost per answer-generation call (change streamline-rag-prompts). Uses the model-reported
/// input token count when the connector surfaces it, otherwise a chars/4 estimate tagged <c>estimated=true</c>.
/// </summary>
public static class PromptMetrics
{
    private static readonly Meter _meter = new("MedAssist.AI");

    private static readonly Histogram<long> _promptTokens = _meter.CreateHistogram<long>(
        "rag_prompt_tokens", unit: "{token}", description: "Prompt tokens sent per answer-generation call");

    public static void Record(string pluginType, ChatHistory prompt, ChatMessageContent response)
    {
        var (tokens, estimated) = PromptTokens(prompt, response);
        _promptTokens.Record(tokens,
            new KeyValuePair<string, object?>("plugin_type", pluginType),
            new KeyValuePair<string, object?>("estimated", estimated));
    }

    // SK's IChatClient adapter stores Microsoft.Extensions.AI UsageDetails under Metadata["Usage"];
    // OllamaSharp fills InputTokenCount from Ollama's prompt_eval_count.
    internal static (long Tokens, bool Estimated) PromptTokens(ChatHistory prompt, ChatMessageContent response)
    {
        if (response.Metadata?.TryGetValue("Usage", out var usage) == true
            && usage is UsageDetails { InputTokenCount: > 0 and var reported })
        {
            return (reported, false);
        }

        return (prompt.Sum(m => (long)(m.Content?.Length ?? 0)) / 4, true);
    }
}
