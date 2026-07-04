using MedAssist.Shared.Models;

namespace MedAssist.Data.Entities;

/// <summary>
/// Persisted row backing the extraction tracker. One row per book (keyed by <see cref="BookDbId"/>,
/// the book's database id) so extract progress/outcome survives an app restart. On startup any row
/// left <see cref="ExtractionState.Running"/> is reconciled to <see cref="ExtractionState.Failed"/>
/// (the in-memory job queue does not survive a restart, so nothing is actually still running).
/// </summary>
public sealed class ExtractionStatusEntity
{
    public int BookDbId { get; set; }
    public string BookSlug { get; set; } = string.Empty;
    public ExtractionState State { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
}
