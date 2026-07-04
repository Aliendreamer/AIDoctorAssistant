using System.Text;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using SKKernel = Microsoft.SemanticKernel.Kernel;

namespace MedAssist.AI.Plugins;

public abstract partial class RagPluginBase
{
    private readonly SKKernel _kernel;
    private readonly IMedicalDictionary _dictionary;
    private readonly ICrossEncoderReranker _reranker;
    private readonly CandidateRetriever _retriever;
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
        _reranker = reranker;
        _retriever = new CandidateRetriever(embedder, sparseVectorizer, vectorStore);
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

        // Rewrite short follow-up queries so the search has enough context to retrieve the right content.
        var searchQuery = await MaybeRewriteQueryAsync(query, conversationHistory, cancellationToken);

        var expandedTerms = await _dictionary.ExpandQueryAsync(searchQuery, cancellationToken);

        // Search with both original and rewritten query — the rewrite enriches semantics but the
        // original anchors retrieval when the rewrite drifts from the indexed vocabulary.
        var initialTerms = searchQuery == query
            ? (IReadOnlyList<string>)[query]
            : [query, searchQuery];
        var candidates = await _retriever.GatherAsync(initialTerms, langFilter, bookIds, topK: 5, cancellationToken);
        candidates = await _retriever.ExpandBySectionAsync(candidates, cancellationToken);

        if (candidates.Count == 0)
        {
            return new QueryResult { Answer = "No relevant information found in the indexed books." };
        }

        var scored = await _reranker.RerankAsync(searchQuery, candidates, cancellationToken);

        // CRAG "INCORRECT" branch: initial score is so low that retrying won't help — signal web fallback.
        var initialScore = scored.Count > 0 ? scored[0].Score : float.NegativeInfinity;
        if (initialScore < _options.MinRetryScore)
        {
            _logger.LogInformation("Initial score {Score:F3} below MinRetryScore {Threshold} — skipping retries, flagging web fallback",
                initialScore, _options.MinRetryScore);
            return new QueryResult { RequiresWebFallback = true, Answer = "The indexed books don't contain sufficiently relevant information to answer this question. Try rephrasing or consult an external source." };
        }

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

            var newChunks = await _retriever.GatherAsync(iterTerms, iterLang, bookIds, strategy.TopK, cancellationToken);
            newChunks = await _retriever.ExpandBySectionAsync(newChunks, cancellationToken);
            if (newChunks.Count == 0)
            {
                continue;
            }

            candidates = candidates
                .Concat(newChunks)
                .DistinctBy(c => $"{c.BookId}:{c.ChunkIndex}")
                .ToList();

