using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface ISparseVectorizer
{
    Task<SparseVector> VectorizePassageAsync(string text, CancellationToken cancellationToken = default);
    Task<SparseVector> VectorizeQueryAsync(string text, CancellationToken cancellationToken = default);
}
