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
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker,
        RagOptions options)
    {
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
        CancellationToken cancellationToken = default)
    {
        var langFilter = ParseLanguage(language);
        var expandedTerms = await _dictionary.ExpandQueryAsync(query, cancellationToken);

        var candidates = await GatherCandidatesAsync(expandedTerms, langFilter, bookIds, topK: 5, cancellationToken);

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

        var topChunks = scored.Select(s => s.Chunk).Take(5).ToList();
        return BuildResult(topChunks);
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

    private static IReadOnlyList<string> SelectTerms(IReadOnlyList<string> expandedTerms, RetryStrategy strategy)
    {
        if (strategy.LongestOnly)
        {
            var longest = expandedTerms.MaxBy(t => t.Length);
            return longest is null ? [] : [longest];
        }
        return expandedTerms;
    }

    private static QueryResult BuildResult(IReadOnlyList<MedicalChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return new QueryResult { Answer = "No relevant information found in the indexed books." };
        }

        var answer = string.Join("\n\n---\n\n", chunks.Select(c =>
            $"**{c.ChapterTitle} > {c.SectionTitle}**\n{c.Text}"));

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
