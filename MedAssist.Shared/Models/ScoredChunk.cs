namespace MedAssist.Shared.Models;

public readonly record struct ScoredChunk(MedicalChunk Chunk, float Score);
