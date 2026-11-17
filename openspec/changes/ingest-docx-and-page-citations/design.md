## Context

Ingestion today is PDF only. Upload stores the PDF and marks the book `Pending`. A separate index trigger queues
an `Index` job, and `IngestionWorker.RunIndexAsync` uses the cached markdown at `Books:MdPath/<bookId>.md` if it
exists; otherwise it converts the PDF with MinerU and caches the result. `BookIndexer` chunks the markdown with
`MarkdownChunker`, which returns `(HeadingPath, Text, ContentType)`, and writes every chunk with
`PageStart = PageEnd = 0`.

The new source is the Bulgarian Nelson Textbook of Pediatrics (20th edition): 112 `.docx` parts in two zips,
each part covering 5 PDF pages, named `<n>-Nelson_<Part>_BG_PDF<start>-<end>[_VAZSTANOVEN].docx`. A survey of
all 112 parts found:

- **Page lines in 110 parts.** They carry the PDF and printed page, written several ways
  (`PDF стр. N · печатна стр. N`, `Оригинал: PDF N · отпечатана стр. N`, `PDF страница N · Отпечатана страница
  N`), sometimes with a descriptive tail, under various styles (`PDFmarker`, `PageMarker`, `SourceMarker`,
  `Subtitle`, none). Printed pages include online-only pages (`1804.e1`) and "no visible printed number".
- **Headings use real heading styles**, partly with Bulgarian-locale numeric style ids (`1`, `21`, `31`, `4`).
- **Banner lines and translator meta-notes** open many parts and appear between sections, in several variants.
- Two parts have no page line; three parts have no PDF range in the name (chapter openers, a corrected table).

The user chose one combined Nelson book, printed pages in citations, and no re-extraction of the existing books.

## Goals / Non-Goals

**Goals:**

- Ingest the Nelson `.docx` zips as one book, adding Parts over time, with no OCR.
- Real page numbers on citations for Nelson (printed) and for future MinerU PDFs (PDF page).
- Never show `pp. 0–0`; show nothing when the page is unknown.

**Non-Goals:**

- Seeding the ICD-10 illness dictionary, and re-extracting the 8 existing books (both in `ROADMAP.md`).
- Generic `.docx` support beyond the conventions these parts use, and images or figures as content.
- Changing retrieval, prompts or answer generation.

## Decisions

### 1. Pages travel inside the markdown as HTML-comment markers

Both converters write a marker line `<!-- page pdf=2635 printed=1804.e1 -->` (`printed` omitted when unknown)
wherever the page changes. The chunker is the only reader.

- **Why:** markdown is already the hand-off point (the cached `.md` file), so pages survive caching and
  re-indexing with no new storage. A comment is invisible if the markdown is ever rendered and cannot occur in
  OCR text.
- **Rejected:** a sidecar offset map (offsets break when the chunker normalises text); Marker's `{N}------`
  separators (no slot for a printed page).

### 2. The chunker resolves page ranges before overlap and strips the markers

`MarkdownChunker.Chunk` returns a record that adds a `PageRange` (PDF start/end, printed start/end) to the
current tuple. Markers are kept in the text through section splitting and small-chunk merging. The page in
effect is carried forward in document order, so each chunk's range is the page in effect at its start through
the last marker inside it. Ranges are fixed and markers removed **before** `ApplyOverlap`, so overlap copied
from the previous chunk never widens a range. `BookIndexer` and the section-summary pass consume the new record.

- **Rejected:** stripping markers first and mapping by character offset; split, merge and normalisation all
  shift offsets.

### 3. Data model: PDF pages stay in the existing fields, printed pages are new text fields

`MedicalChunk.PageStart/PageEnd` and the `page_start`/`page_end` payload keep their names and now mean PDF
pages, with `0` meaning unknown, which is exactly what existing points hold. New optional `PrintedPageStart` and
`PrintedPageEnd` strings go in `MedicalChunk`, `SourceCitation` and `RagTrace`, stored as `printed_page_start`
and `printed_page_end`. The payload reader treats a missing key as unknown instead of indexing the payload
dictionary directly, which would throw.

One shared formatter in `MedAssist.Shared` (`PageLabel.Format`) produces `p. 1804`, `pp. 1804–1805`,
`PDF p. 2635` or an empty string. `Query.razor`, `BookSourceCitation.razor` and `EvaluationRunner` all use it,
so the three places can't drift apart.

- **Rejected:** renaming the payload fields, which forces a re-index of every book for no gain.

### 4. Word reading uses `DocumentFormat.OpenXml`, in process

A new `DocxBookConverter` in `MedAssist.AI/Ingestion` reads each part with the OpenXml SDK (Microsoft, MIT). It
resolves heading levels through `styles.xml`: a style id maps to its style's built-in name (`heading 2`) or
outline level, following `basedOn`. This handles the Bulgarian numeric ids without a hardcoded id table, and
the English ids fall out of the same lookup. Tables become markdown tables. Captions (`Caption`,
`Figurecaption` styles, or lines starting `Фигура`/`Таблица`) stay as text. Drawings are skipped.

