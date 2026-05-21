using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Plugins;

public sealed class DifferentialDiagnosisPlugin : RagPluginBase
{
    public DifferentialDiagnosisPlugin(
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

    [KernelFunction, System.ComponentModel.Description("Generate a differential diagnosis from a clinical presentation or symptom cluster.")]
    public Task<QueryResult> SearchAsync(
        [System.ComponentModel.Description("Clinical presentation or symptom cluster to differentiate")] string query,
        [System.ComponentModel.Description("Language filter: both, en, bg")] string language = "both",
        [System.ComponentModel.Description("Specific book IDs to search (null = all books)")] string[]? bookIds = null,
        BookInfo[]? books = null,
        IReadOnlyList<ChatMessageDto>? conversationHistory = null,
        CancellationToken cancellationToken = default)
        => base.ExecuteSearchAsync(query, language, bookIds, books, conversationHistory, cancellationToken);

    protected override string GetSystemPrompt() =>
        "You are MedAssist, a clinical decision support assistant for physicians. " +
        "Given the clinical presentation described, construct a differential diagnosis. " +
        "Present the diagnoses from most to least likely, explaining the key clinical features " +
        "and findings from the source books that support or argue against each. " +
        "IMPORTANT: Write your entire response as flowing prose — continuous sentences and paragraphs only. " +
        "NEVER use numbered lists, bullet points, dashes, asterisks, bold text, headers, or any markdown. " +
        "Write as you would reason aloud with a colleague during a ward round. " +
        "Mention the source book or section naturally within the text when relevant. " +
        "If the excerpts are insufficient to differentiate, say so in one sentence — do not speculate.";
}
