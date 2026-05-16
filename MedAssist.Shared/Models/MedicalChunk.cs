namespace MedAssist.Shared.Models;

public sealed class MedicalChunk
{
    public string BookId { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string ChapterTitle { get; init; } = string.Empty;
    public string SectionTitle { get; init; } = string.Empty;
    public int PageStart { get; init; }
    public int PageEnd { get; init; }
    public int ChunkIndex { get; init; }
    public ContentType ContentType { get; init; }
    public string Text { get; init; } = string.Empty;
    public IReadOnlyList<string> IcdCodes { get; init; } = [];
}

public enum ContentType
{
    Text,
    Table,
    List
}
