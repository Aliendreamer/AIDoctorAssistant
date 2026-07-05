# mineru-ocr-service Specification

## Purpose
Convert source PDFs to markdown for ingestion by calling the shared MinerU service, replacing the
retired self-hosted Marker container (see the archived `migrate-marker-to-mineru` change). Both the
ingestion worker and the admin extract flow use this single synchronous conversion.

## Requirements

### Requirement: Synchronous PDF-to-markdown conversion via MinerU

The ingestion pipeline SHALL convert a source PDF to markdown with a single synchronous request to
the shared MinerU service. The client SHALL `POST {ServiceUrl}/file_parse` as `multipart/form-data`
with the PDF under the `files` field and the fields `backend`, `parse_method`, and `return_md=true`,
and SHALL return the markdown read from `results.<firstKey>.md` in the JSON response.

#### Scenario: Successful conversion returns markdown

- **WHEN** a valid PDF path is passed to `MinerUClient.ConvertToMarkdownAsync`
- **THEN** the client POSTs the PDF to `/file_parse` with `return_md=true`
- **AND** returns the markdown string taken from `results.<firstKey>.md`

#### Scenario: Configured backend and method are sent

- **WHEN** the client submits a conversion with `Backend=pipeline` and `Method=ocr`
- **THEN** the multipart form includes `backend=pipeline` and `parse_method=ocr`

### Requirement: Conversion failures surface as errors

The client SHALL treat a non-success HTTP status, or a response missing a usable
`results.<firstKey>.md` field, as a failure by throwing — it SHALL NOT return empty or partial
markdown as if it succeeded.

#### Scenario: Service returns an error status

- **WHEN** MinerU responds with a non-2xx status
- **THEN** `ConvertToMarkdownAsync` throws rather than returning markdown

#### Scenario: Response is missing markdown

- **WHEN** MinerU responds 200 but the payload has no `results.<firstKey>.md`
- **THEN** `ConvertToMarkdownAsync` throws an informative exception naming the file

### Requirement: Long conversion timeout

Because OCR of a full book is slow, the MinerU HTTP client SHALL be configured with a long request
timeout driven by `MinerU:ConversionTimeoutMinutes` (default 120), not the short default request
timeout.

#### Scenario: Timeout honors configuration

- **WHEN** the MinerU HTTP client is built
- **THEN** its timeout equals `ConversionTimeoutMinutes` minutes

### Requirement: No self-hosted OCR container

The application SHALL rely on the shared MinerU service (reachable as `mineru:8000` on the
PersonalCommandCenter network) and SHALL NOT build or run its own OCR/Marker container in
`docker-compose.yml`.

#### Scenario: Compose stack has no marker service

- **WHEN** the compose stack is built
- **THEN** only `web` is built and there is no `marker` service, `depends_on: marker`, or
  `marker-models` volume
