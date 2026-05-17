using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Repositories;

public sealed record IngestionCheckpoint(
    string BookId,
    int TotalChunks,
    int IndexedChunks,
    int LastChunkIndex,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed class CheckpointRepository
{
    private readonly IDbContextFactory<MedAssistDbContext> _dbFactory;

    public CheckpointRepository(IDbContextFactory<MedAssistDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task UpsertAsync(IngestionCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
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
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.IngestionCheckpoints.FindAsync([bookId], cancellationToken);
        if (entity is not null)
        {
            db.IngestionCheckpoints.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IngestionCheckpoint?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
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
