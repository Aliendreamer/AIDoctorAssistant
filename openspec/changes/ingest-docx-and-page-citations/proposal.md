## Why

A new source has arrived that the pipeline cannot ingest: the Bulgarian translation of Nelson Textbook of
Pediatrics (20th edition), delivered as zips of 5-page Word (`.docx`) parts rather than PDFs. At the same time,
every citation shows `pp. 0–0`. `BookIndexer` hardcodes pages to 0, and `MinerUClient` asks MinerU for markdown
only, which has no page boundaries. The Nelson parts record their PDF and printed page numbers in the text, so
this is the moment to carry pages end to end: the first ingestion of the new book should cite real pages.

## What Changes

- **New `.docx` ingestion path, no OCR.** An admin uploads a zip of Nelson `.docx` parts. The parts are stored
  under the book and converted to markdown: Word heading styles become markdown headings (including the
  Bulgarian-locale numeric style ids), tables and figure captions are kept, and the translator's per-file header
  lines and meta-notes are dropped. Parts are ordered by the PDF page range in their filenames.
- **One combined book, built up over uploads.** Every Nelson Part goes into a single book. Uploading another
  Part adds its files to the stored set, regenerates the book's markdown from all parts, and re-indexes the
  book through the existing single-book force re-index, which removes its stale points first.
- **Page markers in markdown.** Both converters write a neutral page marker into the markdown wherever the page
  changes. The `.docx` converter takes the printed and PDF pages from the translator's page lines
  (`PDF стр. N · печатна стр. N`, with its wording variants), falling back to the filename's PDF range. MinerU
  conversion requests MinerU's content list and takes the PDF page from each block's `page_idx`.
- **Pages flow through chunking into Qdrant.** The chunker resolves each chunk's page range from the markers and
  strips them from the text. Chunks store the PDF page range and, when known, the printed page range. Printed
  pages are text, because Nelson has online-only pages such as `1804.e1`.
- **Citations show the right page or none.** A citation shows the printed page when known (`p. 1804`), otherwise
  the PDF page (`PDF p. 2635`), otherwise no page at all. `pp. 0–0` is never shown again, which also covers the 8
  books already indexed without page data.
- **Docs:** `CLAUDE.md` still describes Marker OCR and a `marker` service, although OCR moved to the shared
  MinerU service. Correct it.

Out of scope, tracked in `ROADMAP.md`: seeding the empty ICD-10 illness dictionary, and re-extracting the 8
existing books to give them page numbers.

## Capabilities

### New Capabilities

- `docx-book-ingestion`: converting a zip of Word parts into one book's markdown. Covers part storage and
  ordering, heading/table/caption conversion, removal of translator headers and meta-notes, page markers from
  translator page lines, and re-indexing the combined book when a Part is added.
- `page-citations`: page markers in markdown, per-chunk page ranges through chunking and the Qdrant payload,
  and the display rule (printed page, else PDF page, else no page).

### Modified Capabilities

- `mineru-ocr-service`: conversion also requests MinerU's content list and produces markdown carrying PDF page
  markers, instead of reading the page-less `md` field.
- `book-upload-validation`: upload accepts a bounded zip containing only `.docx` parts, alongside the existing
  PDF upload, with entry-name, size and count limits.

## Impact

- **Code:** `MedAssist.AI/Ingestion` (new docx converter, `MinerUClient`, `MarkdownChunker`, `BookIndexer`),
  `MedAssist.AI/VectorStore/QdrantVectorStore.cs` (payload read/write), `MedAssist.Shared/Models`
  (`MedicalChunk`, `SourceCitation`, `RagTrace`), `MedAssist.AI/Plugins/RagPluginBase.cs`,
  `MedAssist.Web/Endpoints/Books` (upload), `MedAssist.Web/Services/IngestionWorker.cs`, the citation UI
  (`Query.razor`, `BookSourceCitation.razor`) and `EvaluationRunner`.
- **Dependencies:** `DocumentFormat.OpenXml` for reading `.docx`; no new service or container.
- **Data:** new optional payload fields for printed pages. Existing points keep working: their page fields read
  as unknown. No re-index of existing books is required.
- **Evaluation:** run the golden set after the change; answers should be unchanged apart from citations.
