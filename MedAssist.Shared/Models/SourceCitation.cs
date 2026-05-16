namespace MedAssist.Shared.Models;

public sealed class SourceCitation
{
    public SourceType SourceType { get; init; }

    // Book source fields
    public string? BookTitle { get; init; }
    public string? Author { get; init; }
    public string? ChapterTitle { get; init; }
    public string? SectionTitle { get; init; }
    public int? PageStart { get; init; }
    public int? PageEnd { get; init; }

    // Web source fields
    public string? Url { get; init; }
    public string? SourceName { get; init; }
    public string? ArticleTitle { get; init; }
}

public enum SourceType
{
    Book,
    Web
}
