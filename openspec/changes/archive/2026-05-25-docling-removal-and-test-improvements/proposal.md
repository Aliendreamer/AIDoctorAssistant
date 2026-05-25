## Why

Docling was replaced by Marker as the PDF extraction backend. Its client code, options class, and Docker artifacts remained as dead code. Additionally, two pre-existing test failures went undetected after `MinRetryScore` was added to the RAG pipeline, and no tests existed for Marker-specific markdown patterns or the MarkerClient polling state machine.

## What Changes

- Deleted `MedAssist.AI/Ingestion/DoclingClient.cs` — dead code, nothing registered or called it
- Deleted `MedAssist.Web/Options/DoclingOptions.cs` — dead code
- Deleted `docker/docling/` — Docker image no longer in use
- Updated soft-hyphen regex comment in `MarkdownChunker.cs` from "Docling PDF artifact" to "scanned PDF artifact"
- Removed `"docling"` / `"Docling"` entries from `cspell.json`
- Added `MarkerImageRefRegex` to `MarkdownChunker` to strip Marker-style image refs (`![](_page_N.jpeg)`) before chunking — they carry no semantic value for RAG
- Made `MarkerClient` poll interval injectable via constructor (`pollInterval` param, defaults to 30 s) to enable fast unit tests without real delays
- Fixed `BulkExtractEndpoint` to skip books whose `.md` is already newer than their `.pdf` (avoids re-extracting freshly completed books during a bulk run)
- Fixed 2 pre-existing `RagIterativeLoopTests` failures: `MinRetryScore`/`MinAnswerScore` were missing from test `RagOptions`, causing the CRAG web-fallback branch to fire before any retries
- Added 5 new `MarkdownChunkerTests` covering Marker-specific patterns (image refs, base64 stripping, spaced-letter OCR, soft hyphens, `####` heading hierarchy)
- Added 6 new `MarkerClientTests` covering job submission, done/failed states, transient HTTP retry, cancellation, and `use_llm` query param

## Capabilities

### New Capabilities

- `marker-image-ref-stripping`: MarkdownChunker strips `![](_page_N.jpeg)` Marker image reference lines before chunking so they don't pollute chunk text
- `bulk-extract-smart-skip`: BulkExtractEndpoint skips books whose `.md` file is newer than the source `.pdf`, preventing redundant re-extraction

### Modified Capabilities

- `iterative-rag-retrieval`: Test options for the RAG loop now explicitly set `MinRetryScore`/`MinAnswerScore` to `NegativeInfinity` to isolate iteration behaviour from threshold gating

## Impact

- `MedAssist.AI/Ingestion/MarkdownChunker.cs` — new regex, comment change
- `MedAssist.AI/Ingestion/MarkerClient.cs` — constructor signature change (optional `pollInterval` param, backward compatible)
- `MedAssist.Web/Endpoints/Books/BulkExtractEndpoint.cs` — eligibility filter updated
- `MedAssist.Tests/` — 11 new/fixed tests, 60 total passing
- `cspell.json` — 2 entries removed
