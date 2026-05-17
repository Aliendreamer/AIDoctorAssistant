## Context

Greenfield .NET 10 solution for medical professionals. Source material is scanned OCR PDFs of Bulgarian and English medical textbooks (mixed Cyrillic/Latin script). The team has prior experience with Qdrant, Docling, and RAG pipelines from the DndMcpAICsharpFun project. Semantic Kernel is new to this project and replaces Microsoft.Extensions.AI for AI orchestration to gain model-agnosticity and plugin composability.

## Goals / Non-Goals

**Goals:**
- 4-project solution with clean dependency graph (Shared ← AI ← Web; Shared ← Indexer)
- Resumable ingestion: process large books in chunks of 50, survive restarts
- Bilingual retrieval: query expansion via ICD-10 dictionary covering BG↔EN illness names
- Model-agnostic AI layer: swap Ollama/Qwen for Azure OpenAI or Anthropic with one config change
- Full observability from day one: structured logs, distributed traces, Prometheus metrics
- Docker Compose deployment of all runtime services

**Non-Goals:**
- Authentication / multi-user access (later phase)
- Mobile client (Blazor first, mobile added later)
- Real-time document watching / auto-ingestion (manual trigger only)
- Fine-tuning or training custom models
- Medicine dosage calculations (phase 2)

## Decisions

### D1: Solution structure — 2 libraries + 1 Blazor app + 1 Worker
**Decision:** `MedAssist.Shared` (library) → `MedAssist.AI` (library) → `MedAssist.Web` (Blazor Server). `MedAssist.Indexer` (Worker Service) references only `MedAssist.Shared`.

**Rationale:** AI and Indexer are deliberately decoupled — Indexer only writes vectors, AI only reads them. This allows independent scaling and replacement. Blazor Server co-locates UI and server logic for simplicity; minimal API endpoints can be added later for mobile without restructuring.

**Alternative considered:** Separate Web API + Blazor WASM — rejected as over-engineering for initial phase with no mobile client yet.

### D2: Docling as offline preprocessor only (not in docker-compose runtime)
**Decision:** Docling runs as a one-time CLI tool to convert PDFs → markdown files stored on disk. The Indexer reads markdown, not PDFs.

**Rationale:** Medical books are static — process once, store output. Removes a heavy ~3GB container from the production stack. Docling's layout-aware OCR reconstruction is essential for Cyrillic scanned pages with complex formatting; simpler alternatives (PdfPig) were tried and abandoned in prior project.

**Alternative considered:** Docling as compose sidecar — rejected because runtime dependency on a heavy GPU-accelerated container adds operational complexity with no benefit for static book content.

### D3: Semantic Kernel for AI orchestration
**Decision:** Use `Microsoft.SemanticKernel` with `IKernelBuilder` registered in DI. Model backend configured via `appsettings.json` (`ModelProvider`: `ollama` | `azure-openai` | `anthropic`).

**Rationale:** SK provides plugin abstraction, prompt template management, and first-class support for RAG patterns. Model switching is a stated requirement; SK's kernel builder makes this a config change rather than a code change.

**Alternative considered:** Microsoft.Extensions.AI (used in DnD project) — sufficient for simple chat but lacks SK's plugin/planner composability needed for 3 distinct query-type plugins.

### D4: Single Qdrant collection with payload filters (not per-language collections)
**Decision:** One collection `medical_books`. Language stored as payload field `language: "bg" | "en"`. Book scoping via `book_id` payload filter.

**Rationale:** Simpler to manage, cross-language semantic similarity search works better across one collection (a Bulgarian query can find semantically similar English chunks). Per-language collections would require explicit fan-out queries.

**Alternative considered:** Separate `medical_books_en` / `medical_books_bg` collections — rejected; complicates query routing and prevents cross-language semantic retrieval.

### D5: Multilingual embedding model
**Decision:** `intfloat/multilingual-e5-large` run locally via ONNX Runtime.

**Rationale:** Supports Bulgarian (Cyrillic) and English in the same embedding space, enabling cross-lingual semantic search. E5-large gives best quality; ONNX Runtime avoids a separate embedding service. Same ONNX pattern already proven in DnD project.

**Alternative considered:** `paraphrase-multilingual-mpnet-base-v2` — lighter but lower quality for medical terminology. Rejected in favour of accuracy.

### D6: SQLite for books registry, dictionary, and checkpoints
**Decision:** Single `medassist.db` SQLite file owned by `MedAssist.Indexer`. Tables: `books`, `illnesses`, `illness_aliases`, `ingestion_checkpoints`. `MedAssist.Web` reads `books` and `illnesses` (read-only).

**Rationale:** Medical book library is small (tens of books). SQLite is zero-infrastructure, human-inspectable, and sufficient. Qdrant is for vectors only — non-vector metadata belongs in a relational store.

**Alternative considered:** Separate JSON files per concern — rejected; SQL queries for illness lookup and alias expansion are cleaner than manual JSON parsing.

### D7: ICD-10 codes as cross-reference key
**Decision:** Each illness in the dictionary carries an `icd_code` (ICD-10). At index time, chunks are tagged with matching ICD codes. At query time, illness name → ICD code → expanded query terms (BG + EN names + aliases).

**Rationale:** ICD-10 is the universal medical standard. One code links the same condition across Bulgarian and English books regardless of naming variation. Also future-proofs for ICD-11 migration.

### D8: Checkpoint granularity — every 50 chunks
**Decision:** Indexer commits checkpoint to SQLite after every 50 successfully upserted vectors. On restart, reads last checkpoint and resumes from `last_chunk_index + 1`.

**Rationale:** 50 chunks ≈ a few pages of a medical book — small enough to not lose much work on failure, large enough to avoid checkpoint overhead dominating ingestion time.

## Risks / Trade-offs

- **Docling OCR quality on Cyrillic** → Mitigation: validate output markdown on a sample book before full ingestion; post-process common OCR errors (medical term dictionary can catch mismatches)
- **Multilingual-E5-Large ONNX size (~1.2GB model)** → Mitigation: downloaded once, cached in Docker volume; Indexer and Web share the same model path via volume mount
- **Semantic Kernel preview pace** → SK moves fast; pin exact package versions, review changelog before upgrades
- **Blazor Server state on reconnect** → For initial phase (single user / small team) this is acceptable; proper session handling added before wider rollout
- **SQLite concurrent reads** → Web reads books/illnesses while Indexer writes checkpoints; use WAL mode to avoid read-write contention

## Migration Plan

1. Scaffold solution and projects (no runtime dependencies needed)
2. Stand up docker-compose (Qdrant + Ollama + Prometheus + Grafana) and verify connectivity
3. Run Docling offline on sample book → inspect markdown quality
4. Build and run Indexer on sample book → verify Qdrant vectors with correct payload
5. Build Web UI → verify end-to-end query against indexed sample
6. Index full book library

Rollback: drop Qdrant collection, delete SQLite file, re-run Indexer from scratch (idempotent by design).

## Open Questions

- Which Qwen model variant to start with? (`qwen2.5:7b` is the sweet spot for quality vs. RAM on a dev machine)
- Should the illness dictionary be seeded from a public ICD-10 dataset or populated manually as books are indexed?
- Grafana dashboard: import a community medical/API dashboard or build from scratch?
