namespace MedAssist.Shared.Models;

public sealed record QueryResult
{
    public string Answer { get; init; } = string.Empty;
    public IReadOnlyList<SourceCitation> Sources { get; init; } = [];

    // Set when retrieval confidence is too low to bother retrying — caller should fall back to web search.
    public bool RequiresWebFallback { get; init; }
}
