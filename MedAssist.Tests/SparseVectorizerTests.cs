using MedAssist.AI.Embedding;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

public sealed class SparseVectorizerTests
{
    private static IBM25VocabStore MakeVocab(params (string term, uint id, float idf)[] terms)
    {
        var termIds = terms.ToDictionary(t => t.term, t => t.id);
        var idfWeights = terms.ToDictionary(t => t.id, t => t.idf);
        var snapshot = new BM25VocabSnapshot(termIds, idfWeights, 100);
        return new StubVocabStore(snapshot);
    }

    [Fact]
    public async Task EmptyInput_ReturnsEmptyVector()
    {
        var sut = new SparseVectorizer(MakeVocab(("fever", 1, 1.5f)));
        var result = await sut.VectorizePassageAsync("");
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task WhitespaceInput_ReturnsEmptyVector()
    {
        var sut = new SparseVectorizer(MakeVocab(("fever", 1, 1.5f)));
        var result = await sut.VectorizeQueryAsync("   ");
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task KnownLatinTerm_ReturnsNonZeroWeight()
    {
        var sut = new SparseVectorizer(MakeVocab(("fever", 1, 2.0f)));
        var result = await sut.VectorizePassageAsync("The patient has fever and high fever");

        Assert.False(result.IsEmpty);
        Assert.True(result.Entries.ContainsKey(1));
        Assert.True(result.Entries[1] > 0f);
    }

    [Fact]
    public async Task CyrillicTerm_TokenisedAndWeighted()
    {
        // "треска" (Bulgarian: fever) should be tokenised by \p{L}+
        var sut = new SparseVectorizer(MakeVocab(("треска", 42, 1.8f)));
        var result = await sut.VectorizePassageAsync("Пациентът има треска.");

        Assert.False(result.IsEmpty);
        Assert.True(result.Entries.ContainsKey(42));
        Assert.True(result.Entries[42] > 0f);
    }

    [Fact]
    public async Task UnknownTerm_NotIncludedInEntries()
    {
        var sut = new SparseVectorizer(MakeVocab(("fever", 1, 1.5f)));
        var result = await sut.VectorizePassageAsync("headache nausea");
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task HigherFrequency_YieldsHigherWeight()
    {
        var sut = new SparseVectorizer(MakeVocab(("pain", 1, 1.5f)));
        var once = await sut.VectorizePassageAsync("pain");
        var twice = await sut.VectorizePassageAsync("pain pain pain");

        Assert.True(twice.Entries[1] > once.Entries[1]);
    }

    [Fact]
    public async Task EmptyVocab_ReturnsEmptyVector()
    {
        var sut = new SparseVectorizer(new StubVocabStore(
            new BM25VocabSnapshot(
                new Dictionary<string, uint>(),
                new Dictionary<uint, float>(),
                0)));
        var result = await sut.VectorizePassageAsync("fever");
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task SingleCharTokens_AreIgnored()
    {
        // Tokenizer skips length <= 1 tokens
        var sut = new SparseVectorizer(MakeVocab(("a", 1, 1.0f)));
        var result = await sut.VectorizePassageAsync("a b c");
        Assert.True(result.IsEmpty);
    }

    private sealed class StubVocabStore(BM25VocabSnapshot snapshot) : IBM25VocabStore
    {
        public Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);

        public Task<int> GetTotalDocumentsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task UpsertTermsAsync(IReadOnlyDictionary<string, int> termDfs, int totalDocs, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
