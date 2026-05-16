namespace MedAssist.Shared.Models;

public sealed record BM25VocabSnapshot(
    IReadOnlyDictionary<string, uint> TermIds,
    IReadOnlyDictionary<uint, float> IdfWeights,
    int TotalDocuments);
