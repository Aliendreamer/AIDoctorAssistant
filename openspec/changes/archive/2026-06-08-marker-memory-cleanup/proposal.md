## Why

The Marker Python service accumulates completed job results in the `_jobs` dict indefinitely. Each finished book leaves its full markdown text (1–4MB) in memory, plus PyTorch VRAM fragmentation builds up across conversions. After 3–4 books the service visibly slows down. Observed during bulk re-extraction of 12 books.

## What Changes

- In `app.py`, clear all `done`/`failed` job entries from `_jobs` at the start of each new `/convert-by-path` request — safe because .NET processes books strictly sequentially (one at a time) so previous results are always already retrieved before the next job is submitted
- Optionally: after saving markdown to disk in `_run_job`, replace the in-memory `markdown` field with `None` (or omit it) and rely on the disk file as the source of truth — further reduces peak RAM during long runs

## Capabilities

### New Capabilities

- `marker-job-memory-cleanup`: Clear stale job entries from `_jobs` dict on each new job submission to prevent unbounded memory growth across bulk extractions

### Modified Capabilities

(none)

## Impact

- `docker/marker/app.py` — `/convert-by-path` endpoint, `_run_job` function
- Requires marker container rebuild and restart — must be done between extraction runs, not during an active job
