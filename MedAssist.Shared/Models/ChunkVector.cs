namespace MedAssist.Shared.Models;

/// <summary>
/// A chunk together with its computed dense and sparse vectors, ready to upsert. Lets the indexer
/// accumulate a batch and upsert many points in one round-trip instead of one call per chunk.
/// </summary>
public sealed record ChunkVector(MedicalChunk Chunk, float[] DenseVector, SparseVector SparseVector);
