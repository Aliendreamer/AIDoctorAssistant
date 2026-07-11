# MedAssist.AI — Project Overview

## Status

Active and well-developed as of 2026-07-11 (was greenfield 2026-05-15). Bilingual (EN/BG) RAG
medical knowledge assistant for physicians. Built and deployed **locally only** — reachable through
an internal HTTPS-terminating proxy on the docker network (see deployment trade-offs in CLAUDE.md).

## Purpose

Answers clinical questions over OCR'd medical books using hybrid dense + sparse (BM25) vector search
with RRF fusion and a cross-encoder reranker, then generates a **cited** answer via a local LLM.

## Solution — MedAssist.slnx (.NET 10)

Layering is acyclic, top-down: Shared <- Data <- AI <- Web. Tests reference all four.

- **MedAssist.Shared** — models, interfaces, constants, pure validation helpers (`BookIdRules`,
  `WebFetchPolicy`, `JwtKeyPolicy`, `DeterministicGuid`).
- **MedAssist.Data** — EF Core 9 + PostgreSQL: entities, migrations, repositories.
- **MedAssist.AI** — embedder (multilingual-e5-large ONNX), cross-encoder reranker
  (ms-marco-MiniLM-L-6-v2), BM25 sparse vectorizer + vocab cache, Qdrant store (dense int8 scalar
  quantization + rescoring, plus sparse), resumable ingestion pipeline, Semantic Kernel plugins
  (GlobalSearch/Disease/Symptoms/Treatment/DifferentialDiagnosis — all over `RagPluginBase`;
  Disease/Symptoms/Treatment use the base prompt).
- **MedAssist.Web** — FastEndpoints REST API + Blazor Server UI + host-managed ingestion worker.
- **MedAssist.Tests** — xUnit; in-memory **SQLite** (a real relational provider) for EF
  SQL-translation tests. `InternalsVisibleTo` on MedAssist.AI exposes internals (e.g. MarkdownStripper).

## Query flow

Browser -> Blazor `QueryService` -> RAG plugin -> ICD query expansion -> dense embed + BM25 sparse
-> Qdrant (dense + sparse prefetch, RRF fusion) -> cross-encoder rerank -> Ollama -> cited answer +
citations. Optional trusted web search (SearXNG), SSRF-guarded by `WebFetchPolicy`. Answers carry
inline `[n]` citation markers mapping to the numbered source list (excerpt n -> sources[n-1]); marker
emission is model-dependent and degrades gracefully. See `openspec/specs/answer-citation-markers`.

## Ingestion flow

Admin uploads PDF -> `IngestionQueue` (a Channel) -> `IngestionWorker` (BackgroundService) ->
**MinerU** OCR (PDF->Markdown) -> chunk -> ICD-10 enrich -> dense + BM25 sparse -> Qdrant upsert
(deterministic point ids) + Postgres status. Resumable via checkpoints. (The old in-repo Marker/GPU
OCR container was retired in favour of the shared MinerU service.)

## Infrastructure — split stack

Shared infra (PostgreSQL, Qdrant, Ollama, SearXNG, OpenTelemetry, Grafana/Prometheus/pgAdmin,
MinerU) is provided by the sibling **PersonalCommandCenter (PCC)** stack on the external network
`personalcommandcenter_default`. This repo's `docker-compose.yml` builds only `web`. Start PCC
first; the `medassist` DB is auto-created by EF migrations on first run. Models: LLM = **qwen3:8b**
(Ollama, run with `/no_think`); embedder = multilingual-e5-large; reranker = ms-marco-MiniLM-L-6-v2
(shared ONNX volume `shared_onnx_models`, shared with the DndMcpAICsharpFun stack).

## Conventions & workflow

- **OpenSpec** change workflow: `openspec/changes/<name>` -> `openspec archive` -> `changes/archive`;
  main specs in `openspec/specs/`. Recent archived changes: ui-visual-redesign, cited-answer-markers.
- Private fields `_camelCase`; constants centralized in `MedAssist.Shared/Constants`; nullable
  enabled; file-scoped namespaces; `Async` suffix. **TreatWarningsAsErrors is ON** — builds must be
  0-warning. FastEndpoints use `Send.*` response methods.
- `config/` and `books/` are **git-crypt** encrypted at rest.
- UI identity: "clinical instrument" — committed dark theme, azure accent, monospace as the
  data/label voice. See `openspec/specs/ui-visual-identity`.

See also `mem:suggested_commands`, `mem:task_completion_checklist`, `mem:feedback/communicate-while-working`.
