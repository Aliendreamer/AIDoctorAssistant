## Why

The current Marker OCR integration keeps an HTTP connection open for the full duration of PDF conversion — up to 2 hours for large books on CPU. This ties up a .NET thread, requires an inflated HttpClient timeout, and silently fails when that timeout is hit before conversion finishes.

## What Changes

- `POST /convert-by-path` on the Python Marker service returns a `job_id` immediately (202) and runs conversion in a background thread
- New `GET /status/{job_id}` endpoint on the Marker service returns `{state, markdown}` when done
- `MarkerClient` gains `StartConversionAsync` (fire-and-forget POST) and `PollStatusAsync` (GET with retry loop) replacing the single blocking `ConvertPdfByPathAsync`
- `ExtractBookEndpoint` and `BulkExtractEndpoint` fire the job then poll every 30 s until done or failed
- HttpClient timeout for Marker drops from 120 minutes to 10 seconds (short request/response only)
- `ExtractionTracker` updated at each poll cycle — no behaviour change to the status API

## Capabilities

### New Capabilities

- `marker-async-job-api`: Async job submission and polling API on the Python Marker service

### Modified Capabilities

- none

## Impact

- `docker/marker/app.py` — new background thread pool, job dict, `/status/{job_id}` endpoint
- `MedAssist.AI/Ingestion/MarkerClient.cs` — replace blocking call with start + poll methods
- `MedAssist.Web/Endpoints/Books/ExtractBookEndpoint.cs` — use new polling flow
- `MedAssist.Web/Endpoints/Books/BulkExtractEndpoint.cs` — use new polling flow
- `config/appsettings.shared.json` — `TimeoutMinutes` drops to 1 (covers only the short HTTP calls)
- No DB schema changes, no API contract changes for callers of the .NET endpoints
