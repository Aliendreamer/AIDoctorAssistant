## 1. Docker Service

- [x] 1.1 Create `docker/marker/Dockerfile` using `pytorch/pytorch:2.3.1-cuda12.1-cudnn8-runtime`
- [x] 1.2 Create `docker/marker/requirements.txt` with `marker-pdf`, `fastapi`, `uvicorn`, `python-multipart`
- [x] 1.3 Create `docker/marker/app.py` — FastAPI with `/health` and `/convert` endpoints, models loaded at startup

## 2. .NET Integration

- [x] 2.1 Create `MedAssist.Web/Options/MarkerOptions.cs` with `Endpoint`, `TimeoutMinutes`, `UseLlm`
- [x] 2.2 Create `MedAssist.AI/Ingestion/MarkerClient.cs` — single POST to `/convert`, returns markdown string
- [x] 2.3 Update `ServiceCollectionExtensions.cs` — replace `DoclingClient`/`DoclingOptions` with `MarkerClient`/`MarkerOptions`
- [x] 2.4 Update `TriggerIndexEndpoint.cs` — resolve `MarkerClient` instead of `DoclingClient`

## 3. Configuration

- [x] 3.1 Update `docker-compose.yml` — replace `docling` service with `marker`, add GPU reservation and LLM env vars
- [x] 3.2 Update `appsettings.shared.json` — replace `Docling` section with `Marker` (endpoint, timeout, `UseLlm: false`)

## 4. Optimisation — Pass file path instead of uploading

- [x] 4.1 Mount `/books/raw` volume into the `marker` container
- [x] 4.2 Add `POST /convert-by-path` endpoint to `app.py` accepting `{ "file_path": "...", "use_llm": false }`
- [x] 4.3 Update `MarkerClient` with `ConvertPdfByPathAsync(string filePath, CancellationToken)` method
- [x] 4.4 Update `TriggerIndexEndpoint` to call `ConvertPdfByPathAsync` using `book.FilePath`
- [x] 4.5 Keep `ConvertPdfToMarkdownAsync` (stream upload) as fallback for future use

## 5. Validation

- [ ] 5.1 Build and start `marker` container, verify `/health` returns `models_loaded: true`
- [ ] 5.2 Trigger indexing on 2–3 books with known Cyrillic artifacts
- [ ] 5.3 Compare extracted markdown — check for absence of Portuguese diacritics in Cyrillic text
- [ ] 5.4 Run Bulgarian queries, compare retrieval scores against pre-swap baseline
- [ ] 5.5 If quality confirmed: delete cached `.md` files and re-index full corpus
