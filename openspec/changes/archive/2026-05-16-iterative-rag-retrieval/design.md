## Context

The RAG pipeline performs hybrid dense+sparse search via Qdrant, reranks candidates with a cross-encoder (ms-marco-MiniLM-L-6-v2), and returns the top-5 chunks. The cross-encoder outputs a raw logit: positive = relevant, negative = not relevant, 0.0 = decision boundary. Before this change the pipeline made no use of that score beyond ordering — there was no mechanism to recover when all candidates scored poorly.

Two confirmed failure cases from live testing against a Bulgarian paediatric textbook:
- "Болест на гравес" — BM25 tokenises the full Cyrillic phrase, missing Latin "Graves" in the index
- "Херниране тонзили форамен магнум" — relevant chunk present but ranked 3rd; better candidates existed deeper in the index

## Goals / Non-Goals

**Goals**
- Stop the loop early when the top candidate clears a configurable logit threshold
- Widen the search space progressively when confidence is low (more candidates, broader language filter, shorter query terms)
- Make all loop parameters configurable via `appsettings` with safe defaults
- Hard-cap iterations at 5 regardless of configuration

**Non-Goals**
- LLM-based query reformulation (no Ollama call in the loop)
- Synonym expansion via a manually maintained dictionary (rejected: maintenance burden across two scripts)
- Chunk overlap changes (postponed: language-specific sentence boundaries make fixed overlap unreliable)

## Decisions

### 1. Score exposure: return `ScoredChunk` from the reranker

**Decision**: Change `ICrossEncoderReranker.RerankAsync` to return `IReadOnlyList<ScoredChunk>` (a `readonly record struct` wrapping chunk + float score).

**Alternatives considered**:
- Add a `Score` property to `MedicalChunk` — rejected: score is a reranking artifact, not a chunk attribute; it would change across queries
- Keep `RerankAsync` unchanged and add a separate `ScoreAsync` method — rejected: double the ONNX inference cost

**Rationale**: The logit is ephemeral and query-dependent. A wrapper struct keeps the domain model clean.

### 2. Five fixed retry strategies, no dynamic reformulation

**Decision**: Encode exactly 5 widening strategies as a static array of `RetryStrategy(TopK, AnyLanguage, LongestOnly)` records inside `RagPluginBase`.

| Strategy | TopK | Language | Terms |
|----------|------|----------|-------|
| 0 | 10 | Same as query | All expanded |
| 1 | 10 | Any (EN+BG) | All expanded |
| 2 | 15 | Same as query | All expanded |
| 3 | 15 | Any | All expanded |
| 4 | 20 | Any | Longest keyword only |

**Alternatives considered**:
- Dynamic reformulation with a local LLM — rejected: adds latency and an Ollama dependency to the hot query path
- Exponential topK growth — rejected: unpredictable candidate pool size; fixed steps are easier to reason about

**Rationale**: Fixed strategies are deterministic, easy to test, and cover the main failure modes (too few candidates, wrong language, overly specific phrase).

### 3. Keyword extraction in `ExpandQueryAsync`

**Decision**: Tokenise the query on whitespace/punctuation, filter stopwords (Bulgarian + English function words), and add surviving words as independent search terms — even without a dictionary match.

**Rationale**: "болест на гравес" → separate "гравес" embedding query → higher cosine similarity to Graves content than the full phrase. This fixes the Cyrillic phrase mismatch at zero infrastructure cost.

### 4. RagOptions bound from configuration, capped in DI

**Decision**: Read `Rag:ConfidenceThreshold` (float, default 0.0) and `Rag:MaxIterations` (int, default 2) in `ServiceCollectionExtensions`. Apply `Math.Min(value, 5)` cap at binding time so the cap is enforced once, not scattered through the loop.

## Risks / Trade-offs

**Latency increase on low-confidence queries** → Each fallback pass adds one embed+Qdrant round-trip (~100-200 ms). With MaxIterations=2 the worst case adds ~400 ms. Mitigated by the early-exit on threshold and by keeping the default at 2 iterations.

**All strategies may return the same cached chunk** → When all Qdrant searches return the same top-k items, deduplication leaves the candidate pool unchanged and re-ranking produces identical results. The loop still terminates after MaxIterations. Not harmful, just wasted work. Mitigated by increasing topK across strategies.

**Threshold calibration is manual** → The default 0.0 (decision boundary) is conservative. If too many queries iterate unnecessarily, raise the threshold. If too many stop too early, lower it. No automatic calibration is provided.

## Migration Plan

1. Deploy the new image — `appsettings.shared.json` contains safe defaults (`ConfidenceThreshold: 0.0`, `MaxIterations: 2`)
2. No database migrations required
3. Rollback: revert image; no state to clean up

## Open Questions

- Should `ConfidenceThreshold` be per-plugin (Symptoms vs Disease vs Treatment) rather than global? Currently global for simplicity.
- Is a Prometheus counter for "iterations used" worth adding to the existing `MedAssist.Web` meter? Left for a follow-up observability pass.
