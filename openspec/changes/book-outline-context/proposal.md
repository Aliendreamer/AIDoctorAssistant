## Why

RAG retrieval is currently blind to book structure. The embedder searches all chunks uniformly, which causes two problems:

1. **Cross-language term mismatch** — a query for "Graves disease" misses Bulgarian chunks that say "Болест на Грейвс" because the embedding distance is large and no section-level hint bridges them.
2. **Book targeting is coarse** — when the user says "search in Litvinenko", we filter by `bookId` in Qdrant but the LLM has no idea what chapters that book contains, so it cannot steer the query toward the right section vocabulary.

The Markdown files produced by Docling contain structured headings (H1/H2/H3) that map directly to chapters and sections. Extracting these at index time gives us a per-book outline that costs nothing to store and is small enough to inject into every LLM prompt.

## What Changes

**Index time**
- After Docling converts a PDF to Markdown, extract all H1/H2/H3 headings in order and store them as a `BookOutline` string on the `BookEntity` (new nullable `Outline` column).
- Extraction happens inside `BookIndexer` right after the Markdown is obtained, before chunking.

**Query time**
- `RagPluginBase` loads the outlines of the books being searched (from `BookCatalogService` which already caches book metadata).
- The outlines are injected into the SK prompt as a "book map" section: the LLM sees chapter names in both languages and can map query terms to the correct section vocabulary before generating the embedding query.

**User targeting**
- When the user specifies one or more books, only those books' outlines are injected — keeping the prompt focused.
- When no books are specified (search all), all indexed book outlines are included.

## Capabilities

### Modified Capabilities
- None — no API contract changes; outline storage and prompt injection are internal.

## Impact

- `MedAssist.Data/Entities/BookEntity.cs` — add nullable `Outline` string property
- `MedAssist.Data/` — EF migration for new column
- `MedAssist.AI/Ingestion/BookIndexer.cs` — extract headings from markdown and persist outline
- `MedAssist.AI/Plugins/RagPluginBase.cs` — inject book outlines into SK prompt
- `MedAssist.Web/Services/BookCatalogService.cs` — expose outline in cached book info if needed
