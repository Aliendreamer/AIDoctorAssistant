using MedAssist.Shared.Constants;

namespace MedAssist.Data.Entities;

public sealed class IngestionCheckpointEntity
{
    public string BookId { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int IndexedChunks { get; set; }
    public int LastChunkIndex { get; set; }
    public string Status { get; set; } = IngestionStatus.InProgress;
    public DateTimeOffset UpdatedAt { get; set; }
}
