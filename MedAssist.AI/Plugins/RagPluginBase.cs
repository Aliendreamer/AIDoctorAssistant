using System.Text;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using SKKernel = Microsoft.SemanticKernel.Kernel;

namespace MedAssist.AI.Plugins;

public abstract class RagPluginBase
{
    private readonly SKKernel _kernel;
    private readonly IMedicalDictionary _dictionary;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbedder _embedder;
    private readonly ISparseVectorizer _sparseVectorizer;
    private readonly ICrossEncoderReranker _reranker;
    private readonly RagOptions _options;

    // Each strategy widens the search space progressively.
    // Strategies are indexed by iteration number (0 = first fallback pass after initial search).
    private static readonly RetryStrategy[] _strategies =
    [
        new(TopK: 10, AnyLanguage: false, LongestOnly: false),
        new(TopK: 10, AnyLanguage: true,  LongestOnly: false),
        new(TopK: 15, AnyLanguage: false, LongestOnly: false),
        new(TopK: 15, AnyLanguage: true,  LongestOnly: false),
        new(TopK: 20, AnyLanguage: true,  LongestOnly: true),
    ];

    protected RagPluginBase(
        SKKernel kernel,
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker,
        RagOptions options)
    {
        _kernel = kernel;
        _dictionary = dictionary;
        _vectorStore = vectorStore;
        _embedder = embedder;
        _sparseVectorizer = sparseVectorizer;
        _reranker = reranker;
        _options = options;
    }

    protected async Task<QueryResult> ExecuteSearchAsync(
        string query,
        string language,
        string[]? bookIds,
        IReadOnlyList<BookInfo>? books = null,
        CancellationToken cancellationToken = default)
    {
        var langFilter = ParseLanguage(language);
        var expandedTerms = await _dictionary.ExpandQueryAsync(query, cancellationToken);

        var candidates = await GatherCandidatesAsync(expandedTerms, langFilter, bookIds, topK: 5, cancellationToken);
        candidates = await ExpandBySectionAsync(candidates, cancellationToken);

        if (candidates.Count == 0)
        {
            return new QueryResult { Answer = "No relevant information found in the indexed books." };
        }

        var scored = await _reranker.RerankAsync(query, candidates, cancellationToken);

        var maxIter = Math.Min(_options.MaxIterations, 5);
        for (var iter = 0; iter < maxIter; iter++)
        {
            if (scored.Count > 0 && scored[0].Score >= _options.ConfidenceThreshold)
            {
                break;
            }

            var strategy = _strategies[iter];
            var iterLang = strategy.AnyLanguage ? LanguageFilter.Both : langFilter;
            var iterTerms = SelectTerms(expandedTerms, strategy);

            var newChunks = await GatherCandidatesAsync(iterTerms, iterLang, bookIds, strategy.TopK, cancellationToken);
            newChunks = await ExpandBySectionAsync(newChunks, cancellationToken);
            if (newChunks.Count == 0)
            {
                continue;
            }

            candidates = candidates
                .Concat(newChunks)
                .DistinctBy(c => $"{c.BookId}:{c.ChunkIndex}")
                .ToList();

            scored = await _reranker.RerankAsync(query, candidates, cancellationToken);
        }

        // Exclude summary chunks from the final answer
        var topChunks = scored
            .Select(s => s.Chunk)
            .Where(c => !c.IsSummary)
            .Take(5)
            .ToList();

        return await BuildResultAsync(query, topChunks, books, cancellationToken);
    }

    private async Task<List<MedicalChunk>> GatherCandidatesAsync(
        IReadOnlyList<string> terms,
        LanguageFilter langFilter,
        string[]? bookIds,
        int topK,
        CancellationToken cancellationToken)
    {
        var chunks = new List<MedicalChunk>();
        foreach (var term in terms)
        {
            var denseVector = await _embedder.EmbedQueryAsync(term, cancellationToken);
            var sparseVector = await _sparseVectorizer.VectorizeQueryAsync(term, cancellationToken);
            var results = await _vectorStore.SearchAsync(denseVector, sparseVector, langFilter, bookIds, topK, cancellationToken);
            chunks.AddRange(results);
        }

        return chunks;
    }

    // For each unique (chapter, section, bookId) in the candidate pool — including
    // summary chunks which act as retrieval triggers — fetch all regular chunks from
    // that section and merge them into the pool.
    private async Task<List<MedicalChunk>> ExpandBySectionAsync(
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
            var sectionChunks = await _vectorStore.ScrollSectionAsync(chapter, section, bookId, limit: 50, cancellationToken);
            expanded.AddRange(sectionChunks);
        }

        // Deduplicate by BookId:ChunkIndex; summaries from search stay in pool
        // until the final filter in ExecuteSearchAsync removes them from the answer.
        return expanded
            .DistinctBy(c => $"{c.BookId}:{c.ChunkIndex}")
            .ToList();
    }

    private static IReadOnlyList<string> SelectTerms(IReadOnlyList<string> expandedTerms, RetryStrategy strategy)
    {
        if (strategy.LongestOnly)
        {
            var longest = expandedTerms.MaxBy(t => t.Length);
            return longest is null ? [] : [longest];
        }

        return expandedTerms;
    }

    private async Task<QueryResult> BuildResultAsync(
        string query,
        IReadOnlyList<MedicalChunk> chunks,
        IReadOnlyList<BookInfo>? books,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return new QueryResult { Answer = "No relevant information found in the indexed books." };
        }

        var sources = chunks.Select(c => new SourceCitation
        {
            SourceType = SourceType.Book,
            BookTitle = c.BookTitle,
            Author = c.Author,
            ChapterTitle = c.ChapterTitle,
            SectionTitle = c.SectionTitle,
            PageStart = c.PageStart,
            PageEnd = c.PageEnd
        }).ToList();

        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine(
            "You are MedAssist, a clinical decision support assistant for physicians. " +
            "Answer using only the provided excerpts. Be direct and clinical. " +
            "Use markdown (headings, bullets, bold terms) where it aids clarity. " +
            "Cite the book title and section when referencing specific content. " +
            "If the excerpts are insufficient, say so explicitly — do not speculate.");

        if (books is { Count: > 0 })
        {
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("Sources searched:");
            foreach (var b in books)
            {
                var entry = string.IsNullOrEmpty(b.Author)
                    ? $"- {b.Title}"
                    : $"- {b.Title} by {b.Author}";
                systemPrompt.AppendLine(entry);
            }
        }

        var context = new StringBuilder();
        foreach (var c in chunks)
        {
            context.AppendLine($"[{c.BookTitle} — {c.ChapterTitle} › {c.SectionTitle}]");
            context.AppendLine(c.Text);
            context.AppendLine();
        }

        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt.ToString());
        history.AddUserMessage($"Question: {query}\n\nMedical excerpts:\n\n{context}");

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        var answer = response.Content ?? "Unable to generate a response.";

        return new QueryResult { Answer = answer, Sources = sources };
    }

    private static LanguageFilter ParseLanguage(string language) => language.ToLowerInvariant() switch
    {
        LanguageCodes.English or LanguageCodes.EnglishName => LanguageFilter.English,
        LanguageCodes.Bulgarian or LanguageCodes.BulgarianName => LanguageFilter.Bulgarian,
        _ => LanguageFilter.Both
    };

    private sealed record RetryStrategy(int TopK, bool AnyLanguage, bool LongestOnly);
}
