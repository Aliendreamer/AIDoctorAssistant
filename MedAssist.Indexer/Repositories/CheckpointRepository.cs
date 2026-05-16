using MedAssist.Data;
using MedAssist.Data.Entities;

namespace MedAssist.Indexer.Repositories;

public sealed record IngestionCheckpoint(
    string BookId,
    int TotalChunks,
    int IndexedChunks,
    int LastChunkIndex,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed class CheckpointRepository
{
    private readonly MedAssistDbContext _db;

    public CheckpointRepository(MedAssistDbContext db) => _db = db;

    public async Task UpsertAsync(IngestionCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var existing = await _db.IngestionCheckpoints.FindAsync([checkpoint.BookId], cancellationToken);
        if (existing is not null)
        {
            existing.TotalChunks = checkpoint.TotalChunks;
            existing.IndexedChunks = checkpoint.IndexedChunks;
            existing.LastChunkIndex = checkpoint.LastChunkIndex;
            existing.Status = checkpoint.Status;
            existing.UpdatedAt = checkpoint.UpdatedAt;
        }
        else
        {
            _db.IngestionCheckpoints.Add(new IngestionCheckpointEntity
            {
                BookId = checkpoint.BookId,
                TotalChunks = checkpoint.TotalChunks,
                IndexedChunks = checkpoint.IndexedChunks,
                LastChunkIndex = checkpoint.LastChunkIndex,
                Status = checkpoint.Status,
                UpdatedAt = checkpoint.UpdatedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IngestionCheckpoint?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.IngestionCheckpoints.FindAsync([bookId], cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new IngestionCheckpoint(
            entity.BookId,
            entity.TotalChunks,
            entity.IndexedChunks,
            entity.LastChunkIndex,
            entity.Status,
            entity.UpdatedAt);
    }
}
