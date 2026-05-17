## Context

The RAG pipeline indexes Bulgarian medical books as fixed-size chunks (~512 tokens) with no overlap. Retrieval uses hybrid BM25 + dense search. Two failure modes are observed:

1. **Script mismatch**: Books write disease names in Latin (e.g. "Graves", "Chiari") while queries arrive in Cyrillic transliterations ("Гравес", "Киари"). BM25 has zero recall; dense search alone cannot consistently outscore unrelated chunks.
2. **Fragmented retrieval**: A medical topic (e.g. Graves disease) may span 4-6 pages → 5-8 chunks. Retrieval returns at most a few of those chunks while the rest of the section is ignored, leaving the LLM with an incomplete picture.

Current chunker (`MarkdownChunker`) splits at heading boundaries and enforces `_minTokens=50` / `_maxTokens=512`. Consecutive chunks within a section have no overlap. The indexer stores exactly one Qdrant point per chunk, no section-level vector exists.

## Goals / Non-Goals

**Goals:**
- Ensure no content is silently cut at a chunk boundary (sliding window overlap)
- Give dense search a clean, high-signal target per section (section summary vectors)
- Pull the full context of a matched section into the reranker candidate pool (section-aware expansion)

**Non-Goals:**
- Transliteration or cross-script synonym mapping (no generic rule exists)
- Changing the hybrid search weights or RRF parameters
- Re-embedding existing stored vectors (only new points are added)

## Decisions

### D1 — Overlap implementation: suffix carry-forward

Append the last `OverlapChars` characters of chunk N as a prefix to chunk N+1 within the same section. Alternative (sliding window at sentence level) is more complex and the sentence splitter already exists in the chunker. Suffix carry-forward reuses the existing `SplitLargeChunk` path.

**Overlap size**: 512 chars (~128 tokens). Large enough to preserve a full sentence across boundaries; small enough to keep chunk count growth under 20%.

### D2 — Section summaries: same collection, `is_summary` flag

Store summary chunks in the existing `medical_books` Qdrant collection with `is_summary: true` payload field. Alternative (separate collection) requires a second Qdrant search per query and complicates the client. Summary chunks are excluded from the final answer sources by filtering on `is_summary` before returning to the LLM.

**Summary text**: `"{HeadingPath}\n\n{first 800 chars of first chunk in section}"`. Short enough to embed well, long enough to encode the topic.

### D3 — Section expansion: scroll by `chapter_title` + `section_title`

After each Qdrant search round in `RagPluginBase.GatherCandidatesAsync`, collect unique `(chapter_title, section_title)` pairs from non-summary candidates. For each pair scroll Qdrant (filter + no vector) up to 50 points. Merge into the candidate pool before reranking. Summary chunks are excluded from the merged pool.

**Why scroll instead of search**: Scrolling by payload filter retrieves all chunks in the section deterministically. A second dense search would miss chunks that score low on the query but are contextually adjacent.

### D4 — IsSummary on MedicalChunk

Add `bool IsSummary` to `MedicalChunk` (mapped to `is_summary` Qdrant payload field). The indexer sets it; `RagPluginBase` uses it to exclude summaries from the final top-5 returned to the LLM.

## Risks / Trade-offs

- [Qdrant point count grows ~15-25%] → Re-indexing takes slightly longer; storage is negligible for current book count.
- [Section expansion may pull many irrelevant chunks from a long section] → Mitigated by the cross-encoder reranker scoring all candidates; irrelevant chunks sink to the bottom.
- [Overlapping chunks introduce duplicate text in the answer] → The reranker deduplicates by score; LLM prompt already uses top-5 distinct chunks. Chunk deduplication by `BookId:ChunkIndex` still applies (overlap chunks have unique indices).
- [Section scroll adds latency per query] → Bounded by section size (≤50 chunks). Scroll is a single Qdrant call per unique section hit; typical queries hit 1-2 sections.

## Migration Plan

1. Update `MarkdownChunker` with overlap logic.
2. Update `MedicalChunk` with `IsSummary` field.
3. Update `BookIndexer` to generate summary chunks.
4. Update `RagPluginBase` with section expansion.
5. Drop and re-create the `medical_books` Qdrant collection (wipe + re-index the book).
6. Trigger re-indexing via `POST /api/admin/index?id=1`.
