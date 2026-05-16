## Why

RAG retrieval quality degrades for queries where the initial hybrid search returns low-confidence chunks — specifically when Cyrillic/Latin script mismatches, phrase-level BM25 misses, or boundary-split content causes the cross-encoder to score all candidates below a meaningful threshold. A single-pass retrieval offers no recovery path. Adding a configurable iterative loop allows the pipeline to widen its search automatically when confidence is low, without requiring an LLM.

## What Changes

- `ICrossEncoderReranker.RerankAsync` now returns `IReadOnlyList<ScoredChunk>` (chunk + raw logit) instead of `IReadOnlyList<MedicalChunk>` — **BREAKING** for any implementation of that interface
- New `ScoredChunk` value type in `MedAssist.Shared.Models`
- New `RagOptions` model (`ConfidenceThreshold`, `MaxIterations`) bound from `Rag:*` configuration
- `RagPluginBase.ExecuteSearchAsync` runs up to 5 progressively wider fallback search passes when the top reranker score is below `ConfidenceThreshold`
- `MedicalDictionaryService.ExpandQueryAsync` extracts individual keywords from the query phrase as additional standalone search terms (fixes Cyrillic phrase → Latin word mismatch in BM25)
- 7 new unit tests covering loop termination, iteration cap, empty-store behaviour, and result shape

## Capabilities

### New Capabilities

- `iterative-rag-retrieval`: Confidence-gated iterative retrieval loop that widens search parameters (topK, language filter, term granularity) when the initial reranker score falls below a configurable threshold

### Modified Capabilities

- `rag-query-expansion`: `ExpandQueryAsync` now also adds individual keywords from the query phrase as independent search terms, not just whole-phrase dictionary lookups

## Impact

- `MedAssist.Shared`: new models `ScoredChunk`, `RagOptions`; `ICrossEncoderReranker` signature changed
- `MedAssist.AI`: `CrossEncoderReranker`, `RagPluginBase`, all three plugins, `KernelFactory` updated
- `MedAssist.Web`: `ServiceCollectionExtensions` binds `RagOptions` from config; `appsettings.shared.json` gains `Rag` section
- No API surface changes; no database migrations; no new external dependencies
