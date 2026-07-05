## Context

Post-implementation performance pass. The query path's dominant cost is ONNX + the LLM, so these
changes target the surrounding glue, the vector store, and the data layer rather than the models.

## Goals / Non-Goals

**Goals:** remove avoidable allocations/scans on the query path; cut ingestion round-trips; configure
the Qdrant dense index to scale in memory as the corpus grows, without losing retrieval accuracy.

**Non-Goals:** algorithmic changes to retrieval/ranking; changing the embedder/reranker/LLM.

## Decisions

- **`AsNoTracking` only on read-only queries** — load-to-mutate queries stay tracked; correctness
  over blanket application.
- **`UpsertBatchAsync` as a default interface method** — Qdrant overrides it with a real batch; test
  fakes inherit the per-point fallback unchanged.
- **int8 quantization + on-disk originals + rescoring** — the codes (RAM) drive fast search; the
  on-disk originals are read only to rescore the oversampled top candidates, keeping accuracy near
  float32. Rescore params are ignored by Qdrant on a not-yet-quantized collection, so back-fill is
  safe.

## Risks / Trade-offs

- **[Quantization accuracy]** → rescoring re-ranks with full-precision vectors; oversampling 2×.
- **[On-disk originals latency]** at tiny corpus sizes → negligible (OS page cache); the config is
  aimed at the growing corpus.
- **[Back-fill timing]** the existing collection is tuned on the next ingestion (gated once/process),
  not at startup → acceptable; search is safe in the interim.
