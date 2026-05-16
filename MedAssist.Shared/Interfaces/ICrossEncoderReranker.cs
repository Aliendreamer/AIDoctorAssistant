using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface ICrossEncoderReranker
{
    Task<IReadOnlyList<ScoredChunk>> RerankAsync(
        string query,
        IReadOnlyList<MedicalChunk> candidates,
        CancellationToken cancellationToken = default);
}
