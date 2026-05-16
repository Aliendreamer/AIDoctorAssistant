using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Plugins;

public abstract class RagPluginBase
{
    private readonly IMedicalDictionary _dictionary;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbedder _embedder;
    private readonly ISparseVectorizer _sparseVectorizer;
    private readonly ICrossEncoderReranker _reranker;

    protected RagPluginBase(
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker)
    {
        _dictionary = dictionary;
        _vectorStore = vectorStore;
        _embedder = embedder;
        _sparseVectorizer = sparseVectorizer;
        _reranker = reranker;
    }

    protected async Task<QueryResult> ExecuteSearchAsync(
        string query,
        string language,
        string[]? bookIds,
        CancellationToken cancellationToken = default)
    {
        var langFilter = ParseLanguage(language);
        var expandedTerms = await _dictionary.ExpandQueryAsync(query, cancellationToken);

        var allChunks = new List<MedicalChunk>();
        foreach (var term in expandedTerms)
        {
            var denseVector = await _embedder.EmbedQueryAsync(term, cancellationToken);
            var sparseVector = await _sparseVectorizer.VectorizeQueryAsync(term, cancellationToken);
            var chunks = await _vectorStore.SearchAsync(
                denseVector, sparseVector, langFilter, bookIds, topK: 5, cancellationToken);
            allChunks.AddRange(chunks);
        }

        var candidates = allChunks
            .DistinctBy(c => c.ChunkIndex + c.BookId)
            .ToList();

        var reranked = candidates.Count > 0
            ? await _reranker.RerankAsync(query, candidates, cancellationToken)
            : candidates;

        var distinctChunks = reranked.Take(5).ToList();

        var answer = BuildAnswer(distinctChunks);
        var sources = distinctChunks.Select(c => new SourceCitation
        {
            SourceType = SourceType.Book,
            BookTitle = c.BookTitle,
            Author = c.Author,
            ChapterTitle = c.ChapterTitle,
            SectionTitle = c.SectionTitle,
            PageStart = c.PageStart,
            PageEnd = c.PageEnd
        }).ToList();

        return new QueryResult { Answer = answer, Sources = sources };
    }

    private static string BuildAnswer(IReadOnlyList<MedicalChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return "No relevant information found in the indexed books.";
        }

        return string.Join("\n\n---\n\n", chunks.Select(c =>
            $"**{c.ChapterTitle} > {c.SectionTitle}**\n{c.Text}"));
    }

    private static LanguageFilter ParseLanguage(string language) => language.ToLowerInvariant() switch
    {
        LanguageCodes.English or LanguageCodes.EnglishName => LanguageFilter.English,
        LanguageCodes.Bulgarian or LanguageCodes.BulgarianName => LanguageFilter.Bulgarian,
        _ => LanguageFilter.Both
    };
}
