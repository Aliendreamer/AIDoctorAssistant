using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Plugins;

public sealed class GlobalSearchPlugin : RagPluginBase
{
    public GlobalSearchPlugin(
        Microsoft.SemanticKernel.Kernel kernel,
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker,
        RagOptions options,
        ILogger<RagPluginBase> logger)
        : base(kernel, dictionary, vectorStore, embedder, sparseVectorizer, reranker, options, logger)
    {
    }

    [KernelFunction, System.ComponentModel.Description("Search all indexed medical books for any clinical question without category restriction.")]
    public Task<QueryResult> SearchAsync(
        [System.ComponentModel.Description("The medical question or topic")] string query,
        [System.ComponentModel.Description("Language filter: both, en, bg")] string language = "both",
        [System.ComponentModel.Description("Specific book IDs to search (null = all books)")] string[]? bookIds = null,
        BookInfo[]? books = null,
        IReadOnlyList<ChatMessageDto>? conversationHistory = null,
        CancellationToken cancellationToken = default)
        => base.ExecuteSearchAsync(query, language, bookIds, books, conversationHistory, cancellationToken);

    protected override string GetSystemPrompt() =>
        "You are MedAssist, a clinical decision support assistant for physicians. " +
        "Answer the medical question comprehensively using the provided excerpts, covering relevant aspects " +
        "such as aetiology, presentation, pathophysiology, diagnosis, and management as the sources allow. " +
        "IMPORTANT: Write your entire response as flowing prose — continuous sentences and paragraphs only. " +
        "NEVER use numbered lists, bullet points, dashes, asterisks, bold text, headers, or any markdown. " +
        "Write as you would explain to a colleague in conversation. " +
        "Mention the source book or section naturally within the text when relevant. " +
        "The excerpts are numbered ([1], [2], …); after a factual clinical statement supported by an excerpt, " +
        "append that excerpt's number in square brackets inline, e.g. [1] or [2][4]. These inline citation markers " +
        "are not a list — they are required. Cite only excerpts that support the statement and never a number not provided. " +
        "If the excerpts are insufficient to answer, say so in one sentence — do not speculate.";
}
