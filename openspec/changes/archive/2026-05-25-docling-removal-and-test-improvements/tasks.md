## Tasks

- [x] Delete `MedAssist.AI/Ingestion/DoclingClient.cs`
- [x] Delete `MedAssist.Web/Options/DoclingOptions.cs`
- [x] Delete `docker/docling/` directory
- [x] Update soft-hyphen comment in `MarkdownChunker.cs` from "Docling PDF artifact" to "scanned PDF artifact"
- [x] Remove `"docling"` / `"Docling"` from `cspell.json`
- [x] Add `MarkerImageRefRegex` to `MarkdownChunker` and call it in `StripInlineImages`
- [x] Make `MarkerClient` poll interval injectable via optional `pollInterval` constructor param
- [x] Update `BulkExtractEndpoint` eligibility filter to skip books with fresher `.md` than `.pdf`
- [x] Fix `RagIterativeLoopTests` `DefaultOptions` to set `MinRetryScore`/`MinAnswerScore = NegativeInfinity`
- [x] Fix per-test `RagOptions` in `LowConfidenceScore_RunsFallbackIterations`, `MaxIterations_CappedAtFive`, `HighConfidenceScore_StopsAfterInitialSearch`
- [x] Add 5 new `MarkdownChunkerTests` for Marker patterns
- [x] Add 6 new `MarkerClientTests` covering polling state machine
- [x] Verify all 60 tests pass
