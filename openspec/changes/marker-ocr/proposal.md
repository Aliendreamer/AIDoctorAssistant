## Why

Docling produces garbled Cyrillic text in several indexed Bulgarian medical books — Portuguese diacritics (ã, ç, ő) appearing inside Bulgarian words, soft hyphen artifacts, and broken ligatures. This corrupts embeddings and causes retrieval misses on valid Bulgarian queries. Marker is a newer Python OCR library with stronger multilingual support and optional LLM-assisted correction via a local vision model (Qwen2.5-VL via Ollama).

## What Changes

- Replace the `docling` Docker service with a new `marker` Docker service (Python FastAPI wrapping `marker-pdf`)
- Replace `DoclingClient.cs` with `MarkerClient.cs` — simpler single-POST interface, no async polling
- Replace `DoclingOptions.cs` with `MarkerOptions.cs` — adds `UseLlm` flag
- Update `TriggerIndexEndpoint.cs` to resolve `MarkerClient` instead of `DoclingClient`
- Update `ServiceCollectionExtensions.cs` DI registration
- Update `docker-compose.yml` and `appsettings.shared.json`

## Capabilities

### New Capabilities

- `marker-ocr-service`: Python FastAPI service wrapping `marker-pdf`; accepts PDF via multipart POST, returns `{ "markdown": "..." }`; optionally uses Qwen2.5-VL via Ollama for LLM-assisted OCR correction

### Modified Capabilities

- None — the PDF-to-markdown contract is unchanged; `MarkdownChunker` and everything downstream is untouched

## Impact

- **Removed**: `docling` container, `DoclingClient.cs`, `DoclingOptions.cs`
- **Added**: `docker/marker/` (Dockerfile, app.py, requirements.txt), `MarkerClient.cs`, `MarkerOptions.cs`
- **Modified**: `docker-compose.yml`, `appsettings.shared.json`, `ServiceCollectionExtensions.cs`, `TriggerIndexEndpoint.cs`
- **Unchanged**: `MarkdownChunker`, `BookIndexer`, vector store, RAG plugins, Query UI
- **Validation**: Re-index 2–3 worst-offending books after swap, compare Bulgarian retrieval scores
