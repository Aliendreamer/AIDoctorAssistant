using MedAssist.AI.Plugins;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

public sealed class RagIterativeLoopTests
{
    // Default options: threshold 0.0, 2 iterations
    private static RagOptions DefaultOptions => new() { ConfidenceThreshold = 0.0f, MaxIterations = 2 };

    private static SymptomsPlugin MakePlugin(
        IVectorStore vectorStore,
        ICrossEncoderReranker reranker,
        RagOptions? options = null)
    {
        return new SymptomsPlugin(
            new StubDictionary(),
            vectorStore,
            new StubEmbedder(),
            new StubSparseVectorizer(),
            reranker,
            options ?? DefaultOptions);
    }

    [Fact]
    public async Task HighConfidenceScore_StopsAfterInitialSearch()
    {
        // Reranker returns 5.0 > threshold 0.0 → loop exits immediately after the first search
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(5.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 5 });

        await sut.SearchAsync("fever", "en");

        Assert.Equal(1, vectorStore.SearchCallCount);
    }

    [Fact]
    public async Task LowConfidenceScore_RunsFallbackIterations()
    {
        // Reranker returns -5.0 < threshold 0.0 → should run MaxIterations fallback passes
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(-5.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 3 });

        await sut.SearchAsync("fever", "en");

        // Initial search + 3 fallback iteration searches
        Assert.True(vectorStore.SearchCallCount > 1);
    }

    [Fact]
    public async Task MaxIterations_CappedAtFive()
    {
        // MaxIterations=10 in config should be capped at 5 by the implementation
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(-10.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 10 });

        await sut.SearchAsync("fever", "en");

        // With cap of 5, no more than 6 total search passes (initial + 5 fallback)
        Assert.True(vectorStore.SearchCallCount <= 6);
        Assert.True(vectorStore.SearchCallCount > 1);
    }

    [Fact]
    public async Task ZeroMaxIterations_OnlyInitialSearch()
    {
        var vectorStore = new StubVectorStore([MakeChunk(1)]);
        var reranker = new StubReranker(-10.0f);
        var sut = MakePlugin(vectorStore, reranker, new RagOptions { ConfidenceThreshold = 0.0f, MaxIterations = 0 });

        await sut.SearchAsync("fever", "en");

        Assert.Equal(1, vectorStore.SearchCallCount);
    }

    [Fact]
    public async Task EmptyVectorStore_ReturnsNoResultsMessage()
    {
        var vectorStore = new StubVectorStore([]);
        var reranker = new StubReranker(0.0f);
        var sut = MakePlugin(vectorStore, reranker);

        var result = await sut.SearchAsync("fever", "en");

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

        var result = await sut.SearchAsync("fever", "en");

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
}
