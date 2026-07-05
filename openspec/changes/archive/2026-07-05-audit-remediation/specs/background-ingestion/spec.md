## ADDED Requirements

### Requirement: Long-running ingestion runs in a host-managed background service

OCR, indexing, and extraction work SHALL run in a host-managed background service fed by a queue,
not as fire-and-forget `Task.Run` detached from the host. Triggering endpoints SHALL validate,
enqueue, and return 202. The background worker SHALL observe `ApplicationStopping` so a shutdown
does not leave a half-written Qdrant/Postgres state.

#### Scenario: Endpoint returns without doing the work inline

- **WHEN** an index/extract request is accepted
- **THEN** the endpoint returns 202 and the work proceeds on the background service

#### Scenario: Shutdown is honored

- **WHEN** the host begins shutting down while a job is running
- **THEN** the worker observes cancellation and stops at a safe point rather than being abandoned mid-write

### Requirement: Extraction state survives restart

Extraction/indexing progress state SHALL be persisted (not held only in memory) so that status is
recoverable after a process restart.

#### Scenario: State readable after restart

- **WHEN** the process restarts while a book is mid-extraction
- **THEN** the book's status is recoverable rather than silently lost
