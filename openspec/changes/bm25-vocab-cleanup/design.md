## Context

The BM25 sparse vectorizer relies on a `bm25_vocab` table storing per-term document frequencies and an IDF snapshot. Two bugs were discovered after re-indexing a 844-chunk medical book:

1. **Base64 image pollution**: Docling embeds PDF images as `![alt](data:image/png;base64,<blob>)` in markdown. The BM25 tokenizer regex `\p{L}+` matches letter sequences inside base64 strings, producing ~1.5M garbage "terms" (e.g., `kggoaaaansuheugaaasqaaackcaiaaacm`) vs. ~50K real medical terms.

2. **Full-table timeout**: `total_documents` is stored on every `bm25_vocab` row. Updating it after indexing runs `UPDATE bm25_vocab SET total_documents = X` over 1.5M rows — exceeding the 30-second EF Core command timeout and failing the entire indexing job.

## Goals / Non-Goals

**Goals:**
- Strip base64 image content from markdown before any processing (chunking, embedding, BM25)
- Remove `total_documents` from `bm25_vocab` rows; store it in a dedicated single-row `bm25_stats` table
- Migrate existing data: truncate corrupt vocab, reset stats to zero (re-index required)
- Fix `force=true` re-index to also clear vocab and stats

**Non-Goals:**
- Changing BM25 algorithm or IDF formula
- Removing image data from Qdrant chunk payloads (images are already stripped by this fix at chunk creation time)
- Supporting multiple books with independent vocab (single global vocab is retained)

## Decisions

### 1. Strip images in MarkdownChunker, not in BookIndexer

**Decision**: Add a preprocessing step at the top of `MarkdownChunker.Chunk()` that removes `![...](<data:...;base64,...>)` patterns via regex before line-by-line parsing.

**Why here**: The chunker is the single entry point for all text that flows to embedding, BM25, and Qdrant. Stripping here ensures no downstream consumer ever sees base64. Doing it in BookIndexer would require threading the stripped text through multiple call sites.

**Alternative considered**: Strip in `VocabularyBuilder.AddChunk()` only. Rejected — base64 would still inflate chunk token counts and embed into Qdrant payloads.

### 2. Single-row `bm25_stats` table for `total_documents`

**Decision**: Create a new `bm25_stats` table with columns `(id SERIAL PK, total_documents INT, updated_at TIMESTAMPTZ)`. `BM25VocabService` upserts one row (id=1).

**Why**: Reduces the vocab update from O(N rows) to O(1). The `total_documents` value is a global counter shared across all terms — storing it per-row is pure redundancy.

**Alternative considered**: Store it in app settings/PostgreSQL GUC. Rejected — EF Core migration is cleaner and keeps everything in the relational schema.

### 3. Migration truncates `bm25_vocab`

**Decision**: The migration SQL includes `TRUNCATE TABLE bm25_vocab` as a data migration step. A re-index is required after deploy.

**Why**: All 1.58M existing rows are corrupt (base64 noise mixed in). Trying to surgically remove only the garbage terms is not reliable. Clean slate + re-index is the safe path.

**Risk**: If re-index is not triggered after deploy, the sparse vectorizer has an empty vocab and degrades to dense-only search. Mitigation: document in deploy checklist; the indexing endpoint already supports `force=true`.

## Risks / Trade-offs

- **Re-index required**: After deploy, BM25 is non-functional until re-index completes (~25 min). Dense search still works during this window.
- **Single global vocab**: If a second book is later added, `force=true` on one book clears all vocab data. This is a pre-existing design limitation, not introduced here.

## Migration Plan

1. Apply code changes (strip images, new entity, updated service)
2. `dotnet ef migrations add CleanBm25VocabAndSeparateTotalDocs` on host
3. Rebuild and restart web container (migrations run at startup via `MigrateDbAsync`)
4. Trigger re-index: `POST /api/admin/index?id=1&force=true`
5. Verify point count in Qdrant and row count in `bm25_vocab` (~50K real terms expected)

**Rollback**: Revert to previous image, restore `bm25_vocab` from backup (or accept empty vocab until next re-index).
