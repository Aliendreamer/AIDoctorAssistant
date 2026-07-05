## Context

Ingestion OCRs a PDF to markdown, then chunks/embeds/indexes it. Today that OCR is a **self-hosted
Marker** container (`docker/marker`, `python:3.11-slim` + `torch`/`torchvision` on the `cu128` wheel
index) exposing an async job API (`POST /convert-by-path` → poll `/status/{id}`), driven by
`MarkerClient`. The `cu128` NVIDIA wheels (600+ MB each) cannot be pulled on the current network
(path MTU ≈ 1492, sustained large downloads stall), so `docker compose build` fails.

The PCC stack already runs a shared **MinerU** service (`personalcommandcenter-mineru-1`, reachable
as `mineru:8000`). The sibling **DndMcpAICsharpFun** project already migrated off Marker onto this
shared MinerU service (`MinerUPdfConverter`, `POST /file_parse`) and works. We adopt the same pattern.

## Goals / Non-Goals

**Goals:**
- Remove the unbuildable local `marker` container and reuse the shared `mineru:8000` service.
- Keep the existing markdown-based pipeline (`MarkdownChunker` unchanged) by requesting markdown
  from MinerU (`return_md=true`).
- Preserve ingestion behavior: cached markdown is reused; a fresh conversion is written to the
  `Books:MdPath` cache; index and extract paths both work.

**Non-Goals:**
- Consuming MinerU's structured `content_list` (DnD does this for D&D stat blocks; MedAssist stays
  markdown-based).
- Changing chunking, embedding, BM25, or the query pipeline.
- Deleting the `docker/marker/` directory from the repo (only the compose wiring is removed).

## Decisions

**1. Synchronous single-call client (`MinerUClient.ConvertToMarkdownAsync`).**
MinerU's `/file_parse` returns the parsed result in one response, so the submit/poll loop is dropped
entirely. Alternative considered: keep the async shape and wrap MinerU — rejected as pointless
indirection since MinerU has no job API.

**2. Request markdown (`return_md=true`), not `content_list`.**
The pipeline already consumes markdown; reading `results.<key>.md` keeps `MarkdownChunker` and the
whole downstream unchanged. Alternative: map `content_list` blocks (DnD's approach) — rejected as a
larger, unnecessary rewrite for MedAssist's needs.

**3. Long HTTP timeout on the MinerU client (`ConversionTimeoutMinutes`, default 120).**
Because the entire OCR runs inside the single request, the client can't use a short timeout. This
mirrors DnD's `MinerUOptions.ConversionTimeoutMinutes = 120`.

**4. Response parsing mirrors DnD's proven envelope.**
`results` is an object keyed by the uploaded file; take the first entry and read `.md`. Missing
`results`/first-entry/`md` throws with the file name, so failures are loud (per the spec).

**5. Ingestion writes markdown to the existing cache.**
Both `RunIndexAsync` and `RunExtractAsync` call `ConvertToMarkdownAsync` and `File.WriteAllText` to
the `Books:MdPath` `.md` path. This also fixes a latent bug where the index path wrote Marker's
`save_path` string into the markdown file.

## Risks / Trade-offs

- **[Shared-service dependency]** MinerU must be up on `personalcommandcenter_default` → ingestion
  fails if it isn't. Mitigation: it's already running and PCC-managed; failures surface via the
  extraction tracker + worker logs, same as before.
- **[Response-shape assumption]** If MinerU's `return_md=true` payload differs from
  `results.<key>.md`, parsing throws. Mitigation: unit-test the parser against the documented shape;
  DnD's `content_list` path confirms the `results.<key>` envelope; loud failure beats silent-empty.
- **[Long blocking call]** One request can run for many minutes. Mitigation: it runs on the
  host-managed `IngestionWorker` off the request thread, with a bounded (120-min) client timeout and
  the shutdown token.
- **[Marker specs retired]** `marker-ocr-service` / `marker-async-job-api` requirements are removed;
  any external consumer of those endpoints would break. Mitigation: they were internal-only.

## Migration Plan

1. Add `MinerUClient` + `MinerUOptions`; wire the `mineru` HttpClient (long timeout) in DI.
2. Switch `IngestionWorker` to `ConvertToMarkdownAsync`.
3. Replace config `Marker` → `MinerU`; remove `marker` service/volume/`depends_on` from compose.
4. Replace `MarkerClientTests` with `MinerUClientTests`; delete `MarkerClient`/`MarkerOptions`.
5. Build + test; then `docker compose build web` (no marker) and verify a query end-to-end.

Rollback: revert the change; the `docker/marker/` context still exists, so the old compose wiring
can be restored if the shared MinerU service is ever unavailable.
