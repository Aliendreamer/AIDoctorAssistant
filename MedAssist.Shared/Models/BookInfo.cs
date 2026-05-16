namespace MedAssist.Shared.Models;

public sealed class BookInfo
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string Edition { get; init; } = string.Empty;
    public int TotalChunks { get; init; }
    public BookStatus Status { get; init; }
    public DateTimeOffset? IndexedAt { get; init; }
}

public enum BookStatus
{
    Pending,
    InProgress,
    Indexed
}
