## Why

The `bm25_vocab` table has 1.58 million rows because Docling embeds PDF images as inline base64 (`data:image/png;base64,...`) in the markdown output, and the BM25 tokenizer (`\p{L}+`) treats base64 character sequences as words. Additionally, `total_documents` is stored redundantly on every vocab row, causing a full-table UPDATE (30+ seconds) after each book indexing — which times out and fails. Both issues need to be fixed before re-indexing produces reliable BM25 sparse vectors.

## What Changes

- Strip inline base64 images from markdown before chunking in `MarkdownChunker`
- Move `total_documents` from `bm25_vocab` (per-row) to a new `bm25_stats` table (one global row)
- Create EF Core migration to add `bm25_stats`, drop `total_documents` from `bm25_vocab`, and truncate the corrupt vocab data
- Update `BM25VocabService` to read/write `total_documents` from `bm25_stats`
- Update `TriggerIndexEndpoint` force path to also truncate `bm25_vocab` and reset `bm25_stats`

## Capabilities

### New Capabilities

- `bm25-stats-store`: Global single-row table storing `total_documents` for BM25 IDF computation, replacing the redundant per-row column in `bm25_vocab`

### Modified Capabilities

- None — this is purely an infrastructure fix; RAG retrieval behavior is unchanged

## Impact

- `MedAssist.AI/Ingestion/MarkdownChunker.cs` — add image-strip preprocessing step
- `MedAssist.Data/Entities/Bm25VocabEntity.cs` — remove `TotalDocuments` property
- `MedAssist.Data/Entities/Bm25StatsEntity.cs` — new entity
- `MedAssist.Data/Configuration/Bm25StatsEntityConfiguration.cs` — new EF config
- `MedAssist.Data/MedAssistDbContext.cs` — add `DbSet<BM25StatsEntity>`
- `MedAssist.Data/Migrations/` — new migration (schema + data truncation)
- `MedAssist.AI/Dictionary/BM25VocabService.cs` — updated to use `bm25_stats`
- `MedAssist.Web/Endpoints/Books/TriggerIndexEndpoint.cs` — clear vocab+stats on force
- Container rebuild required; re-index required after deploy
