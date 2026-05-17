using MedAssist.Shared.Models;

namespace MedAssist.Data.Entities;

public sealed class BookEntity
{
    public int Id { get; set; }
    public string BookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Pending;
    public DateTimeOffset? IndexedAt { get; set; }
}
