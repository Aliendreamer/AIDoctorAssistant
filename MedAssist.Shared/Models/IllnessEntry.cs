namespace MedAssist.Shared.Models;

public sealed class IllnessEntry
{
    public Guid Id { get; init; }
    public string IcdCode { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameBg { get; init; } = string.Empty;
    public IReadOnlyList<string> Aliases { get; init; } = [];
}
