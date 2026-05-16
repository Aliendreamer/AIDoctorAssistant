using MedAssist.Shared.Constants;

namespace MedAssist.Data.Entities;

public sealed class BookEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public string Status { get; set; } = IngestionStatus.Pending;
    public DateTimeOffset? IndexedAt { get; set; }
}
