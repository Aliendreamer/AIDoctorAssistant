## Why

After RRF fusion merges results across expanded query terms, chunks are deduplicated and blindly truncated to top-10 with no quality signal — whatever RRF rank happened to produce becomes the LLM context. A cross-encoder reranker scores each (query, chunk) pair jointly, dramatically improving the precision of what gets passed to the model.

## What Changes

- Add `ICrossEncoderReranker` interface and ONNX-based implementation loading `ms-marco-MiniLM-L-6-v2` (or equivalent multilingual model)
- Add `CrossEncoderReranker` to `MedAssist.AI` project using `Microsoft.ML.OnnxRuntime`
- Extend `ModelInitializer` to download reranker model files from HuggingFace alongside the embedder
- Replace `DistinctBy + Take(10)` in `RagPluginBase.ExecuteSearchAsync` with reranker-scored sort + Take(5)
- Register reranker in DI and wire into `RagPluginBase`
- Add reranker model path to `config/appsettings.shared.json` under `Models`
- Add reranker-specific words to `cspell.json`

## Capabilities

### New Capabilities
- `cross-encoder-reranking`: Score candidate chunks against the original query using a cross-encoder model loaded via ONNX; return top-N by score replacing the current blind truncation

### Modified Capabilities
- `rag-retrieval`: Retrieval pipeline gains a reranking step after RRF fusion — the effective topK passed to the LLM changes from 10 (unscored) to 5 (reranked)

## Impact

- `MedAssist.AI`: new `Reranker/` folder, updated `RagPluginBase`, updated `ModelInitializer`, updated DI extensions
- `MedAssist.Web`: no changes (reranker is internal to the AI layer)
- New NuGet dependency: `Microsoft.ML.OnnxRuntime` (already present for embedder — verify shared)
- New model files (~90 MB cross-encoder ONNX): downloaded at runtime, gitignored
- Latency: adds ~50–200ms per query for reranking 10–15 candidates on CPU
