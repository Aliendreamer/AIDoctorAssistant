using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Plugins;

public sealed class TreatmentPlugin : RagPluginBase
{
    public TreatmentPlugin(
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

    [KernelFunction, System.ComponentModel.Description("Find treatment options for a given condition.")]
    public Task<QueryResult> SearchAsync(
        [System.ComponentModel.Description("The condition to find treatments for")] string query,
        [System.ComponentModel.Description("Language filter: both, en, bg")] string language = "both",
        [System.ComponentModel.Description("Specific book IDs to search (null = all books)")] string[]? bookIds = null,
        BookInfo[]? books = null,
        IReadOnlyList<ChatMessageDto>? conversationHistory = null,
        CancellationToken cancellationToken = default)
        => base.ExecuteSearchAsync(query, language, bookIds, books, conversationHistory, cancellationToken);
}
