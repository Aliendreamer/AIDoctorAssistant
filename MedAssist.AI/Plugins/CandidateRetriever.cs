using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Retrieves candidate chunks for the RAG search loop and section-expands them. Extracted from
/// <see cref="RagPluginBase"/> (audit P2-16) to isolate the hybrid dense+sparse retrieval from the
/// search-orchestration/retry algorithm that drives it.
/// </summary>
internal sealed class CandidateRetriever(
    IEmbedder embedder,
    ISparseVectorizer sparseVectorizer,
    IVectorStore vectorStore)
{
    /// <summary>Dense + sparse search for each term, concatenating the hits.</summary>
    public async Task<List<MedicalChunk>> GatherAsync(
        IReadOnlyList<string> terms,
        LanguageFilter langFilter,
        string[]? bookIds,
        int topK,
        CancellationToken cancellationToken)
    {
        var chunks = new List<MedicalChunk>();
        foreach (var term in terms)
        {
            var denseVector = await embedder.EmbedQueryAsync(term, cancellationToken);
            var sparseVector = await sparseVectorizer.VectorizeQueryAsync(term, cancellationToken);
            var results = await vectorStore.SearchAsync(denseVector, sparseVector, langFilter, bookIds, topK, cancellationToken);
            chunks.AddRange(results);
        }

        return chunks;
    }

    /// <summary>
    /// For each unique (chapter, section, bookId) among the summary candidates — which act as
    /// retrieval triggers — pull all regular chunks from that section and merge them in, deduped by
    /// <c>BookId:ChunkIndex</c>. Summaries stay in the pool until the caller filters them from the
    /// final answer.
    /// </summary>
    public async Task<List<MedicalChunk>> ExpandBySectionAsync(
        List<MedicalChunk> candidates,
        CancellationToken cancellationToken)
    {
        var sections = candidates
            .Where(c => c.IsSummary && !string.IsNullOrEmpty(c.SectionTitle))
            .Select(c => (c.ChapterTitle, c.SectionTitle, c.BookId))
            .Distinct()
            .ToList();

        if (sections.Count == 0)
        {
            return candidates;
        }

        var expanded = new List<MedicalChunk>(candidates);
        foreach (var (chapter, section, bookId) in sections)
        {
            var sectionChunks = await vectorStore.ScrollSectionAsync(chapter, section, bookId, limit: 50, cancellationToken);
            expanded.AddRange(sectionChunks);
        }

        return expanded
            .DistinctBy(c => (c.BookId, c.ChunkIndex))
            .ToList();
    }
}
