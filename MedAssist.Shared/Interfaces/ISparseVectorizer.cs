using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface ISparseVectorizer
{
    Task<SparseVector> VectorizePassageAsync(string text, CancellationToken cancellationToken = default);
    Task<SparseVector> VectorizeQueryAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Every lowercase word in the indexed books with its IDF, so a lower weight is a commoner word.</summary>
    Task<IReadOnlyDictionary<string, float>> GetWordWeightsAsync(CancellationToken cancellationToken = default);
}
