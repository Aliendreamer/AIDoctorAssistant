using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

/// <summary>
/// Persists resumable ingestion checkpoints. Abstracted in Shared so the AI indexer can depend on it
/// without referencing the EF data layer (audit P2-13).
/// </summary>
public interface ICheckpointRepository
{
    Task<IngestionCheckpoint?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken = default);
    Task UpsertAsync(IngestionCheckpoint checkpoint, CancellationToken cancellationToken = default);
    Task DeleteAsync(string bookId, CancellationToken cancellationToken = default);
}
