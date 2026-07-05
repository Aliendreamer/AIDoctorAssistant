## Why

A performance pass over the query hot path and ingestion, guided by the .NET, Qdrant, and EF Core
optimization skills. The per-query path is dominated by ONNX embedding/reranking + the LLM, but
several avoidable inefficiencies existed in the surrounding glue, the vector store, and the data
layer — and the vector index was not configured to scale as the book corpus grows.

## What Changes

- **EF Core**: `AsNoTracking()` on read-only queries (dictionary expansion — per-query hot path,
  book catalog/lookup, chat history, user find/list); load-to-mutate queries stay tracked.
- **Qdrant — payload indexes**: keyword indexes on `BookId`/`Language`/`ChapterTitle`/`SectionTitle`
  and a bool index on `IsSummary`, so filtered search and the per-query `ScrollSection` use an index
  instead of a full-collection scan.
- **Qdrant — batch upserts**: `IVectorStore.UpsertBatchAsync` (default falls back to per-point);
  `BookIndexer` upserts one batch per checkpoint instead of one call per chunk.
- **Qdrant — int8 quantization**: dense vectors int8 scalar-quantized (codes in RAM) with the
  originals on disk and search-time rescoring, cutting vector RAM ~4× at scale while preserving
  accuracy. Applied at collection creation and back-filled onto the existing collection.
- **.NET**: value-tuple `DistinctBy` keys on the query path (alloc-free, collision-proof);
  `MedicalDictionaryService` stopwords → `FrozenSet`.

## Capabilities

### New Capabilities
- `query-performance`: Baseline performance requirements for the query hot path, the ingestion
  vector upserts, and the Qdrant vector-index configuration.

### Modified Capabilities

## Impact

- **Code**: `QdrantVectorStore`, `IVectorStore` (+`ChunkVector`), `BookIndexer`, `RagPluginBase`,
  `CandidateRetriever`, `MedicalDictionaryService`, `BookCatalogService`, `BookRepository`,
  `ChatHistoryRepository`, `UserRepository`.
- **Runtime**: filtered searches and section-expansion are index-backed; ingestion issues far fewer
  Qdrant round-trips; the dense index is int8-quantized with on-disk originals + rescoring.
- Build 0/0; 163 tests pass. No behavior change to results (verified: a live query returns cited
  answers).
