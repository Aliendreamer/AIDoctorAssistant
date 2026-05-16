namespace MedAssist.Data.Entities;

public sealed class IngestionCheckpointEntity
{
    public string BookId { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int IndexedChunks { get; set; }
    public int LastChunkIndex { get; set; }
    public string Status { get; set; } = "in_progress";
    public DateTimeOffset UpdatedAt { get; set; }
}
