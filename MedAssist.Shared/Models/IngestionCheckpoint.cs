namespace MedAssist.Shared.Models;

/// <summary>
/// A resumable ingestion checkpoint for a book: how far indexing has progressed. Lives in Shared so
/// both the AI indexer (via <c>ICheckpointRepository</c>) and the Data implementation can reference
/// it without the AI layer depending on EF (audit P2-13).
/// </summary>
public sealed record IngestionCheckpoint(
    string BookId,
    int TotalChunks,
    int IndexedChunks,
    int LastChunkIndex,
    BookStatus Status,
    DateTimeOffset UpdatedAt);
