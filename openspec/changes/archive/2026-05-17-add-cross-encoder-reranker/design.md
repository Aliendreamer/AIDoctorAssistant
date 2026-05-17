## Context

The current RAG pipeline performs hybrid search (dense + BM25 via Qdrant RRF) and expands queries through `MedicalDictionary`. After deduplication, candidates are blindly truncated to 10 and fed to the LLM. There is no quality signal distinguishing a highly relevant chunk from a marginally matching one. A cross-encoder model reads both query and passage together and produces a much more accurate relevance score than bi-encoder cosine similarity.

## Goals / Non-Goals

**Goals:**
- Integrate a cross-encoder reranker as a pure post-processing step inside `RagPluginBase`
- Keep the reranker self-hosted (ONNX, no external API)
- Reuse the existing `ModelInitializer` download pattern and `Microsoft.ML.OnnxRuntime` dependency
- Reduce LLM context from 10 unscored chunks to 5 reranked chunks

**Non-Goals:**
- Real-time/streaming reranking
- GPU acceleration (CPU ONNX is sufficient for ~15 candidates)
- Reranking web search results (SearXNG results are already filtered by domain)

## Decisions

### Model: `cross-encoder/ms-marco-MiniLM-L-6-v2`
Chosen over heavier models (e.g., `ms-marco-MiniLM-L-12-v2`) because it runs ~2× faster on CPU with minimal accuracy loss on passage reranking. Multilingual queries are handled by the embedder expansion step upstream; the reranker operates on English-normalised passages. ONNX export available directly from HuggingFace.

**Alternative considered**: `cross-encoder/mmarco-mMiniLMv2-L12-H384-v1` (truly multilingual cross-encoder) — rejected for now because the Bulgarian medical book corpus is small and reranking is mainly a precision boost on English content; revisit if BG result quality is poor.

### Integration point: `RagPluginBase.ExecuteSearchAsync` after dedup
The reranker sits between `DistinctBy` and `Take(N)`. The original query (not the expanded terms) is used as the reranker query — the cross-encoder must see what the user actually asked, not a synonym.

### Interface: `ICrossEncoderReranker`
```
Task<IReadOnlyList<MedicalChunk>> RerankAsync(string query, IReadOnlyList<MedicalChunk> candidates, CancellationToken ct)
```
Thin interface so the reranker is swappable in tests and future upgrades.

### Tokenization: Reuse `Microsoft.ML.Tokenizers` (already in codebase)
The same `TokenizerFactory` used by the embedder handles WordPiece/SentencePiece tokenization. Cross-encoder input format: `[CLS] query [SEP] passage [SEP]`, truncated to 512 tokens.

### Config: `Models:RerankerPath` in `appsettings.shared.json`
Mirrors the existing `Models:Path` pattern. Default: `models/ms-marco-MiniLM-L-6-v2`.

## Risks / Trade-offs

- **Latency +50–200ms**: Running ONNX inference on 10–15 candidates serially adds latency. Mitigation: run candidates in parallel using `Parallel.ForEachAsync` with degree = `Environment.ProcessorCount / 2`.
- **Model download on first start**: ~90 MB cross-encoder ONNX. Mitigation: same eager-download pattern as the embedder via `ModelInitializer`; Docker pre-warm handles it.
- **English-only model on BG passages**: Reranker may score Bulgarian passages less accurately. Mitigation: accept this for v1; BG results still benefit from upstream hybrid search quality.

## Open Questions

- Should `topK` after reranking (currently 5) be configurable per plugin type, or fixed globally?
- Do we want a reranker score threshold (drop chunks below 0.0) or always take top-N regardless?