- **Rejected:** pandoc (another binary in the image, and it maps the numeric ids to body text); sending `.docx`
  to MinerU (it renders to PDF and OCRs text that is already digital, and the page lines would come back as
  noisy text).

### 5. Page lines, banners and meta-notes are recognised by text patterns

The page-line pattern is anchored at the start of a short paragraph (under 200 characters): an optional
`Оригинал:`/`ОРИГИНАЛ:`, then `PDF` with an optional `стр.`/`страница`, a page or range, `·`, then an optional
`печатна`/`отпечатана` with `стр.`/`страница`, and a printed page (`\d+(\.e\d+)?`) or a "no visible number"
phrase. The whole paragraph, descriptive tail included, becomes a marker. Patterns live in a constants class,
with one unit test per variant found in the survey.

Banner lines (`ЧАСТ …`, `Част N — PDF страници …`, `Раздел … · …`, the "translation of Nelson Textbook" line,
`Nelson Textbook of Pediatrics, N-о издание · …`) are dropped only among the leading paragraphs of a part,
before its first heading or body paragraph, so the same words inside content are untouched. Meta-notes are
dropped anywhere by prefix (`Контролна граница:`, `Бележка за продължението:`,
`Бележка за печатния оригинал:`). Styles are not used for this, because the survey found the same kind of line
under many different styles.

### 6. Parts live in a per-book directory; indexing always regenerates the markdown

Upload extracts parts to `Books:DocxPath/<bookId>/`, resolved with `BookIdRules.ResolveWithin`, and sets the
book's `FilePath` to that directory. In `RunIndexAsync`, a book whose `FilePath` is a directory is a Word book:
the worker regenerates the markdown from all parts, overwrites the cache, and indexes. Conversion takes seconds,
so regenerating every time means a stale cache can't happen. Adding a Part is "upload, then index with
`force=true`". This reuses the existing single-book force re-index, which deletes only this book's points and
resets its checkpoint.

Ordering: parts sort by first PDF page. A part without a range takes its numeric-prefix position (`41.2` sorts
as 41.2) among the parts uploaded with it, and is ordered by the nearest ranged part before it.

### 7. MinerU markdown is built from the content list

The client sends `return_content_list=true` and builds markdown from the blocks, in order:

- `text` blocks, as headings when `text_level` is set;
- `table` blocks, with their caption and `table_body`;
- `equation` blocks, as their LaTeX text;
- `image` blocks, as captions only.

It writes a marker whenever `page_idx + 1` changes. If the content list is absent or not an array, it falls
back to `md` with no markers. This only affects books converted from now on.

### 8. One upload endpoint, branching on the file signature

`POST /api/admin/books/upload` reads the first bytes: `%PDF-` goes to the existing PDF path, `PK\x03\x04` to
the zip path, anything else is rejected. Zip limits are named constants: at most 500 entries, 50 MB per entry
and 500 MB in total. Sizes are counted while copying, not taken from the zip headers. Extraction goes to a
temporary directory inside the book's parts directory and is moved into place only when every entry passes, so
a rejected zip leaves the stored parts untouched.

## Risks / Trade-offs

- **An unrecognised page-line variant** stays in the chunk text and the page doesn't advance. → The converter
  logs every short paragraph containing `PDF` and a digit that it did not recognise, and a test asserts zero
  such lines across the two real zips.
- **Over-eager banner or note stripping removes content.** → Stripping is limited to the leading paragraphs
  (banners) and exact prefixes (notes). A test checks that the first body paragraph of sampled parts survives.
- **Content-list markdown differs from MinerU's own `md`**, so a re-extracted book chunks slightly differently.
  → This is acceptable because no existing book is re-extracted in this change. The roadmap item re-measures
  with the golden set when it happens.
- **A force re-index of the whole Nelson book on each new Part** re-embeds every chunk, which takes minutes of
  GPU time per Part. → Acceptable for an occasional admin action, and much simpler than per-Part indexing with
  shifting chunk ids.
- **`.docx` parsing of untrusted uploads.** → Admin-only endpoint, signature check, size and entry caps, and the
  OpenXml SDK reads only the document, style and table parts.

## Migration Plan

1. Deploy. Existing points keep working: no printed fields, and `page_start = 0` reads as unknown, so their
   citations simply drop the page text.
2. Upload both Nelson zips to book `nelson-20-bg` (title "Nelson Textbook of Pediatrics", edition 20,
   language `bg`), then index.
3. Run the golden set against the pinned baseline. Answers on the existing books should be unchanged.
4. Rollback: redeploy the previous image. The new payload fields are ignored, and the Nelson book can be
   removed with a single-book delete.

## Open Questions

None. (Resolved: `Admin/UploadBook.razor` limits the picker to `accept=".pdf"`; it will accept `.pdf,.zip`.)
