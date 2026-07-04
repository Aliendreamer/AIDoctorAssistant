using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Data.Repositories;

public sealed class CheckpointRepository(MedAssistDbContext db) : ICheckpointRepository
{
    public async Task UpsertAsync(IngestionCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var existing = await db.IngestionCheckpoints.FindAsync([checkpoint.BookId], cancellationToken);
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
            db.IngestionCheckpoints.Add(new IngestionCheckpointEntity
            {
                BookId = checkpoint.BookId,
                TotalChunks = checkpoint.TotalChunks,
                IndexedChunks = checkpoint.IndexedChunks,
                LastChunkIndex = checkpoint.LastChunkIndex,
                Status = checkpoint.Status,
                UpdatedAt = checkpoint.UpdatedAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string bookId, CancellationToken cancellationToken = default)
    {
        var entity = await db.IngestionCheckpoints.FindAsync([bookId], cancellationToken);
        if (entity is not null)
        {
            db.IngestionCheckpoints.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IngestionCheckpoint?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken = default)
    {
        var entity = await db.IngestionCheckpoints.FindAsync([bookId], cancellationToken);
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