            scored = await _reranker.RerankAsync(searchQuery, candidates, cancellationToken);
        }

        var topScore = scored.Count > 0 ? scored[0].Score : float.NegativeInfinity;
        // Debug level: query text can be PHI, so it stays out of default (Information) logs (audit P2-9).
        _logger.LogDebug("Reranker top score for query '{Query}' (search: '{SearchQuery}'): {Score:F3} (threshold: {Threshold})",
            query, searchQuery, topScore, _options.MinAnswerScore);

        // Reject if the best result still doesn't meet the answer quality floor
        if (scored.Count == 0 || topScore < _options.MinAnswerScore)
        {
            return new QueryResult { Answer = "The indexed books don't contain sufficiently relevant information to answer this question. Try rephrasing or consult an external source." };
        }

        // Guard against domain-drift: if the query contains specific Latin terms (medical eponyms,
        // drug names) that the cross-encoder can't evaluate in Bulgarian context, verify they
        // actually appear in the retrieved chunks.
        var latinTerms = ExtractLatinTerms(searchQuery);
        if (latinTerms.Count > 0)
        {
            var topTexts = scored.Take(3).Select(s => s.Chunk.Text).ToList();
            var anyFound = latinTerms.Any(t => topTexts.Any(txt => txt.Contains(t, StringComparison.OrdinalIgnoreCase)));
            if (!anyFound)
            {
                _logger.LogDebug("Latin term check failed for query '{Query}' — terms [{Terms}] absent from top chunks",
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

        var languageInstruction = query.Any(c => c is >= 'Ѐ' and <= 'ӿ')
            ? "ВАЖНО: Отговорът трябва да е изцяло на български език. Не използвай никакъв друг език.\n\n"
            : "IMPORTANT: Respond entirely in English.\n\n";

        // /no_think: reasoning models (qwen3) otherwise prepend a long <think> block — we want prose
        // only, and skipping the reasoning also cuts latency. Harmless for non-reasoning models.
        history.AddUserMessage($"{languageInstruction}Question: {query}\n\nMedical excerpts:\n\n{context}\n\n/no_think");

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        var answer = MarkdownStripper.Strip(response.Content ?? "Unable to generate a response.");

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

    private async Task<string> MaybeRewriteQueryAsync(
        string query,
        IReadOnlyList<ChatMessageDto>? conversationHistory,
        CancellationToken cancellationToken)
    {
        var wordCount = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount >= 20 || conversationHistory is not { Count: >= 2 })
        {
            return query;
        }

        var lastUser = conversationHistory.LastOrDefault(m => m.Role == "user")?.Content;
        if (lastUser is null || lastUser == query)
        {
            return query;
        }

        var rewriteHistory = new ChatHistory();
        rewriteHistory.AddSystemMessage(
            "You are a medical search query optimizer. " +
            "Rewrite the follow-up question as a concise, self-contained medical search query using context from the previous question. " +
            "IMPORTANT: Keep the rewritten query in the exact same language as the follow-up question. " +
            "Output ONLY the rewritten query — no explanation, no quotes.");
        rewriteHistory.AddUserMessage(
            $"Previous question: {lastUser}\n\nFollow-up: {query}\n\nRewritten query:\n\n/no_think");

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chat.GetChatMessageContentAsync(rewriteHistory, cancellationToken: cancellationToken);
        // Strip any reasoning block so a qwen3 <think>…</think> can't leak into the search query.
        var rewritten = MarkdownStripper.Strip(response.Content ?? string.Empty);

        _logger.LogDebug("Query rewrite: '{Original}' → '{Rewritten}'", query, rewritten);
        return string.IsNullOrWhiteSpace(rewritten) ? query : rewritten;
    }

    protected virtual string GetSystemPrompt() =>
        """
        You are MedAssist, a clinical decision support assistant for physicians.

        Your answers must be written as continuous prose — the same way a knowledgeable colleague explains something in conversation. Study the example below and match its style exactly.

        EXAMPLE QUESTION: What is Graves' disease?

        EXAMPLE ANSWER:
        Graves' disease is an autoimmune disorder in which the immune system produces thyroid-stimulating immunoglobulins that bind to and chronically activate TSH receptors, driving the thyroid to overproduce thyroxine. It is the single most common cause of hyperthyroidism, responsible for roughly 80% of cases according to the endocrinology sources indexed here. Patients typically present with a constellation of symptoms reflecting thyroid excess — palpitations, heat intolerance, unintentional weight loss despite a normal or increased appetite, fine tremor, and anxiety. A hallmark not shared with other causes of hyperthyroidism is Graves' ophthalmopathy, in which immune-mediated inflammation of the orbital tissues produces proptosis, periorbital oedema, and in severe cases diplopia or corneal exposure injury. Treatment is chosen based on patient age, goitre size, and disease severity, and the main options are antithyroid drugs such as methimazole, radioactive iodine ablation, or surgical thyroidectomy. The choice between these is discussed at length in the indexed textbooks, which note that antithyroid drugs are preferred as first-line therapy in younger patients and during pregnancy, while definitive ablative treatment is generally preferred when medical therapy fails or relapse occurs.

        RULES — follow these without exception:
        - Always respond in the same language the user asked in. If the question is in Bulgarian, answer in Bulgarian. If in English, answer in English.
        - Write only in paragraphs of complete sentences. No lists of any kind.
        - Do not start any line with a dash, asterisk, number, or heading marker.
        - Do not bold or italicise any text.
        - Weave source references naturally into the prose ("according to the paediatrics textbook…", "as described in the indexed sources…").
        - If the excerpts are insufficient, say so in one sentence in the user's language and stop.
        """;


    private sealed record RetryStrategy(int TopK, bool AnyLanguage, bool LongestOnly);
}
