using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Plugins;

public sealed class TreatmentPlugin : RagPluginBase
{
    public TreatmentPlugin(IMedicalDictionary dictionary, IVectorStore vectorStore, IEmbedder embedder, ISparseVectorizer sparseVectorizer, ICrossEncoderReranker reranker)
        : base(dictionary, vectorStore, embedder, sparseVectorizer, reranker)
    {
    }

    [KernelFunction, System.ComponentModel.Description("Find treatment options for a given condition.")]
    public Task<QueryResult> SearchAsync(
        [System.ComponentModel.Description("The condition to find treatments for")] string query,
        [System.ComponentModel.Description("Language filter: both, en, bg")] string language = "both",
        [System.ComponentModel.Description("Specific book IDs to search (null = all books)")] string[]? bookIds = null,
        CancellationToken cancellationToken = default)
        => base.ExecuteSearchAsync(query, language, bookIds, cancellationToken);
}
