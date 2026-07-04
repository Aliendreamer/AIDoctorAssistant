## REMOVED Requirements

### Requirement: Submit conversion job
**Reason**: MinerU conversion is synchronous — there is no job to submit; `POST /file_parse` returns the parsed result directly.
**Migration**: Call `MinerUClient.ConvertToMarkdownAsync(filePath)`, which performs one `POST /file_parse`.

### Requirement: Poll job status
**Reason**: With a synchronous conversion there is no `/status/{id}` to poll.
**Migration**: The single `ConvertToMarkdownAsync` call returns the markdown when the request completes.

### Requirement: Sequential job execution
**Reason**: Job serialization was a property of the self-hosted Marker service; the shared MinerU service owns its own concurrency, and the ingestion worker already drains jobs one at a time.
**Migration**: Serialization is provided by the single-reader `IngestionWorker`; no per-service job queue is needed here.

### Requirement: .NET client polls until completion
**Reason**: The `MarkerClient` submit-then-poll loop is removed.
**Migration**: `MinerUClient.ConvertToMarkdownAsync` awaits the single request instead of polling.

### Requirement: Short HttpClient timeout
**Reason**: A short timeout + polling is replaced by one long-lived request; the MinerU client SHALL use a long timeout because the whole OCR runs within the single call.
**Migration**: Configure `MinerU:ConversionTimeoutMinutes` (default 120) on the MinerU HTTP client — see `mineru-ocr-service`'s "Long conversion timeout" requirement.
