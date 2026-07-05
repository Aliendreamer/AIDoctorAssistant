# marker-async-job-api Specification

## Purpose
RETIRED. The Marker async job API (submit `POST /convert-by-path`, poll `/status/{id}`) was removed
when OCR moved to the shared MinerU service, whose `/file_parse` is synchronous (see the archived
`migrate-marker-to-mineru` change and the `mineru-ocr-service` capability). This capability no longer
exists in the system.

## Requirements

### Requirement: Marker async job API is retired
The application SHALL NOT expose or consume a Marker submit/poll job API; PDF→markdown conversion is a
single synchronous call to `mineru-ocr-service`.

#### Scenario: No submit/poll flow
- **WHEN** the ingestion worker converts a PDF
- **THEN** it makes one synchronous MinerU call and does not submit or poll a job
