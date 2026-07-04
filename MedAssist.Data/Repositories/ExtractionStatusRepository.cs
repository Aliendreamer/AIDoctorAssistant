using MedAssist.Data.Entities;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Repositories;

/// <summary>
/// Durable store for Marker extraction status (audit P1-8 follow-up). Replaces the previous
/// in-memory <c>ConcurrentDictionary</c> so extract progress/outcome survives an app restart.
/// </summary>
public sealed class ExtractionStatusRepository(MedAssistDbContext db)
{
    private readonly MedAssistDbContext _db = db;

    /// <summary>
    /// Starts an extraction for the book unless one is already running. When a prior Done/Failed
    /// record exists it is reset to a fresh Running record. Returns whether this call started it and
    /// the resulting (or in-flight) entry.
    /// </summary>
    public async Task<ExtractionStartOutcome> TryStartAsync(int bookDbId, string bookSlug, CancellationToken cancellationToken = default)
    {
        var existing = await _db.ExtractionStatuses.FindAsync([bookDbId], cancellationToken);
        if (existing is not null && existing.State == ExtractionState.Running)
        {
            return new ExtractionStartOutcome(false, Map(existing));
        }

        if (existing is null)
        {
            existing = new ExtractionStatusEntity { BookDbId = bookDbId };
            _db.ExtractionStatuses.Add(existing);
        }

        existing.BookSlug = bookSlug;
        existing.State = ExtractionState.Running;
        existing.StartedAt = DateTimeOffset.UtcNow;
        existing.CompletedAt = null;
        existing.Error = null;

        await _db.SaveChangesAsync(cancellationToken);
        return new ExtractionStartOutcome(true, Map(existing));
    }

    public async Task MarkDoneAsync(int bookDbId, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(bookDbId, cancellationToken);
        entity.State = ExtractionState.Done;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.Error = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(int bookDbId, string error, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(bookDbId, cancellationToken);
        entity.State = ExtractionState.Failed;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.Error = error;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsRunningAsync(int bookDbId, CancellationToken cancellationToken = default) =>
        await _db.ExtractionStatuses
            .AnyAsync(e => e.BookDbId == bookDbId && e.State == ExtractionState.Running, cancellationToken);

    public async Task<ExtractionEntry?> GetAsync(int bookDbId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ExtractionStatuses.AsNoTracking()
            .FirstOrDefaultAsync(e => e.BookDbId == bookDbId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ExtractionEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Order client-side: the table holds one small row per book, and SQLite (the test provider)
        // cannot ORDER BY a DateTimeOffset — sorting in memory is identical on both providers.
        var entities = await _db.ExtractionStatuses.AsNoTracking().ToListAsync(cancellationToken);
        return entities.OrderBy(e => e.StartedAt).Select(Map).ToList();
    }

    /// <summary>
    /// Marks every row still <see cref="ExtractionState.Running"/> as failed. Called once on startup:
    /// the in-memory job queue does not survive a restart, so any persisted "running" extraction was
    /// interrupted and is not actually in flight. Returns the number reconciled.
    /// </summary>
    public async Task<int> MarkInterruptedRunningAsFailedAsync(CancellationToken cancellationToken = default)
    {
        var stuck = await _db.ExtractionStatuses
            .Where(e => e.State == ExtractionState.Running)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var entity in stuck)
        {
            entity.State = ExtractionState.Failed;
            entity.CompletedAt = now;
            entity.Error = "Interrupted by shutdown";
        }

        await _db.SaveChangesAsync(cancellationToken);
        return stuck.Count;
    }

    private async Task<ExtractionStatusEntity> RequireAsync(int bookDbId, CancellationToken cancellationToken)
    {
        var entity = await _db.ExtractionStatuses.FindAsync([bookDbId], cancellationToken);
        return entity ?? throw new InvalidOperationException($"Book {bookDbId} extraction was not started.");
    }

    private static ExtractionEntry Map(ExtractionStatusEntity e) =>
        new(e.BookDbId, e.BookSlug, e.State, e.StartedAt, e.CompletedAt, e.Error);
}
