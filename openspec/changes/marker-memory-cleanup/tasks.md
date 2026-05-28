## Tasks

### Python (docker/marker/app.py)
- [x] Update `convert_by_path` to evict `done`/`failed` jobs from `_jobs` before inserting the new job
- [x] Update `_run_job` to write markdown to disk (let disk failures propagate to failed state), then store only `save_path` in `_jobs` (not the full markdown string)
- [x] Update `job_status` endpoint to return `save_path` (already in the dict) — no disk-read logic needed

### .NET (MedAssist.AI/Ingestion/MarkerClient.cs)
- [x] Add `SavePath` property to `JobStatusResponse` (`[JsonPropertyName("save_path")]`)
- [x] Update `PollStatusAsync` to return `string savePath` instead of `string markdown`
- [x] Remove the `Markdown` field from `JobStatusResponse`

### .NET (MedAssist.Web/Endpoints/Books/BulkExtractEndpoint.cs)
- [x] Update to verify file exists at `savePath` (from `PollStatusAsync`) instead of writing markdown from HTTP body
- [x] Remove the `WriteAllTextAsync` call — file is already on the shared volume from Python

### Tests (MedAssist.Tests/MarkerClientTests.cs)
- [x] Update `PollStatusAsync_ReturnsMarkdown_WhenStateDone` → `PollStatusAsync_ReturnsSavePath_WhenStateDone`
- [x] Update `PollStatusAsync_RetriesOnTransientHttpError` to use `save_path` in mock response

### Infrastructure
- [ ] Rebuild and restart marker container
- [ ] Rebuild and restart web container
- [ ] Trigger `POST /api/admin/books/extract/all` to re-extract all books without .md files

### Verification
- [ ] Verify bulk extraction creates .md files on disk for all books
- [ ] Verify book 4 and book 5 re-extract successfully
