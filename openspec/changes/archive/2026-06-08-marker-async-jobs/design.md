## Context

Marker OCR converts PDFs page-by-page using ML models. A 463-page book on CPU takes ~85 minutes. The current architecture makes a single synchronous HTTP POST and waits for the response, requiring a 120-minute HttpClient timeout and holding a thread for the entire duration. The timeout is also fragile — any network hiccup or slight underestimate kills the job silently.

The Python FastAPI service already loads models at startup. Adding a background thread pool and an in-memory job dict is minimal additional complexity.

## Goals / Non-Goals

**Goals:**
- Python service accepts a conversion request and returns a `job_id` in < 1 second
- .NET polls `/status/{job_id}` every 30 s — short-lived requests only
- HttpClient timeout reduced to 10 s (enough for request/response, not the full conversion)
- `ExtractionTracker` state (Running/Done/Failed) reflects the actual job state at each poll
- Completed markdown is returned inline in the status response (no shared filesystem dependency for the result)

**Non-Goals:**
- Job persistence across Python container restarts (in-memory only; restart = job lost, re-trigger required)
- Parallel conversion of multiple pages (Marker is already CPU-bound; concurrency here would thrash)
- Progress percentage exposed via the .NET status API (Python logs have it; out of scope for now)

## Decisions

**D1: In-memory job dict on Python side (not Redis/DB)**
Python holds `jobs: dict[str, JobEntry]` with `threading.Lock`. Simple, zero dependencies, sufficient for a single-container deployment. Jobs are lost on restart — acceptable because `.md` files persist on disk; a restart just means re-triggering.

**D2: UUID job IDs**
`uuid.uuid4()` generated at submission. Unguessable, collision-free, no coordination needed.

**D3: Poll every 30 s from .NET, max 150 polls (75 min)**
30 s interval gives low overhead (2 req/min) while not introducing noticeable lag on completion. 150 polls = 75 min ceiling — longer than any known book on CPU. If exceeded, mark Failed in `ExtractionTracker`.

**D4: Markdown returned inline in status response, not via a separate download**
Avoids a second endpoint and keeps the .NET polling logic simple. Markdown for a large book is ~2–5 MB — acceptable as a JSON string payload.

**D5: Single background thread per job via `threading.Thread`**
`concurrent.futures.ThreadPoolExecutor` with `max_workers=1` ensures only one conversion runs at a time, preventing OOM on CPU-constrained machines. Subsequent submissions queue behind the running job.

## Risks / Trade-offs

- **Job lost on Python restart** → Mitigation: `ExtractionTracker` marks Failed on next poll (connection refused), user re-triggers. `.md` file check in `TriggerIndexEndpoint` means re-extraction is only needed if the markdown wasn't saved yet.
- **Large markdown payload in status response** → Mitigation: only returned once (when `state=done`); .NET saves to disk immediately and stops polling.
- **ThreadPoolExecutor queue grows unbounded** → Mitigation: bulk extract already runs books sequentially on the .NET side, so at most one job is submitted at a time.

## Migration Plan

1. Update `app.py` — add job dict, thread pool, `/status/{job_id}` endpoint, modify `/convert-by-path` to return job_id
2. Update `MarkerClient.cs` — add `StartConversionAsync` and `PollStatusAsync`, remove blocking path
3. Update `ExtractBookEndpoint` and `BulkExtractEndpoint` to use new client methods
4. Update `appsettings.shared.json` — `TimeoutMinutes: 1`
5. Rebuild marker and web containers

Rollback: revert app.py and MarkerClient.cs; set TimeoutMinutes back to 120.
