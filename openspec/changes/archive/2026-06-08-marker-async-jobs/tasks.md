## 1. Python — Async Job API

- [x] 1.1 Add `uuid`, `threading`, `concurrent.futures.ThreadPoolExecutor(max_workers=1)` and `jobs: dict` with `threading.Lock` to `app.py`
- [x] 1.2 Change `POST /convert-by-path` to submit to the thread pool and return `{"job_id": "<uuid>"}` with HTTP 202 immediately
- [x] 1.3 Add `GET /status/{job_id}` endpoint returning `{state, markdown?, error?}` or 404 if unknown
- [x] 1.4 Store job result (markdown or error) in the jobs dict on thread completion; set state to `done` or `failed`

## 2. .NET — MarkerClient

- [x] 2.1 Add `StartConversionAsync(string filePath, CancellationToken ct) → Task<string>` (returns job_id) — POSTs to `/convert-by-path`, expects `{"job_id": "..."}` response
- [x] 2.2 Add `PollStatusAsync(string jobId, CancellationToken ct) → Task<string>` — polls `GET /status/{job_id}` every 30 s, returns markdown on done, throws on failed or after 150 polls
- [x] 2.3 Remove (or keep internal) the old blocking `ConvertPdfByPathAsync` — replace call sites with Start + Poll

## 3. .NET — Endpoints

- [x] 3.1 Update `ExtractBookEndpoint` background task to call `StartConversionAsync` then `PollStatusAsync`, save markdown on success, `MarkFailed` on exception
- [x] 3.2 Update `BulkExtractEndpoint` background task the same way

## 4. Configuration

- [x] 4.1 Set `Marker.TimeoutMinutes` to `1` in `appsettings.shared.json` (covers only individual short HTTP calls)

## 5. Validation

- [x] 5.1 Rebuild marker and web containers
- [x] 5.2 Submit extract request, verify 202 returned immediately and `GET /extract/status` shows `Running`
- [x] 5.3 Poll `GET /extract/status` until `Done`; verify `.md` file written to disk
- [x] 5.4 Submit two extract requests back-to-back, verify second queues behind first (check Python logs)
