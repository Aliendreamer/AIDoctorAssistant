## 1. Page model and display (test-first)

- [x] 1.1 Add `PageMarker` (format + parse of `<!-- page pdf=N printed=X -->`) in `MedAssist.Shared`, with
  round-trip tests including `printed=1804.e1` and an absent printed page
- [x] 1.2 Add `PageLabel.Format` with tests: printed range, single printed page, PDF-only, unknown/`0` → empty
- [x] 1.3 Add `PrintedPageStart`/`PrintedPageEnd` (string?) to `MedicalChunk`, `SourceCitation` and `RagTrace`;
  map them in `RagPluginBase`
- [x] 1.4 `QdrantVectorStore`: write `printed_page_start`/`printed_page_end`; read all page fields tolerantly
  (missing key → unknown), with a test for a legacy payload
- [x] 1.5 Use `PageLabel.Format` in `Query.razor` (source list and copy label), `BookSourceCitation.razor` and
  `EvaluationRunner` (trace line), so `0–0` is never rendered

## 2. Chunker page ranges (test-first)

- [x] 2.1 Change `MarkdownChunker.Chunk` to return a record with a `PageRange`; update `BookIndexer` and the
  section-summary pass to compile against it
- [x] 2.2 Tests: chunk inside one page, chunk crossing a marker, split of a large section across pages, merged
  small chunks, overlap not widening the range, markers absent from chunk text, no markers → unknown
- [x] 2.3 Implement page carrying through split/merge and resolve ranges before `ApplyOverlap`
- [x] 2.4 `BookIndexer`: set `PageStart`/`PageEnd` and printed pages from the chunk's range (remove the
  hardcoded `0`)

## 3. MinerU content list → markdown with page markers (test-first)

- [x] 3.1 Tests in `MinerUClientTests`: form sends `return_content_list=true`; text/heading/table/equation/image
  blocks render; markers at `page_idx` changes (1-based); fallback to `md` when the content list is missing
- [x] 3.2 Implement the content-list renderer in `MinerUClient` and update its XML doc

## 4. Word part conversion (test-first)

- [x] 4.1 Add `DocumentFormat.OpenXml` to `MedAssist.AI` (central package version if CPM is used)
- [x] 4.2 Add constants for page-line, banner and meta-note patterns; one unit test per page-line variant from
  the survey (`PDF стр.`, `Оригинал: PDF`, `PDF страница`, `.eN` pages, no visible printed number, with tail)
- [x] 4.3 Implement `DocxBookConverter`: heading levels via `styles.xml` name/outline level and `basedOn`
  (covers ids `21`, `31`, …), tables → markdown tables, captions kept, drawings skipped
- [x] 4.4 Strip leading banners and meta-notes; keep `Адаптирано от`/`Източник:` lines; log unrecognised
  short `PDF`+digit paragraphs
- [x] 4.5 Part ordering by first PDF page, unranged parts by numeric prefix (`41.2`), filename fallback marker,
  page inheritance across parts; tests with synthetic `.docx` built in-test via OpenXml
- [x] 4.6 Fixture test over the two real zips (skipped when `books/` is absent or still encrypted): zero
  unrecognised page lines, no banner line in the output, every part contributes text

## 5. Upload and indexing of Word books

- [x] 5.1 Zip validation in `BookApplicationService` (signature branch `%PDF-` / `PK\x03\x04`): `.docx`-only
  entries, flattened safe names, entry/size caps counted while copying, staging directory then move; tests for
  each `book-upload-validation` scenario
- [x] 5.2 Store parts in `Books:DocxPath/<bookId>/` (config + compose volume), replace same-named parts, set
  `FilePath` to the directory, status `Pending`
- [x] 5.3 `IngestionWorker.RunIndexAsync`: a directory `FilePath` regenerates markdown via
  `DocxBookConverter`, overwrites the cache, and never calls MinerU; test it
- [x] 5.4 `Admin/UploadBook.razor`: accept `.pdf,.zip`; upload response message mentions `force=true` when
  the book was already indexed

## 6. Docs and verification

- [x] 6.1 Fix `CLAUDE.md`: Marker → MinerU in the ingestion flow and the compose services line
- [x] 6.2 `dotnet build MedAssist.slnx` with 0 warnings; `dotnet test MedAssist.Tests` green
- [x] 6.3 With approval, rebuild `web`, seed `nelson-20-bg` from `seed_books.sql` (its markdown is committed in
  `books/mdFiles`, so no zip upload), index it, and check in the UI (Playwright, <http://localhost:8081>) that a
  Nelson answer cites printed pages and an old-book answer shows no page
- [x] 6.4 Run the golden set against the pinned baseline (`9c9c2df9`; `a7c83bb3` was out of date); record the
  result and that old-book answers are unchanged. With Nelson `0e19ee84`: 27 of 34 vs 28 in a no-Nelson control
  `50e3ef46` on the same setup (R2 gained; R5, G11's fact and G12 lost); other answers identical. The first run
  `1c1aa109` (23) was not reproducible. See `mem:project/evaluation-baseline-findings`
- [x] 6.5 Save a Serena memory on the docx conventions and page-marker format
