using MedAssist.Shared.Models;

namespace MedAssist.Data.Entities;

public sealed class IngestionCheckpointEntity
{
    public string BookId { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int IndexedChunks { get; set; }
    public int LastChunkIndex { get; set; }
    public BookStatus Status { get; set; } = BookStatus.InProgress;
    public DateTimeOffset UpdatedAt { get; set; }
}
