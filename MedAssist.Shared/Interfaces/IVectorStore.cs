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
}
