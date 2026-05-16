namespace MedAssist.Shared.Models;

public sealed class QueryRequest
{
    public string Query { get; init; } = string.Empty;
    public QueryType QueryType { get; init; } = QueryType.Disease;
    public LanguageFilter Language { get; init; } = LanguageFilter.Both;
    public IReadOnlyList<string>? BookIds { get; init; }
    public bool WebSearchEnabled { get; init; }
}

public enum QueryType
{
    Symptoms,
    Disease,
    Treatment
}

public enum LanguageFilter
{
    Both,
    English,
    Bulgarian
}
