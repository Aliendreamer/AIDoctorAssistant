# Roadmap

Deferred work and known follow-ups that aren't yet a formal OpenSpec change. Implemented work is
archived under `openspec/changes/archive/`; this file tracks tuning and ideas that are quality
levers rather than defects.

## Follow-ups

### Citation-marker reliability — from `cited-answer-markers`

Inline `[n]` citation markers are implemented and render correctly (superscript markers tied to the
numbered source list, range-guarded, graceful degradation when absent). **Emission is model-bound**,
not a code gap: the local `qwen3:8b` produced no markers from the system-prompt instruction alone
and only complied once a citation reminder was repeated at the *end* of the user message — and even
then not on every query. Because rendering degrades gracefully (no markers → clean prose), this is a
consistency lever, not a bug. Options to make markers more reliable:

- **Larger / more instruction-tuned local model** in the shared Ollama (the simplest lever).
- **Stricter prompt** — e.g. a per-query-type one-shot example, or a firmer "cite every factual
  paragraph" directive, accepting some rigidity/latency.
- **Post-generation citation pass** — a second, cheap LLM call that inserts `[n]` into the finished
  prose, if determinism matters more than cost/latency.

### Medical-document model upgrade — PaddleOCR-VL 1.6 + MedGemma 1.5 4B

Evaluate swapping the two model-bound stages of the pipeline for medically/structurally stronger
models, keeping the surrounding RAG plumbing intact.

- **Ingestion OCR: Marker → PaddleOCR-VL 1.6.** A vision-language OCR model as an alternative to
  Marker for PDF→Markdown. Motivation: better layout, table, and formula extraction and multilingual
  coverage (relevant to the EN/BG corpus). Bulgarian is explicitly in its 109-language set (Cyrillic
  scripts incl. Russian/Ukrainian/Serbian/**Bulgarian**), so Cyrillic is not a concern. Would slot
  into the ingestion flow at the OCR step; needs a GPU-service equivalent to the current `marker`
  container and a quality comparison on real book scans before committing.
- **Answer generation: `qwen3:8b` → MedGemma 1.5 4B.** A medical-domain-tuned generation model in
  the shared Ollama. Motivation: clinical accuracy on physician-facing answers, and it directly
  serves the citation-marker reliability lever above (a "more instruction-tuned local model" that may
  emit `[n]` markers more consistently). Also multimodal, so it could later reason over figures.
  **Bulgarian caveat:** the Gemma 3 base is multilingual and MedGemma preserves non-English ability
  (demonstrated in Chinese/Dutch), but its model card notes non-English performance "may benefit from
  additional local fine-tuning" and there is no Bulgarian-specific evidence — the medical uplift may
  not fully carry into BG, and it could trail `qwen3:8b` there. Gate adoption on an explicit
  Bulgarian answer-quality eval, not an assumed win.

**Scope note — MedGemma is a *generation* lever, not a *retrieval* one.** It runs at the end of the
flow and only sees chunks that dense embed + BM25 + RRF + the cross-encoder reranker have already
selected, so it does not change *what* gets retrieved — only the quality of the answer written from
it. Making retrieval more medicine-aware is a **separate** line of work, tracked below.

Both changes above are drop-in at their respective interfaces (OCR worker and the Ollama generation
call) rather than architectural changes. Sequence as two independent spikes — measure OCR quality
and answer quality separately against the current stack before adopting either.

### Medicine-aware retrieval — distinct from the MedGemma generation lever

Retrieval quality is governed by the embedder, sparse/fusion, and reranker — not the generation
model. If we want domain-specific gains in *what* surfaces (not just how it's written up), the levers
are:

- **Medical text embedder** (replacing `multilingual-e5-large`) — the main recall lever. **Cyrillic
  is the blocker here:** most medical embedders (BioBERT/PubMedBERT/MedCPT-style) are English/
  PubMed-only and would silently drop Bulgarian. Any candidate must be verified to cover EN/BG;
  benchmark against e5 on real clinical queries before committing, and note a dimension/model change
  means re-embedding the whole corpus (Qdrant re-index).
- **Medical cross-encoder reranker** — a domain-tuned reranker in place of the current
  `mmarco-MiniLM` cross-encoder, to reorder the fused candidate set more clinically. Same Cyrillic
  risk: `mmarco-MiniLM` is multilingual, so confirm any medical replacement handles BG before
  swapping — the current multilingual coverage is an asset not to trade away blindly.
- **LLM-based query expansion / rewriting** — an *upstream* use of a medical LLM (MedGemma or other)
  to expand abbreviations and synonyms or generate a HyDE-style hypothetical answer before retrieval,
  augmenting the existing deterministic ICD-code expansion. This is the one place a medical
  generative model *can* help retrieval, and it is optional/additive.

### Embedding-model trials — candidates to benchmark against `multilingual-e5-large`

Current embedder is `intfloat/multilingual-e5-large` (local ONNX, 1024-dim, EN/BG-capable), wired to a
1024-dim Qdrant collection. These are stronger general multilingual embedders to trial for better
recall while keeping Bulgarian — a different axis from the *medical* embedder above (these keep BG
rather than risking it). Every swap requires re-embedding the whole corpus; a dimension change also
means recreating the Qdrant collection. Prefer a held-out EN + BG clinical query set and measure
recall/nDCG before committing.

- **`multilingual-e5-large` (current) / e5-multilingual family** — baseline. Cheapest upgrade within
  the family is `multilingual-e5-large-instruct` (same arch, 1024-dim, drop-in — no collection change,
  only re-embed).
- **BGE-M3** — multilingual (100+ langs incl. BG), **1024-dim so dimensionally drop-in**. Notable: one
  model yields dense + sparse + multi-vector (ColBERT) — could potentially unify today's separate
  dense + BM25 paths. Strong candidate; local-runnable.
- **Qwen3 Embedding** — Qwen3-Embedding series (0.6B/4B/8B), multilingual, instruction-aware, top MTEB
  scores, Matryoshka (configurable output dims). Larger variants cost more VRAM/latency; pick a dim
  and re-create the collection accordingly. Local-runnable.

All candidates must run **locally** (ONNX / self-hosted) — cloud embedding APIs are out of scope, in
keeping with the local-only / no-external-exposure deployment model for clinical data.

Verdict order to trial first: BGE-M3 (drop-in dim, unifies dense+sparse) and e5-large-instruct
(cheapest), then Qwen3 if quality justifies the re-index.
