using MedAssist.AI.Ingestion;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

// VocabularyBuilder is the Pass-A accumulator: it counts, per book, how many chunks contain each
// term (document frequency) and how many chunks the book has, then forwards that contribution to
// the store. These tests pin the accumulation (per-chunk DF dedup, chunk count) and that FlushAsync
// forwards exactly what it accumulated for the given book.
public sealed class VocabularyBuilderTests
{
    private sealed class CapturingStore : IBM25VocabStore
    {
        public string? BookId { get; private set; }
        public IReadOnlyDictionary<string, int>? Terms { get; private set; }
        public int ChunkCount { get; private set; }
        public int Calls { get; private set; }

        public Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BM25VocabSnapshot(new Dictionary<string, uint>(), new Dictionary<uint, float>(), 0));

        public Task ApplyBookContributionAsync(
            string bookId,
            IReadOnlyDictionary<string, int> termDocumentFrequencies,
            int chunkCount,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            BookId = bookId;
            Terms = new Dictionary<string, int>(termDocumentFrequencies);
            ChunkCount = chunkCount;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Flush_ForwardsPerTermDocumentFrequencyAndChunkCount()
    {
        var store = new CapturingStore();
        var sut = new VocabularyBuilder(store);

        sut.AddChunk("fever cough fever");   // chunk 1: {fever, cough}
        sut.AddChunk("fever headache");      // chunk 2: {fever, headache}

        await sut.FlushAsync("book-a");

        Assert.Equal("book-a", store.BookId);
        Assert.Equal(2, store.ChunkCount);
        Assert.Equal(2, store.Terms!["fever"]);     // present in both chunks
        Assert.Equal(1, store.Terms!["cough"]);      // once, and deduped within its chunk
        Assert.Equal(1, store.Terms!["headache"]);
    }

    [Fact]
    public async Task Flush_ResetsState_SecondFlushIsEmpty()
    {
        var store = new CapturingStore();
        var sut = new VocabularyBuilder(store);

        sut.AddChunk("fever");
        await sut.FlushAsync("book-a");

        await sut.FlushAsync("book-b");

        Assert.Equal("book-b", store.BookId);
        Assert.Equal(0, store.ChunkCount);
        Assert.Empty(store.Terms!);
    }

    [Fact]
    public async Task Flush_ForwardsEvenWithNoTerms_SoAReindexToEmptyClearsContribution()
    {
        var store = new CapturingStore();
        var sut = new VocabularyBuilder(store);

        await sut.FlushAsync("book-a");

        Assert.Equal(1, store.Calls);
        Assert.Equal("book-a", store.BookId);
        Assert.Equal(0, store.ChunkCount);
        Assert.Empty(store.Terms!);
    }
}
