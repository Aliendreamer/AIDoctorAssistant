## Overview

Two-part fix to prevent memory growth and avoid large HTTP payloads in the Marker service during bulk multi-book extraction runs.

## Root Cause (Observed)

Book 4 (2042 pages) completed conversion in Python but left no `.md` on disk. Investigation shows both Python's silent disk-write failure AND the `.NET` side failing to receive the large markdown JSON body (15–25 MB for a book this size) likely contributed. The `_jobs` dict also retains every book's full markdown indefinitely, compounding RAM pressure.

## Part 1 — Clear stale jobs on new submission

In `convert_by_path`, before inserting the new job, evict all completed entries:

```python
@app.post("/convert-by-path", status_code=202)
async def convert_by_path(req: ConvertByPathRequest):
    job_id = str(uuid.uuid4())
    save_path = os.path.splitext(req.file_path)[0] + ".md"

    with _jobs_lock:
        # Free memory from previous completed jobs before starting the next one.
        # Safe: .NET submits jobs sequentially so all prior results are already retrieved.
        done_keys = [k for k, v in _jobs.items() if v.get("state") in ("done", "failed")]
        for k in done_keys:
            del _jobs[k]
        _jobs[job_id] = {"state": "running", "started_at": time.time()}

    _executor.submit(_run_job, job_id, req.file_path, req.use_llm, save_path)
    return JSONResponse(status_code=202, content={"job_id": job_id})
```

## Part 2 — Drop markdown from memory; return save_path from status API

In `_run_job`, after writing to disk, store only `save_path` (not the full markdown) in `_jobs`. The `/status` endpoint returns `save_path` when done — `.NET` reads the file directly from the shared volume mount:

```python
def _run_job(job_id, file_path, use_llm, save_path):
    try:
        markdown = _convert(file_path, use_llm)

        with open(save_path, "w", encoding="utf-8") as f:
            f.write(markdown)
        logger.info("Job %s: markdown saved to %s", job_id, save_path)

        with _jobs_lock:
            # Store only the path — avoids holding a 15-25 MB string per book
            _jobs[job_id] = {"state": "done", "save_path": save_path}
        logger.info("Job %s: done", job_id)
    except Exception as exc:
        logger.exception("Job %s: conversion failed", job_id)
        with _jobs_lock:
            _jobs[job_id] = {"state": "failed", "error": str(exc)}


@app.get("/status/{job_id}")
async def job_status(job_id: str):
    with _jobs_lock:
        job = _jobs.get(job_id)

    if job is None:
        raise HTTPException(status_code=404, detail=f"Unknown job: {job_id}")

    result = dict(job)
    if "started_at" in result:
        result["elapsed_seconds"] = int(time.time() - result.pop("started_at"))

    return result
```

Note: the status endpoint no longer reads the file — it just returns `save_path`. The `.NET` side reads from disk.

## Part 3 — .NET reads from shared volume instead of HTTP body

Both the `marker` and `medassist-web` containers mount `./books/raw:/books/raw`, so the path Python writes to is directly readable by `.NET`.

### MarkerClient changes

`PollStatusAsync` returns `(string savePath)` instead of the full markdown string.

```csharp
// JobStatusResponse gains save_path field
private sealed class JobStatusResponse
{
    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("save_path")]
    public string? SavePath { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("elapsed_seconds")]
    public int? ElapsedSeconds { get; init; }
}

// PollStatusAsync now returns save_path (string), throws on failed
public async Task<string> PollStatusAsync(string jobId, CancellationToken cancellationToken = default)
{
    // ... polling loop ...
    switch (status.State)
    {
        case "done":
            return status.SavePath
                ?? throw new InvalidOperationException("Job done but save_path is missing.");
        // ... failed / default cases unchanged ...
    }
}
```

### BulkExtractEndpoint changes

After polling, read the markdown directly from disk instead of from the HTTP body:

```csharp
var savePath = await marker.PollStatusAsync(jobId);

// Read from shared volume — avoids 15-25 MB HTTP body for large books
var markdown = await File.ReadAllTextAsync(savePath);
await File.WriteAllTextAsync(markdownPath, markdown);  // markdownPath == savePath, this is a no-op but kept for clarity
_tracker.MarkDone(book.Id);
```

Since `savePath` returned by Python is already `markdownPath` (same path, same shared volume), the `WriteAllTextAsync` is redundant but harmless. Alternatively, skip the `.NET` write entirely and just use `savePath` directly for chunking.

## Deployment

- Rebuild marker container after current bulk extraction is fully complete
- Rebuild web container after Python changes are deployed (MarkerClient + BulkExtractEndpoint changes)
- No changes to database schema
