## Why

The app builds and runs its own GPU **Marker** OCR container (`docker/marker`), whose image pins
`torch`/`torchvision` on the CUDA `cu128` wheel index. Those multi-hundred-MB NVIDIA wheels cannot
be downloaded on the current network (path-MTU/throughput limits stall the transfer), so the stack
can't be built. The sibling **DndMcpAICsharpFun** project already solved this by dropping its own OCR
container and calling the **shared MinerU service** (`mineru:8000`) that the PersonalCommandCenter
(PCC) stack already runs. Adopting the same pattern removes the unbuildable GPU image entirely and
reuses running shared infrastructure.

## What Changes

- **BREAKING**: Remove the self-hosted `marker` service from `docker-compose.yml` (service,
  `web`'s `depends_on: marker`, and the `marker-models` volume). The `docker/marker/` build context
  is no longer used by the compose stack.
- Replace the async **Marker** client (`MarkerClient`: `POST /convert-by-path` → poll `/status/{id}`)
  with a synchronous **MinerU** client (`MinerUClient.ConvertToMarkdownAsync`) that does a single
  `POST {mineru}/file_parse` (multipart: `files`, `backend=pipeline`, `parse_method=ocr`,
  `return_md=true`) and reads the markdown from `results.<key>.md`.
- Ingestion (`IngestionWorker`) calls the one synchronous conversion instead of submit-then-poll for
  both the index and extract paths; the markdown is written to the existing `Books:MdPath` cache.
- Config: replace the `Marker` section with a `MinerU` section (`ServiceUrl=http://mineru:8000`,
  `Backend=pipeline`, `Method=ocr`, `ConversionTimeoutMinutes=120`). DI wiring updated accordingly.
- Tests: replace `MarkerClientTests` with `MinerUClientTests` covering the request shape and the
  `results.<key>.md` response parsing.

## Capabilities

### New Capabilities
- `mineru-ocr-service`: Synchronous PDF→Markdown conversion by POSTing a PDF to the shared MinerU
  service's `/file_parse` endpoint and extracting the markdown from the `results.<key>.md` payload.

### Modified Capabilities
- `marker-ocr-service`: **Removed** — the `/convert` markdown endpoint is no longer used; MinerU
  replaces it.
- `marker-async-job-api`: **Removed** — the submit/poll (`/convert-by-path` + `/status/{id}`) flow is
  no longer used; MinerU conversion is synchronous.

## Impact

- **Code**: `MedAssist.AI/Ingestion/MarkerClient.cs` (removed) → new `MinerUClient.cs`;
  `MedAssist.Web/Options/MarkerOptions.cs` → `MinerUOptions.cs`; `IngestionWorker` (both ingestion
  paths); `ServiceCollectionExtensions` DI; `MedAssist.Tests/MarkerClientTests.cs` → `MinerUClientTests.cs`.
- **Config**: `config/appsettings.shared.json` `Marker` → `MinerU`.
- **Infra**: `docker-compose.yml` loses the `marker` service + `marker-models` volume; no local GPU
  OCR build. Depends on the PCC-provided `mineru:8000` service being reachable on
  `personalcommandcenter_default`.
- **Runtime behavior**: OCR is now a single blocking HTTP call (long timeout) instead of a polled
  job; the ingestion worker already runs off the request thread, so this is contained.
