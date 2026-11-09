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
    /// <summary>Dense + sparse search for each term, concatenating the hits. Reference lists and indexes are left out.</summary>
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
            chunks.AddRange(results.Where(Admit));
        }

        return chunks;
    }

    /// <summary>The spellings the books use for the named conditions; see <see cref="ConditionNameGuard"/>.</summary>
    public async Task<IReadOnlyList<string>> FindBookSpellingsAsync(
        IReadOnlyList<IReadOnlyList<string>> names,
        CancellationToken cancellationToken)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var words = await sparseVectorizer.GetWordWeightsAsync(cancellationToken);
        return ConditionNameGuard.FindBookSpellings(names, words.Keys);
    }

    /// <summary>The books' Bulgarian terms for an English question; see <see cref="BookTermLookup"/>.</summary>
    public async Task<IReadOnlyList<string>> FindBookTermsAsync(
        string question,
        IReadOnlyList<IReadOnlyList<string>> names,
        CancellationToken cancellationToken)
    {
        if (!BookTermLookup.Applies(question))
        {
            return [];
        }

        var words = await sparseVectorizer.GetWordWeightsAsync(cancellationToken);
        return BookTermLookup.Find(question, names, words);
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
            expanded.AddRange(sectionChunks.Where(Admit));
        }

        return expanded
            .DistinctBy(c => (c.BookId, c.ChunkIndex))
            .ToList();
    }

    /// <summary>Keeps reference lists and indexes out, unless the evaluation run asked to keep them.</summary>
    private static bool Admit(MedicalChunk chunk) =>
        RagRunScope.Current?.Overrides.KeepReferenceMatter == true || !ReferenceMatter.IsReferenceMatter(chunk.Text);
}
