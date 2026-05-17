## 1. Shared Model

- [x] 1.1 Add `bool IsSummary` property to `MedAssist.Shared/Models/MedicalChunk.cs`

## 2. Sliding Window Overlap (MarkdownChunker)

- [x] 2.1 Add `int OverlapChars = 512` parameter to `MarkdownChunker` constructor
- [x] 2.2 In `Chunk()`, after producing each chunk within a section, store the last `OverlapChars` chars as carry-forward state
- [x] 2.3 Prepend the carry-forward prefix to the next chunk's text when it belongs to the same heading path
- [x] 2.4 Reset carry-forward state when a new heading section begins
- [x] 2.5 Ensure overlap prefix does not push a chunk over `_maxTokens` (split further if needed)

## 3. Section Summary Generation (BookIndexer)

- [x] 3.1 After chunking a book, group chunks by unique heading path
- [x] 3.2 For each group, create a summary `MedicalChunk` with `IsSummary = true`, text = `"{HeadingPath}\n\n{first 800 chars of first chunk}"`
- [x] 3.3 Store `is_summary` boolean in the Qdrant payload when upserting points
- [x] 3.4 Map `is_summary` from Qdrant payload back to `MedicalChunk.IsSummary` in the vector store read path

## 4. Section-Aware Candidate Expansion (RagPluginBase)

- [x] 4.1 After each `GatherCandidatesAsync` call, extract unique `(chapter_title, section_title)` pairs from non-summary candidates
- [x] 4.2 For each unique pair with a non-empty section title, scroll Qdrant by payload filter (up to 50 chunks, `is_summary: false`)
- [x] 4.3 Also trigger section expansion from summary chunk hits (use their section pair, then exclude the summary from the pool)
- [x] 4.4 Merge scrolled chunks into the candidate pool; deduplicate by `{BookId}:{ChunkIndex}`
- [x] 4.5 Before passing top-K to the LLM, filter out any chunks with `IsSummary = true`

## 5. Vector Store / Qdrant Client

- [x] 5.1 Add `is_summary` field to the Qdrant upsert payload in `QdrantVectorStore` (or wherever points are written)
- [x] 5.2 Add `ScrollSectionAsync(string chapterTitle, string sectionTitle, string bookId, int limit)` method to the vector store / retrieval service to support section expansion scroll

## 6. Re-index

- [x] 6.1 Drop and recreate the `medical_books` Qdrant collection to clear old points
- [ ] 6.2 Trigger re-indexing via `POST /api/admin/index?id=1` and confirm completion
- [ ] 6.3 Verify point count in Qdrant is higher than original 800 (overlap + summaries)

## 7. Verification

- [x] 7.1 Run `dotnet test` — all existing tests pass
- [ ] 7.2 Query "болест на Гравес" and confirm thyroid section chunks appear in results
- [ ] 7.3 Run the 12 Bulgarian query set and compare hit count vs baseline (4 clear hits)
