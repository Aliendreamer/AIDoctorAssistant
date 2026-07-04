using MedAssist.AI.Ingestion;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

// Guards P0-1: a single-book force re-index must delete ONLY that book's vectors. It must never
// drop the whole Qdrant collection (which would erase every other book). The previous code called
// DeleteCollectionAsync + truncated the shared BM25 tables for one book — silent, total data loss.
public sealed class BookReindexCleanerTests
{
    private sealed class RecordingVectorStore : IVectorStore
    {
        public int DeleteCollectionCalls { get; private set; }
        public List<string> DeleteByBookCalls { get; } = [];

        public Task UpsertAsync(MedicalChunk chunk, float[] denseVector, SparseVector sparseVector, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MedicalChunk>> SearchAsync(float[] denseQueryVector, SparseVector? sparseQueryVector, LanguageFilter language, IReadOnlyList<string>? bookIds, int topK = 5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MedicalChunk>>([]);

        public Task<IReadOnlyList<MedicalChunk>> ScrollSectionAsync(string chapterTitle, string sectionTitle, string bookId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MedicalChunk>>([]);

        public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
        {
            DeleteCollectionCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteByBookAsync(string bookId, CancellationToken cancellationToken = default)
        {
            DeleteByBookCalls.Add(bookId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ForceReindex_DeletesOnlyThatBooksVectors_NotTheWholeCollection()
    {
        var store = new RecordingVectorStore();
        var sut = new BookReindexCleaner(store);

        await sut.ClearBookAsync("book-b", force: true);

        Assert.Equal(["book-b"], store.DeleteByBookCalls);
        Assert.Equal(0, store.DeleteCollectionCalls);
    }

    [Fact]
    public async Task NonForceReindex_DeletesNothing()
    {
        var store = new RecordingVectorStore();
        var sut = new BookReindexCleaner(store);

        await sut.ClearBookAsync("book-b", force: false);

        Assert.Empty(store.DeleteByBookCalls);
        Assert.Equal(0, store.DeleteCollectionCalls);
    }
}
