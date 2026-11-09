using MedAssist.AI.Plugins;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Tests;

// Queries are Bulgarian (the corpus language) so no query-preparation call adds a second search term
// and search counts reflect iteration passes only (tune-query-pipeline).
public sealed class RagIterativeLoopTests
{
    // Default options: score thresholds set to minimum so stub scores never trigger early exits
    private static RagOptions DefaultOptions => new()
    {
        ConfidenceThreshold = 0.0f,
        MaxIterations = 2,
        MinRetryScore = float.NegativeInfinity,
        MinAnswerScore = float.NegativeInfinity
    };

    private static Microsoft.SemanticKernel.Kernel BuildTestKernel()
    {
        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new StubChatCompletionService());
        return builder.Build();
    }

    private static SymptomsPlugin MakePlugin(
        IVectorStore vectorStore,
        ICrossEncoderReranker reranker,
        RagOptions? options = null)
    {
        return new SymptomsPlugin(
            BuildTestKernel(),
            new StubDictionary(),
            vectorStore,
            new StubEmbedder(),
            new StubSparseVectorizer(),
            reranker,
            options ?? DefaultOptions,
            NullLogger<RagPluginBase>.Instance);
    }

    [Fact]
    public async Task HighConfidenceScore_StopsAfterInitialSearch()
    {
        // Reranker returns 5.0 > threshold 0.0 → loop exits immediately after the first search
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(5.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 5, MinRetryScore = float.NegativeInfinity, MinAnswerScore = float.NegativeInfinity });

        await sut.SearchAsync("треска", "en");

        Assert.Equal(1, vectorStore.SearchCallCount);
    }

    [Fact]
    public async Task LowConfidenceScore_RunsFallbackIterations()
    {
        // Reranker returns -5.0 < threshold 0.0 → should run MaxIterations fallback passes
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(-5.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 3, MinRetryScore = float.NegativeInfinity, MinAnswerScore = float.NegativeInfinity });

        await sut.SearchAsync("треска", "en");

        // Initial search + 3 fallback iteration searches
        Assert.True(vectorStore.SearchCallCount > 1);
    }

    [Fact]
    public async Task MaxIterations_CappedAtFive()
    {
        // MaxIterations=10 in config should be capped at 5 by the implementation
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(-10.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 10, MinRetryScore = float.NegativeInfinity, MinAnswerScore = float.NegativeInfinity });

        await sut.SearchAsync("треска", "en");

        // With cap of 5, no more than 6 total search passes (initial + 5 fallback)
        Assert.True(vectorStore.SearchCallCount <= 6);
        Assert.True(vectorStore.SearchCallCount > 1);
    }

    [Fact]
    public async Task EnglishQuestion_SearchesWithOriginalAndPreparedQuery()
    {
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var sut = MakePlugin(vectorStore, new StubReranker(5.0f),
            new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0, MinRetryScore = float.NegativeInfinity, MinAnswerScore = float.NegativeInfinity });

        await sut.SearchAsync("fever", "en");

        Assert.Equal(2, vectorStore.SearchCallCount);
    }

    [Fact]
    public async Task OversizedExcerpts_SourcesMatchTheExcerptsActuallySent()
    {
        var big = Enumerable.Range(1, 5)
            .Select(i => new MedicalChunk
            {
                BookId = $"book{i}", BookTitle = $"Book {i}", Language = "bg", ChapterTitle = "Chapter",
                SectionTitle = "Section", ChunkIndex = i, Text = new string('ж', 12_000),
            })
            .ToArray();
        var sut = MakePlugin(new StubVectorStore(big), new StubReranker(5.0f),
            new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0, MinRetryScore = float.NegativeInfinity, MinAnswerScore = float.NegativeInfinity });

        // The stub chat echoes the final user message, so the answer shows the numbered excerpts sent.
        var result = await sut.SearchAsync("треска", "bg");

        Assert.InRange(result.Sources.Count, 1, 4);
        Assert.Contains($"[{result.Sources.Count}] (Book", result.Answer);
        Assert.DoesNotContain($"[{result.Sources.Count + 1}] (Book", result.Answer);
    }

    [Fact]
    public async Task ZeroMaxIterations_OnlyInitialSearch()
    {
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(-10.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0 });

        await sut.SearchAsync("треска", "en");

        Assert.Equal(1, vectorStore.SearchCallCount);
    }

    [Fact]
    public async Task EmptyVectorStore_ReturnsNoResultsMessage()
    {
        var vectorStore = new StubVectorStore([]);
        var reranker = new StubReranker(0.0f);
        var sut = MakePlugin(vectorStore, reranker);

        var result = await sut.SearchAsync("треска", "en");

        Assert.Contains("No relevant information found", result.Answer);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task ResultsAreCappedAtFive()
    {
        // Vector store returns 10 distinct chunks; final result must be at most 5
        var chunks = Enumerable.Range(1, 10).Select(MakeChunk).ToList();
        var vectorStore = new StubVectorStore(chunks);
        var reranker = new StubReranker(5.0f); // high score → stops early
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0 });

        var result = await sut.SearchAsync("треска", "en");

        Assert.True(result.Sources.Count <= 5);
    }

    [Fact]
    public async Task AnswerContainsChapterAndSectionTitles()
    {
        var chunk = new MedicalChunk
        {
            BookId = "b1", BookTitle = "Pediatrics", Author = "Author",
            Language = "en", ChapterTitle = "Endocrinology", SectionTitle = "Thyroid",
            PageStart = 100, PageEnd = 102, ChunkIndex = 1,
            Text = "Graves disease is an autoimmune condition."
        };
        var vectorStore = new StubVectorStore([chunk]);
        var reranker = new StubReranker(5.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0 });

        var result = await sut.SearchAsync("Graves", "en");

        Assert.Contains("Endocrinology", result.Answer);
        Assert.Contains("Thyroid", result.Answer);
        Assert.Contains("Graves disease", result.Answer);
    }

    [Fact]
    public async Task CompoundQuery_InitialGather_UsesFullQueryOnly()
    {
        // Dictionary expands compound term into fragments (simulates ExtractKeywords behaviour)
        var dict = new ExpandingStubDictionary(["малкомозъчните тонзили", "тонзили", "малкомозъчните"]);
        var embedder = new TrackingStubEmbedder();
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(5.0f); // high score → no retries

        var plugin = new SymptomsPlugin(
            BuildTestKernel(), dict, vectorStore, embedder,
            new StubSparseVectorizer(), reranker,
            new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0 },
            NullLogger<RagPluginBase>.Instance);

        await plugin.SearchAsync("малкомозъчните тонзили", "bg");

        // Initial gather must embed only the full query — fragments come in retries
        Assert.Contains("малкомозъчните тонзили", embedder.QueriedTexts);
        Assert.DoesNotContain("тонзили", embedder.QueriedTexts);
        Assert.DoesNotContain("малкомозъчните", embedder.QueriedTexts);
    }

    [Fact]
    public async Task SingleWordQuery_BehaviourUnchanged()
    {
        // Single-word query: expandedTerms == [query], so initial pass is identical to before
        var dict = new StubDictionary(); // returns [query] unchanged
        var embedder = new TrackingStubEmbedder();
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(5.0f);

        var plugin = new SymptomsPlugin(
            BuildTestKernel(), dict, vectorStore, embedder,
            new StubSparseVectorizer(), reranker,
            new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0 },
            NullLogger<RagPluginBase>.Instance);

        await plugin.SearchAsync("тонзилит", "bg");

        Assert.Single(embedder.QueriedTexts);
        Assert.Equal("тонзилит", embedder.QueriedTexts[0]);
    }

    private static MedicalChunk MakeChunk(int index) => new()
    {
        BookId = $"book{index}",
        BookTitle = $"Book {index}",
        Author = "Author",
        Language = "en",
        ChapterTitle = "Chapter",
        SectionTitle = "Section",
        PageStart = index,
        PageEnd = index + 1,
        ChunkIndex = index,
        Text = $"Content {index}"
    };

    private sealed class ExpandingStubDictionary(IReadOnlyList<string> terms) : IMedicalDictionary
    {
        public Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(terms);

        public Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken ct = default)
            => Task.FromResult<IllnessEntry?>(null);

        public Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IllnessEntry>>([]);

        public Task<IReadOnlyList<IllnessEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IllnessEntry>>([]);
    }

    [Fact]
    public async Task ActiveScope_RecordsTheRankingExcerptsAndOutcome()
    {
        var sut = MakePlugin(new StubVectorStore([MakeChunk(1), MakeChunk(2)]), new StubReranker(5.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        await sut.SearchAsync("треска", "bg");

        Assert.Equal(RagOutcome.Answered, scope.Trace.Outcome);
        Assert.Equal("треска", scope.Trace.SearchQuery);
        Assert.Equal(2, scope.Trace.Ranked.Count);
        Assert.Equal(5.0f, scope.Trace.Ranked[0].Score);
        Assert.Equal(2, scope.Trace.Excerpts.Count);
        Assert.True(scope.Trace.PromptTokens > 0);
    }

    [Fact]
    public async Task ReferenceListChunks_NeverReachTheModel()
    {
        var references = new MedicalChunk
        {
            BookId = "book2",
            BookTitle = "Book 2",
            ChunkIndex = 2,
            Text = "- 1. Nelson Textbook of Pediatrics, 19th ed, Philadelphia, Saunders, 2011.\n" +
                   "- 2. Smith J. et al. Pediatric emergencies. J. Pediatr. 2015, 166, 10-15.",
        };
        var sut = MakePlugin(new StubVectorStore([MakeChunk(1), references]), new StubReranker(5.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        var result = await sut.SearchAsync("треска", "bg");

        Assert.Equal("Book 1", Assert.Single(result.Sources).BookTitle);
        Assert.DoesNotContain(scope.Trace.Ranked, c => c.BookTitle == "Book 2");
    }

    [Fact]
    public async Task KeepReferenceMatterOverride_LetsReferenceListsThrough()
    {
        var references = new MedicalChunk
        {
            BookId = "book2",
            BookTitle = "Book 2",
            ChunkIndex = 2,
            Text = "- 1. Nelson Textbook of Pediatrics, 19th ed, Philadelphia, Saunders, 2011.\n" +
                   "- 2. Smith J. et al. Pediatric emergencies. J. Pediatr. 2015, 166, 10-15.",
        };
        var sut = MakePlugin(new StubVectorStore([MakeChunk(1), references]), new StubReranker(5.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides { KeepReferenceMatter = true });
        await sut.SearchAsync("треска", "bg");

        Assert.Contains(scope.Trace.Ranked, c => c.BookTitle == "Book 2");
    }

    [Fact]
    public async Task ActiveScope_MaxExcerptsLimitsTheAnswerSources()
    {
        var chunks = Enumerable.Range(1, 6).Select(MakeChunk).ToList();
        var sut = MakePlugin(new StubVectorStore(chunks), new StubReranker(5.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides { MaxExcerpts = 2, InitialTopK = 6 });
        var result = await sut.SearchAsync("треска", "bg");

        Assert.Equal(2, result.Sources.Count);
        Assert.Equal(6, scope.Trace.Ranked.Count);
    }

    [Fact]
    public async Task ActiveScope_InitialTopKReachesRetrieval()
    {
        var chunks = Enumerable.Range(1, 6).Select(MakeChunk).ToList();
        var sut = MakePlugin(new StubVectorStore(chunks), new StubReranker(5.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides { InitialTopK = 3 });
        await sut.SearchAsync("треска", "bg");

        Assert.Equal(3, scope.Trace.Ranked.Count);
    }

    [Fact]
    public async Task ActiveScope_TinyBudgetDropsExcerptsAndCountsThem()
    {
        var chunks = Enumerable.Range(1, 5).Select(MakeChunk).ToList();
        var sut = MakePlugin(new StubVectorStore(chunks), new StubReranker(5.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides { ContextWindowTokens = 1_000, AnswerReserveTokens = 999 });
        var result = await sut.SearchAsync("треска", "bg");

        Assert.Single(result.Sources);
        Assert.Equal(4, scope.Trace.ExcerptsDropped);
    }

    [Fact]
    public async Task ActiveScope_LowScoreRecordsWebFallback()
    {
        var options = new RagOptions { MinRetryScore = 1.0f, MinAnswerScore = 1.5f };
        var sut = MakePlugin(new StubVectorStore([MakeChunk(1)]), new StubReranker(-5.0f), options);

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        var result = await sut.SearchAsync("треска", "bg");

        Assert.True(result.RequiresWebFallback);
        Assert.Equal(RagOutcome.WebFallback, scope.Trace.Outcome);
        Assert.Single(scope.Trace.Ranked);
    }

    [Fact]
    public async Task ActiveScope_ScoreBelowTheAnswerFloorRecordsIt()
    {
        var options = new RagOptions { ConfidenceThreshold = 0f, MaxIterations = 0, MinRetryScore = 0f, MinAnswerScore = 9f };
        var sut = MakePlugin(new StubVectorStore([MakeChunk(1)]), new StubReranker(5.0f), options);

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        await sut.SearchAsync("треска", "bg");

        Assert.Equal(RagOutcome.BelowAnswerFloor, scope.Trace.Outcome);
    }

    [Fact]
    public async Task NamedConditionMissingFromExcerpts_IsRefusedWithoutCallingTheModel()
    {
        var sut = MakePlugin(new StubVectorStore([MakeChunk(1)]), new StubReranker(9.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        var result = await sut.SearchAsync("Какво е синдром на Зарковски-Хелман?", "bg");

        Assert.Equal(RagOutcome.ConditionNotInExcerpts, scope.Trace.Outcome);
        Assert.Empty(result.Sources);
        Assert.Contains("don't contain sufficiently relevant information", result.Answer);
    }

    [Fact]
    public async Task NamedCondition_SearchesWithTheSpellingTheBooksUse()
    {
        var chunk = new MedicalChunk { BookId = "book1", Language = "bg", ChunkIndex = 1, Text = "Синдром на Goodpasture." };
        var sut = MakePlugin(new StubVectorStore([chunk]), new StubReranker(9.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        await sut.SearchAsync("Какво е синдром на Гудпасчър?", "bg");

        Assert.Equal("Какво е синдром на Гудпасчър? goodpasture", scope.Trace.SearchQuery);
        Assert.Equal(RagOutcome.Answered, scope.Trace.Outcome);
    }

    [Theory]
    [InlineData("Гуилън-Барe синдром: определение, причини, симптоми", "Гуилън-Барe синдром")]
    [InlineData("Лечение на отит\nОбяснение: това е заявка", "Лечение на отит")]
    [InlineData("едно две три четири пет шест седем осем девет десет единадесет дванадесет 13 14 15 16 17 18",
        "едно две три четири пет шест седем осем девет десет единадесет дванадесет 13 14 15 16")]
    [InlineData("Кавасаки болест", "Кавасаки болест")]
    [InlineData("Заявка: лечение на отит", "Заявка: лечение на отит")]
    public void Shorten_KeepsOneShortQuery(string prepared, string expected)
    {
        Assert.Equal(expected, QueryPreparation.Shorten(prepared));
    }

    [Fact]
    public async Task NamedConditionInExcerpts_IsAnswered()
    {
        var chunk = new MedicalChunk { BookId = "book1", Language = "bg", ChunkIndex = 1, Text = "Болест на Kawasaki е системен васкулит." };
        var sut = MakePlugin(new StubVectorStore([chunk]), new StubReranker(9.0f));

        using var scope = RagRunScope.Begin(new RagRunOverrides());
        await sut.SearchAsync("Какво е болест на Кавазаки?", "bg");

        Assert.Equal(RagOutcome.Answered, scope.Trace.Outcome);
    }

    private sealed class TrackingStubEmbedder : IEmbedder
    {
        private readonly List<string> _queried = [];
        public IReadOnlyList<string> QueriedTexts => _queried;

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default)
        {
            _queried.Add(text);
            return Task.FromResult(new float[1024]);
        }

        public Task<float[]> EmbedPassageAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[1024]);
    }

    private sealed class StubDictionary : IMedicalDictionary
    {
        public Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([query]);

        public Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken ct = default)
            => Task.FromResult<IllnessEntry?>(null);

        public Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IllnessEntry>>([]);

        public Task<IReadOnlyList<IllnessEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IllnessEntry>>([]);
    }

    private sealed class StubVectorStore : IVectorStore
    {
        private readonly IReadOnlyList<MedicalChunk> _chunks;

        public int SearchCallCount { get; private set; }

        public StubVectorStore(IReadOnlyList<MedicalChunk> chunks) => _chunks = chunks;

        public Task<IReadOnlyList<MedicalChunk>> SearchAsync(
            float[] denseQueryVector, SparseVector? sparseQueryVector,
            LanguageFilter language, IReadOnlyList<string>? bookIds,
            int topK = 5, CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            return Task.FromResult<IReadOnlyList<MedicalChunk>>(_chunks.Take(topK).ToList());
        }

        public Task UpsertAsync(MedicalChunk chunk, float[] denseVector, SparseVector sparseVector, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MedicalChunk>> ScrollSectionAsync(string chapterTitle, string sectionTitle, string bookId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MedicalChunk>>([]);

        public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteByBookAsync(string bookId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubEmbedder : IEmbedder
    {
        public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[1024]);

        public Task<float[]> EmbedPassageAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[1024]);
    }

    private sealed class StubSparseVectorizer : ISparseVectorizer
    {
        public Task<SparseVector> VectorizeQueryAsync(string text, CancellationToken ct = default)
            => Task.FromResult(SparseVector.Empty);

        public Task<SparseVector> VectorizePassageAsync(string text, CancellationToken ct = default)
            => Task.FromResult(SparseVector.Empty);

        public Task<IReadOnlyDictionary<string, float>> GetWordWeightsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, float>>(
                new Dictionary<string, float> { ["goodpasture"] = 5f, ["fever"] = 2f, ["бронхиолит"] = 5f });
    }

    private sealed class StubReranker : ICrossEncoderReranker
    {
        private readonly float _score;

        public StubReranker(float score) => _score = score;

        public Task<IReadOnlyList<ScoredChunk>> RerankAsync(
            string query, IReadOnlyList<MedicalChunk> candidates, CancellationToken ct = default)
        {
            IReadOnlyList<ScoredChunk> result = candidates
                .Select(c => new ScoredChunk(c, _score))
                .ToList();
            return Task.FromResult(result);
        }
    }

    // Returns the user message verbatim so answer-content assertions remain valid.
    private sealed class StubChatCompletionService : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var userMsg = chatHistory.LastOrDefault(m => m.Role == AuthorRole.User)?.Content ?? string.Empty;
            IReadOnlyList<ChatMessageContent> result = [new ChatMessageContent(AuthorRole.Assistant, userMsg)];
            return Task.FromResult(result);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
