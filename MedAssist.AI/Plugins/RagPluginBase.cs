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

    private const string _notCoveredAnswer =
        "The indexed books don't contain sufficiently relevant information to answer this question. Try rephrasing or consult an external source.";

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
        IReadOnlyList<ChatMessageDto>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var langFilter = ParseLanguage(language);

        // Bulgarian search query (the corpus language): translates non-Bulgarian questions and makes
        // follow-ups self-contained, so retrieval and reranking see the corpus language.
        var searchQuery = await PrepareSearchQueryAsync(query, conversationHistory, cancellationToken);

        // The books mostly spell eponyms in Latin, so add their spelling for retrieval and reranking.
        var namedConditions = ConditionNameGuard.ExtractNames(query);
        var spellings = await _retriever.FindBookSpellingsAsync(namedConditions, cancellationToken);

        // The model invents Bulgarian medical words, so add the books' own term for each one it translated.
        var bookTerms = await _retriever.FindBookTermsAsync(query, namedConditions, cancellationToken);
        var additions = spellings.Concat(bookTerms).ToList();
        if (additions.Count > 0)
        {
            searchQuery = $"{searchQuery} {string.Join(' ', additions)}";
        }

        var trace = RagRunScope.Current?.Trace;
        if (trace is not null)
        {
            trace.SearchQuery = searchQuery;
        }

        var expandedTerms = await _dictionary.ExpandQueryAsync(searchQuery, cancellationToken);

        // Search with both original and rewritten query — the rewrite enriches semantics but the
        // original anchors retrieval when the rewrite drifts from the indexed vocabulary.
        var initialTerms = searchQuery == query
            ? (IReadOnlyList<string>)[query]
            : [query, searchQuery];
        var candidates = await _retriever.GatherAsync(initialTerms, langFilter, bookIds, RagRunScope.InitialTopK, cancellationToken);
        candidates = await _retriever.ExpandBySectionAsync(candidates, cancellationToken);

        if (candidates.Count == 0)
        {
            SetOutcome(RagOutcome.NoCandidates);
            return new QueryResult { Answer = "No relevant information found in the indexed books." };
        }

        var scored = await QueryPreparation.RerankByLanguageAsync(_reranker, searchQuery, query, candidates, cancellationToken);

        // CRAG "INCORRECT" branch: initial score is so low that retrying won't help — signal web fallback.
        var initialScore = scored.Count > 0 ? scored[0].Score : float.NegativeInfinity;
        if (initialScore < _options.MinRetryScore)
        {
            _logger.LogInformation("Initial score {Score:F3} below MinRetryScore {Threshold} — skipping retries, flagging web fallback",
                initialScore, _options.MinRetryScore);
            RecordRanking(scored);
            SetOutcome(RagOutcome.WebFallback);
            return new QueryResult { RequiresWebFallback = true, Answer = _notCoveredAnswer };
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
                .DistinctBy(c => (c.BookId, c.ChunkIndex))
                .ToList();

            scored = await QueryPreparation.RerankByLanguageAsync(_reranker, searchQuery, query, candidates, cancellationToken);
        }

        RecordRanking(scored);

        var topScore = scored.Count > 0 ? scored[0].Score : float.NegativeInfinity;
        // Debug level: query text can be PHI, so it stays out of default (Information) logs (audit P2-9).
        _logger.LogDebug("Reranker top score for query '{Query}' (search: '{SearchQuery}'): {Score:F3} (threshold: {Threshold})",
            query, searchQuery, topScore, _options.MinAnswerScore);

        // Reject if the best result still doesn't meet the answer quality floor
        if (scored.Count == 0 || topScore < _options.MinAnswerScore)
        {
            SetOutcome(RagOutcome.BelowAnswerFloor);
            return new QueryResult { Answer = _notCoveredAnswer };
        }

        // Guard against domain-drift: if the query contains specific Latin terms (medical eponyms,
        // drug names) that the cross-encoder can't evaluate in Bulgarian context, verify they
        // actually appear in the retrieved chunks.
        var latinTerms = QueryPreparation.ExtractLatinTerms(searchQuery);
        if (latinTerms.Count > 0)
        {
            var topTexts = scored.Take(3).Select(s => s.Chunk.Text).ToList();
            var anyFound = latinTerms.Any(t => topTexts.Any(txt => txt.Contains(t, StringComparison.OrdinalIgnoreCase)));
            if (!anyFound)
            {
                _logger.LogDebug("Latin term check failed for query '{Query}' — terms [{Terms}] absent from top chunks",
                    query, string.Join(", ", latinTerms));
                SetOutcome(RagOutcome.LatinTermGuard);
                return new QueryResult { Answer = _notCoveredAnswer };
            }
        }

        // Exclude summary chunks from the final answer
        var topChunks = scored
            .Select(s => s.Chunk)
            .Where(c => !c.IsSummary)
            .Take(RagRunScope.MaxExcerpts)
            .ToList();

        // Reranker scores can't tell an invented disease from a real one, so a condition the question
        // names has to actually appear in what the answer would be built from.
        if (!ConditionNameGuard.AllAppearIn(namedConditions, topChunks.Select(c => c.Text)))
        {
            _logger.LogDebug("Condition named in query '{Query}' is absent from the excerpts", query);
            SetOutcome(RagOutcome.ConditionNotInExcerpts);
            return new QueryResult { Answer = _notCoveredAnswer };
        }

        return await BuildResultAsync(query, topChunks, conversationHistory, cancellationToken);
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
        IReadOnlyList<ChatMessageDto>? conversationHistory,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            SetOutcome(RagOutcome.NoCandidates);
            return new QueryResult { Answer = "No relevant information found in the indexed books." };
        }

        // Fit the prompt to the context window first: dropped excerpts must not appear in `sources`.
        var (excerpts, history) = AnswerPromptBuilder.FitToBudget(
            Profile, query, chunks, conversationHistory, RagRunScope.PromptBudgetTokens);

        // Citation-marker contract (change cited-answer-markers): `sources` and the numbered
        // excerpts in the prompt are both built from `excerpts` in the SAME order, so excerpt [n]
        // corresponds to sources[n-1]. QueryService appends web sources after these, preserving the
        // book indices. Keep these two loops in lockstep.
        var sources = excerpts.Select(c => new SourceCitation
        {
            SourceType = SourceType.Book,
            BookTitle = c.BookTitle,
            Author = c.Author,
            ChapterTitle = c.ChapterTitle,
            SectionTitle = c.SectionTitle,
            PageStart = c.PageStart,
            PageEnd = c.PageEnd
        }).ToList();

        var prompt = AnswerPromptBuilder.BuildBookAnswer(Profile, query, excerpts, history);

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chat.GetChatMessageContentAsync(prompt, AnswerPromptBuilder.ExecutionSettings, cancellationToken: cancellationToken);
        PromptMetrics.Record(GetType().Name.Replace("Plugin", string.Empty, StringComparison.Ordinal), prompt, response);
        var answer = MarkdownStripper.Strip(response.Content ?? "Unable to generate a response.");

        if (RagRunScope.Current?.Trace is { } trace)
        {
            (trace.PromptTokens, trace.PromptTokensEstimated) = PromptMetrics.PromptTokens(prompt, response);
            trace.CompletionTokens = PromptMetrics.CompletionTokens(response);
            trace.Excerpts = excerpts.Select(c => ToTraced(c, 0f)).ToList();
            trace.ExcerptsDropped = chunks.Count - excerpts.Count;
            trace.Outcome = RagOutcome.Answered;
        }

        return new QueryResult { Answer = answer, Sources = sources };
    }

    private static void SetOutcome(RagOutcome outcome)
    {
        if (RagRunScope.Current?.Trace is { } trace)
        {
            trace.Outcome = outcome;
        }
    }

    private static void RecordRanking(IReadOnlyList<ScoredChunk> scored)
    {
        if (RagRunScope.Current?.Trace is { } trace)
        {
            trace.Ranked = scored.Where(s => !s.Chunk.IsSummary).Select(s => ToTraced(s.Chunk, s.Score)).ToList();
        }
    }

    private static TracedChunk ToTraced(MedicalChunk chunk, float score) => new()
    {
        BookTitle = chunk.BookTitle,
        ChapterTitle = chunk.ChapterTitle,
        SectionTitle = chunk.SectionTitle,
        PageStart = chunk.PageStart,
        PageEnd = chunk.PageEnd,
        Score = score,
        Text = chunk.Text,
    };

    private static LanguageFilter ParseLanguage(string language) => language.ToLowerInvariant() switch
    {
        LanguageCodes.English or LanguageCodes.EnglishName => LanguageFilter.English,
        LanguageCodes.Bulgarian or LanguageCodes.BulgarianName => LanguageFilter.Bulgarian,
        _ => LanguageFilter.Both
    };

    private async Task<string> PrepareSearchQueryAsync(
        string query,
        IReadOnlyList<ChatMessageDto>? conversationHistory,
        CancellationToken cancellationToken)
    {
        if (!QueryPreparation.NeedsPreparation(query, conversationHistory))
        {
            return query;
        }

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chat.GetChatMessageContentAsync(
            QueryPreparation.BuildPrompt(query, conversationHistory), QueryPreparation.Settings,
            cancellationToken: cancellationToken);
        // Strip any reasoning block so a qwen3 <think>…</think> can't leak into the search query.
        var prepared = QueryPreparation.Shorten(MarkdownStripper.Strip(response.Content ?? string.Empty));

        _logger.LogDebug("Search query prepared: '{Original}' → '{Prepared}'", query, prepared);
        return string.IsNullOrWhiteSpace(prepared) ? query : prepared;
    }

    /// <summary>This query type's delta over the shared prompt core (see <see cref="AnswerPromptBuilder"/>).</summary>
    protected virtual PromptProfile Profile => PromptProfiles.Default;


    private sealed record RetryStrategy(int TopK, bool AnyLanguage, bool LongestOnly);
}
