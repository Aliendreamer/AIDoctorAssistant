## Context

Each book is converted to Markdown by Docling. The Markdown contains H1/H2/H3 headings that map to chapters and sections. Currently this structure is discarded after chunking. The `BookEntity` has no outline field; `RagPluginBase` prompts the LLM with only the retrieved chunks and no book-structure context.

## Goals / Non-Goals

**Goals:**
- Extract chapter/section headings from Markdown at index time and persist per book
- Inject the relevant book outlines into the RAG prompt so the LLM can map query terms to section vocabulary
- Improve cross-language retrieval (EN↔BG) by giving the LLM bilingual chapter names as a bridge
- Allow "search in Litvinenko" to leverage chapter awareness, not just `bookId` filtering

**Non-Goals:**
- Semantic search over outlines (outlines are injected as plain text, not embedded separately)
- Re-indexing existing chunks based on outline — chunks stay as-is
- UI display of book outlines

## Decisions

**D1 — Heading extraction: regex over Markdown**
Parse H1/H2/H3 headings from the raw Markdown string using a simple regex (`^#{1,3}\s+(.+)$` multiline). Join them as a numbered list preserving order. No external library needed; the Markdown is already in memory at index time. Cap at 200 headings to bound prompt size.

**D2 — Storage: nullable `Outline` text column on `BookEntity`**
Add `public string? Outline { get; set; }` to `BookEntity`. Null means the book was indexed before this feature existed or outline extraction was skipped. Add an EF migration. No separate table — outlines are small (typically 1–5 KB) and 1:1 with books.

**D3 — Extraction point: inside `BookIndexer.IndexAsync`**
After obtaining the Markdown (from cache or Docling), call a static `ExtractOutline(markdown)` helper before chunking. Persist the outline via `BookRepository.UpdateOutlineAsync(bookId, outline)`. This is a fire-and-forget upsert — if it fails, indexing continues; the outline column stays null.

**D4 — Prompt injection: in `RagPluginBase` before chunk context**
`RagPluginBase` already builds a prompt from retrieved chunks. Add a "Book Structure" section at the top of the prompt listing each searched book's outline. Load outlines from `BookCatalogService` (already caches `BookInfo` per bookId). If a book has no outline (null), skip it silently.

Format injected into prompt:
```
=== Book Structure ===
[Pediatric Diseases — Litvinenko]
1. Глава 1: Общи принципи в педиатрията
2. Глава 2: Болести на новороденото
3. Глава 3: Болести на ендокринната система / Болест на Грейвс
...

[Harrison's Principles]
1. Chapter 1: Introduction to Clinical Medicine
...
======================
```

**D5 — BookCatalogService: expose outline**
`BookCatalogService` caches `BookInfo` records. Add `Outline` to `BookInfo` (or use a thin wrapper). On cache refresh it reads the `Outline` column. No extra DB query at query time — already loaded with the book record.

## Risks / Trade-offs

- Large books with many headings could add 1–2 KB to every prompt. Mitigated by the 200-heading cap and by only injecting outlines for books being searched.
- Books indexed before this feature have `Outline = null` — they won't benefit until re-indexed. Existing queries degrade gracefully (outline section is simply omitted).
- Heading extraction accuracy depends on Docling output quality. If Docling produces malformed headings, the outline may be noisy but won't break retrieval.
