# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Hard Rules

- **Never run `docker` or `git` commands without explicit user approval.** State the command and
  reason first, then wait for confirmation. This includes `docker compose up/build`, container
  restarts, `git commit`, `git push`, etc. Read-only commands (`docker logs`, `docker ps`) are
  lower risk but still require stating intent first.
- `dotnet build` / `dotnet test` are fine to run without approval.

## Memory

Use **Serena** for project memory (repo-versioned under `.serena/memories/`), not the built-in
`~/.claude/.../memory/` store.

- **Recall:** at the start of non-trivial work, call Serena `list_memories` and `read_memory` for
  anything relevant (activate the `AIDoctorAssistant` project first if Serena reports no active
  project).
- **Save:** write new durable facts with Serena `write_memory`. Prefer topic-prefixed names, e.g.
  `feedback/<slug>` for how-to-work-with-the-user notes, `project/<slug>` for project context.
- **Format:** every memory you write MUST be **markdownlint-clean** against `.markdownlint.json`
  (blank line after headings and around lists, angle-bracket bare URLs like `<http://…>`, lines
  ≤120 chars). Verify with `npx markdownlint-cli2 ".serena/memories/**/*.md"` (add `--fix` for the
  structural rules; wrap long lines by hand).
- Do **not** write new memories into the built-in store for this project.

## What this is

**MedAssist.AI** — a bilingual (EN/BG) RAG medical knowledge assistant for physicians. It answers
clinical questions over OCR'd medical books using hybrid dense + sparse (BM25) vector search with
RRF fusion and a cross-encoder reranker, then generates a cited answer via a local LLM (Ollama).

## Architecture (.NET 10, `MedAssist.slnx`)

`MedAssist.Tests` uses in-memory **SQLite** (not the EF InMemory provider, which hides SQL-translation bugs).

Layering is acyclic and top-down: Shared ← Data ← AI ← Web. Tests reference all four.

### Query flow

`Browser → Blazor → QueryService → RAG plugin` → query translation plus the books' own terms (`BookTermLookup`) → ICD query expansion → dense embed + BM25 sparse →
`QdrantVectorStore` (dense + sparse prefetch → RRF fusion) → cross-encoder rerank → named-condition guard
(`ConditionNameGuard`, refuses when the excerpts never mention the condition) → Ollama → answer + citations. Optional trusted web search (SearXNG) is SSRF-guarded via `WebFetchPolicy`.

### Evaluation flow

Admin starts a run at `/admin/evaluation` → `EvaluationCoordinator` → `EvaluationWorker` → `EvaluationRunner`
sends `MedAssist.AI/Evaluation/golden-set.json` through `QueryService` inside a `RagRunScope` (per-run model and
context overrides, plus a trace of what was retrieved) → local LLM judge → results and summary in Postgres,
compared with a baseline. Run it after any prompt, model, retrieval or context change.

### Ingestion flow

Admin uploads a PDF or a zip of Word parts → `IngestionQueue` (a `Channel`) → `IngestionWorker`
(`BackgroundService`) → markdown with page markers (PDF: shared MinerU OCR; Word parts: `DocxBookConverter`, no
OCR, regenerated from all parts on every index) → chunk (page ranges per chunk) → ICD-10 enrich → dense + BM25
sparse vectors → Qdrant upsert (deterministic point ids) + Postgres status. Indexing is resumable via checkpoints.

## Infrastructure — split stack

Shared infra (PostgreSQL, Qdrant, Ollama, SearXNG, MinerU OCR, OpenTelemetry, Grafana/Prometheus/pgAdmin) is
provided by the sibling **PersonalCommandCenter (PCC)** stack. This repo's `docker-compose.yml` builds only the
`web` service, joined to PCC's external `personalcommandcenter_default` network.

**Start PCC first** — `pcc-net` is an external network, so `docker compose up` here fails if PCC
isn't up. The `medassist` database is auto-created by EF migrations on first run. See `README.md`.

## Conventions

- Constants are centralized in `MedAssist.Shared/Constants`.
- FastEndpoints use `Send.*` response methods (the endpoint-response-pattern spec).
- Nullable enabled, file-scoped namespaces, `Async` suffix on async methods.
- `config/` and `books/` are git-crypt encrypted at rest.

## Deployment model (informs accepted security trade-offs)

Built and deployed **locally only** — no remote registry — and reachable solely through an internal
HTTPS-terminating proxy on the docker network. Given this, several audit findings are accepted
risks (committed JWT key, committed dev admin credential, plaintext-on-the-internal-network,
config baked into the image). A Production startup guard (`JwtKeyPolicy`) still rejects the
*placeholder* JWT key so it can't be shipped silently. Revisit these if the app is ever exposed to
an untrusted audience. See `openspec/changes/audit-remediation/` for the full audit + dispositions.
