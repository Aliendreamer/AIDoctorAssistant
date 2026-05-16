namespace MedAssist.Shared.Models;

public sealed record SparseVector
{
    public static SparseVector Empty { get; } = new();

    public IReadOnlyDictionary<uint, float> Entries { get; init; } = new Dictionary<uint, float>();

    public bool IsEmpty => Entries.Count == 0;
}
