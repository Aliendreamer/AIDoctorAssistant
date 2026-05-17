## Why

Hybrid BM25+dense retrieval misses relevant content when a disease name is written in Latin script in the book but queried in Cyrillic transliteration (e.g. "Гравес" vs "Graves"), and when a topic spans multiple chunks retrieval may return only one fragment while the rest of the section is ignored. These gaps degrade RAG answer quality for a significant share of real medical queries.

## What Changes

- **Sliding window chunking**: `MarkdownChunker` produces overlapping chunks (128-token / ~512-char overlap between consecutive chunks in the same section) so no content is cut at an arbitrary boundary.
- **Section summary vectors**: During indexing a lightweight "section summary" chunk (heading path + first paragraph) is generated per section alongside regular chunks, stored with `is_summary: true` in the Qdrant payload. Dense search has a clean, topic-rich target to hit.
- **Section-aware candidate expansion**: After Qdrant returns initial candidates, `RagPluginBase` scrolls for all chunks sharing the same `chapter_title` + `section_title` as any hit, adds them to the candidate pool, then reranks the full set. A single hit on the right section pulls in the complete context.

## Capabilities

### New Capabilities

- `chunk-overlap`: Overlapping chunk generation in `MarkdownChunker` with configurable overlap size.
- `section-summary-index`: Section-level summary chunk generation and storage during ingestion.
- `section-aware-retrieval`: Post-retrieval section expansion in `RagPluginBase` before reranking.

### Modified Capabilities

- `iterative-rag-retrieval`: Candidate gathering now includes section expansion step after each Qdrant search round.
- `rag-query-expansion`: No requirement changes — implementation unchanged.

## Impact

- `MedAssist.AI/Ingestion/MarkdownChunker.cs` — overlap logic added
- `MedAssist.AI/Ingestion/BookIndexer.cs` — section summary chunk generation
- `MedAssist.AI/Plugins/RagPluginBase.cs` — section expansion after gather
- `MedAssist.Shared/Models/MedicalChunk.cs` — `IsSummary` flag
- Re-indexing required after chunker and indexer changes (~5-10 min for current book)
- Qdrant point count will increase (~10-20% from overlap + summaries)
