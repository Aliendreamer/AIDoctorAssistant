using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(
        MedicalChunk chunk,
        float[] denseVector,
        SparseVector sparseVector,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicalChunk>> SearchAsync(
        float[] denseQueryVector,
        SparseVector? sparseQueryVector,
        LanguageFilter language,
        IReadOnlyList<string>? bookIds,
        int topK = 5,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicalChunk>> ScrollSectionAsync(
        string chapterTitle,
        string sectionTitle,
        string bookId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task DeleteCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes only the points belonging to a single book (matched on the BookId payload),
    /// leaving every other book's vectors intact. Use this for per-book re-indexing —
    /// never <see cref="DeleteCollectionAsync"/>, which drops the whole corpus.
    /// </summary>
    Task DeleteByBookAsync(string bookId, CancellationToken cancellationToken = default);
}
