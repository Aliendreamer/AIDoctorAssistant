# marker-ocr-service Specification

## Purpose
TBD - created by archiving change marker-ocr. Update Purpose after archive.
## Requirements
### Requirement: PDF to markdown conversion endpoint
The Marker service SHALL expose a `POST /convert` endpoint accepting a PDF file via multipart form-data and returning `{ "markdown": "<extracted text>" }`.

#### Scenario: Successful conversion without LLM
- **WHEN** a valid PDF is posted to `/convert`
- **THEN** the service returns HTTP 200 with `{ "markdown": "<text>" }`

#### Scenario: LLM-assisted conversion
- **WHEN** a valid PDF is posted to `/convert?use_llm=true`
- **THEN** the service uses Qwen2.5-VL via Ollama to correct OCR output and returns HTTP 200

#### Scenario: Empty file rejected
- **WHEN** an empty file is posted to `/convert`
- **THEN** the service returns HTTP 400

### Requirement: Health check endpoint
The Marker service SHALL expose `GET /health` returning `{ "status": "ok", "models_loaded": true }` once models are ready.

#### Scenario: Models loaded
- **WHEN** `GET /health` is called after startup completes
- **THEN** the service returns HTTP 200 with `models_loaded: true`

### Requirement: GPU acceleration
The Marker service SHALL run on GPU when an NVIDIA device is available via Docker GPU passthrough.

#### Scenario: GPU detected
- **WHEN** the container starts with `nvidia` device reservation
- **THEN** PyTorch uses CUDA for model inference

### Requirement: MarkerClient integration
The .NET `MarkerClient` SHALL call `POST /convert` with the PDF stream and return the extracted markdown string.

#### Scenario: Successful extraction
- **WHEN** `ConvertPdfToMarkdownAsync` is called with a valid PDF stream
- **THEN** it returns the markdown string from the service response

#### Scenario: LLM flag forwarded
- **WHEN** `MarkerOptions.UseLlm` is `true`
- **THEN** `MarkerClient` appends `?use_llm=true` to the request URL

