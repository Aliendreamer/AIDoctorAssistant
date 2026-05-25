## ADDED Requirements

### Requirement: Submit conversion job
The Marker service SHALL accept a `POST /convert-by-path` request and return a `job_id` immediately without waiting for conversion to complete.

#### Scenario: Successful job submission
- **WHEN** a valid `file_path` is POSTed to `/convert-by-path`
- **THEN** the service returns HTTP 202 with `{"job_id": "<uuid>"}` within 1 second

#### Scenario: File not found
- **WHEN** the submitted `file_path` does not exist on disk
- **THEN** the service returns HTTP 404 before starting a job

#### Scenario: Models not loaded
- **WHEN** the service receives a request before model initialisation is complete
- **THEN** the service returns HTTP 503 with `{"detail": "Models not loaded yet"}`

### Requirement: Poll job status
The Marker service SHALL expose `GET /status/{job_id}` returning the current state of a conversion job.

#### Scenario: Job in progress
- **WHEN** the job is still converting
- **THEN** the endpoint returns `{"state": "running"}`

#### Scenario: Job completed
- **WHEN** conversion has finished successfully
- **THEN** the endpoint returns `{"state": "done", "markdown": "<full markdown text>"}`

#### Scenario: Job failed
- **WHEN** conversion raised an exception
- **THEN** the endpoint returns `{"state": "failed", "error": "<exception message>"}`

#### Scenario: Unknown job ID
- **WHEN** `job_id` does not exist in the in-memory store
- **THEN** the endpoint returns HTTP 404

### Requirement: Sequential job execution
The Marker service SHALL process at most one conversion at a time; subsequent submissions SHALL queue behind the running job.

#### Scenario: Concurrent submissions
- **WHEN** two jobs are submitted while one is already running
- **THEN** the second job starts only after the first completes

### Requirement: .NET client polls until completion
`MarkerClient` SHALL submit a job via `StartConversionAsync` and poll `PollStatusAsync` every 30 seconds until the job reaches state `done` or `failed`, or a maximum of 150 polls is exceeded.

#### Scenario: Successful poll completion
- **WHEN** the Python job reaches state `done`
- **THEN** `PollStatusAsync` returns the markdown string and stops polling

#### Scenario: Poll timeout exceeded
- **WHEN** 150 polls complete without a terminal state
- **THEN** `PollStatusAsync` throws `TimeoutException`

#### Scenario: Job failed on Python side
- **WHEN** the Python job reaches state `failed`
- **THEN** `PollStatusAsync` throws `InvalidOperationException` with the error message

### Requirement: Short HttpClient timeout
The `MarkerClient` HttpClient timeout SHALL be set to 10 seconds — sufficient for individual short request/response cycles only.

#### Scenario: Timeout applies only to individual HTTP calls
- **WHEN** a single `/status/{job_id}` call takes longer than 10 seconds
- **THEN** the call is cancelled and retried on the next poll interval
