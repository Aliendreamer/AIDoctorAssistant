namespace MedAssist.Shared.Models;

public sealed record QueryResult
{
    public string Answer { get; init; } = string.Empty;
    public IReadOnlyList<SourceCitation> Sources { get; init; } = [];
}
