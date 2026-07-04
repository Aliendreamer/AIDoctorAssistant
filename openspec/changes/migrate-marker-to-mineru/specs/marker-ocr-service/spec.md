## REMOVED Requirements

### Requirement: PDF to markdown conversion endpoint
**Reason**: The self-hosted Marker service is removed; PDF→markdown is now done by the shared MinerU service via `POST /file_parse`.
**Migration**: Use `mineru-ocr-service` — `MinerUClient.ConvertToMarkdownAsync` POSTs to `{mineru}/file_parse` with `return_md=true` and reads `results.<key>.md`.

### Requirement: Health check endpoint
**Reason**: There is no longer a self-hosted Marker container to health-check; the shared MinerU service's availability is owned by the PersonalCommandCenter stack.
**Migration**: Rely on the PCC-managed `mineru:8000` service; ingestion failures surface via the extraction status / worker logs.

### Requirement: GPU acceleration
**Reason**: The GPU OCR container (the `torch`/`cu128` build) is removed from this repo; GPU OCR now runs inside the shared MinerU service.
**Migration**: GPU acceleration is provided by the shared MinerU deployment, not by a container built here.

### Requirement: MarkerClient integration
**Reason**: `MarkerClient` (the `/convert` markdown call) is replaced by `MinerUClient`.
**Migration**: Inject `MinerUClient` and call `ConvertToMarkdownAsync(filePath)`.
