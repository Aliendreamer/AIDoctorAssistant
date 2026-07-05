namespace MedAssist.Web.Options;

public sealed class ModelsOptions
{
    public string Path { get; init; } = "models";
    public string RerankerPath { get; init; } = "models/ms-marco-MiniLM-L-6-v2";
    public int InitializerTimeoutMinutes { get; init; } = 15;

    /// <summary>
    /// ONNX intra-op thread count for the embedder + reranker sessions. 0 (default) leaves the ONNX
    /// Runtime default (all cores) — best for single-query latency. Set a lower value to reduce
    /// per-inference core contention under concurrent load (audit P2-20; tune with profiling).
    /// </summary>
    public int OnnxIntraOpNumThreads { get; init; } = 0;
}
