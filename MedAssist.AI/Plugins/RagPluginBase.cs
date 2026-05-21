using System.Text;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RagPluginBase> _logger;

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
        RagOptions options,
        ILogger<RagPluginBase> logger)
    {
        _kernel = kernel;
        _dictionary = dictionary;
        _vectorStore = vectorStore;
        _embedder = embedder;
        _sparseVectorizer = sparseVectorizer;
        _reranker = reranker;
        _options = options;
        _logger = logger;
    }

    protected async Task<QueryResult> ExecuteSearchAsync(
        string query,
        string language,
        string[]? bookIds,
        IReadOnlyList<BookInfo>? books = null,
        IReadOnlyList<ChatMessageDto>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var langFilter = ParseLanguage(language);
        var expandedTerms = await _dictionary.ExpandQueryAsync(query, cancellationToken);

        // Use only the full query for the initial pass to preserve compound-term semantics.
        // Per-keyword fragments from expandedTerms fire in the retry loop below.
        var candidates = await GatherCandidatesAsync([query], langFilter, bookIds, topK: 5, cancellationToken);
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

        var topScore = scored.Count > 0 ? scored[0].Score : float.NegativeInfinity;
        _logger.LogInformation("Reranker top score for query '{Query}': {Score:F3} (threshold: {Threshold})",
            query, topScore, _options.MinAnswerScore);

        // Reject if the best result still doesn't meet the answer quality floor
        if (scored.Count == 0 || topScore < _options.MinAnswerScore)
        {
            return new QueryResult { Answer = "The indexed books don't contain sufficiently relevant information to answer this question. Try rephrasing or consult an external source." };
        }

        // Guard against domain-drift: if the query contains specific Latin terms (medical eponyms,
        // drug names) that the cross-encoder can't evaluate in Bulgarian context, verify they
        // actually appear in the retrieved chunks.
        var latinTerms = ExtractLatinTerms(query);
        if (latinTerms.Count > 0)
        {
            var topTexts = scored.Take(3).Select(s => s.Chunk.Text).ToList();
            var anyFound = latinTerms.Any(t => topTexts.Any(txt => txt.Contains(t, StringComparison.OrdinalIgnoreCase)));
            if (!anyFound)
            {
                _logger.LogInformation("Latin term check failed for query '{Query}' — terms [{Terms}] absent from top chunks",
                    query, string.Join(", ", latinTerms));
                return new QueryResult { Answer = "The indexed books don't contain sufficiently relevant information to answer this question. Try rephrasing or consult an external source." };
            }
        }

        // Exclude summary chunks from the final answer
        var topChunks = scored
            .Select(s => s.Chunk)
            .Where(c => !c.IsSummary)
            .Take(5)
            .ToList();

        return await BuildResultAsync(query, topChunks, books, conversationHistory, cancellationToken);
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
        IReadOnlyList<ChatMessageDto>? conversationHistory,
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
        systemPrompt.AppendLine(GetSystemPrompt());

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

        if (conversationHistory is { Count: > 0 })
        {
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "user")
                {
                    history.AddUserMessage(msg.Content);
                }
                else
                {
                    history.AddAssistantMessage(msg.Content);
                }
            }
        }

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

    // Extract purely-Latin alphabetic words (≥5 chars) from the query.
    // These are typically medical eponyms (Chiari, Arnold, Wilson, Parkinson) or
    // specific English terms that should appear verbatim in relevant chunks.
    private static IReadOnlyList<string> ExtractLatinTerms(string query)
    {
        return query
            .Split([' ', ',', '.', '!', '?', '(', ')', ':', ';', '\t', '\n', '/', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 5 && w.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    protected virtual string GetSystemPrompt() =>
        "You are MedAssist, a clinical decision support assistant for physicians. " +
        "IMPORTANT: Write your entire response as flowing prose — continuous sentences and paragraphs only. " +
        "NEVER use numbered lists, bullet points, dashes, asterisks, bold text, headers, or any markdown. " +
        "Do not organise your answer as a list of topics. Write as you would explain to a colleague in conversation. " +
        "Synthesise the excerpts into a coherent paragraph-form answer in your own words. " +
        "Mention the source book or section naturally within the text when relevant. " +
        "If the excerpts are insufficient to answer, say so in one sentence — do not speculate.";

    private sealed record RetryStrategy(int TopK, bool AnyLanguage, bool LongestOnly);
}
