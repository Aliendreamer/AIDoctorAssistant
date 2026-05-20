using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Plugins;

public sealed class DiseasePlugin : RagPluginBase
{
    public DiseasePlugin(
        Microsoft.SemanticKernel.Kernel kernel,
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker,
        RagOptions options)
        : base(kernel, dictionary, vectorStore, embedder, sparseVectorizer, reranker, options)
    {
    }

    [KernelFunction, System.ComponentModel.Description("Retrieve clinical information about a disease or condition.")]
    public Task<QueryResult> SearchAsync(
        [System.ComponentModel.Description("The disease or condition name")] string query,
        [System.ComponentModel.Description("Language filter: both, en, bg")] string language = "both",
        [System.ComponentModel.Description("Specific book IDs to search (null = all books)")] string[]? bookIds = null,
        BookInfo[]? books = null,
        CancellationToken cancellationToken = default)
        => base.ExecuteSearchAsync(query, language, bookIds, books, cancellationToken);
}
