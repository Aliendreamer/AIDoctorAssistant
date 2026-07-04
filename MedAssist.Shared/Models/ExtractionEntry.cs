namespace MedAssist.Shared.Models;

public enum ExtractionState
{
    Running,
    Done,
    Failed
}

/// <summary>
/// The persisted status of a single book's Marker (PDF→Markdown) extraction. Keyed by the book's
/// database id so status survives a restart (audit P1-8 durable-ingestion follow-up).
/// </summary>
public sealed record ExtractionEntry(
    int BookDbId,
    string BookId,
    ExtractionState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

/// <summary>
/// Result of attempting to start an extraction: <see cref="Started"/> is false when an extraction
/// for that book is already <see cref="ExtractionState.Running"/>, in which case <see cref="Entry"/>
/// is the in-flight one.
/// </summary>
public sealed record ExtractionStartOutcome(bool Started, ExtractionEntry Entry);
