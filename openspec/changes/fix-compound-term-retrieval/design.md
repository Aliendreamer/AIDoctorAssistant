## Context

`RagPluginBase.ExecuteSearchAsync` calls `_dictionary.ExpandQueryAsync(query)` to get a list of search terms, then passes **all of them** to the initial `GatherCandidatesAsync` call. Each term triggers a full dense+sparse Qdrant search. For a query like "Хернирането на малкомозъчните тонзили", `ExtractKeywords` produces `["Хернирането", "малкомозъчните", "тонзили"]`. The fragment "тонзили" independently matches a large body of ENT/tonsil chunks, which dominate the candidate pool before reranking even runs.

The iterative retry loop already exists to progressively widen search — individual keywords are a natural fit for later-stage fallback, not the first pass.

## Goals / Non-Goals

**Goals:**
- Initial candidate gather uses only the full query as the search term
- Dictionary-expanded synonyms (e.g. "Arnold-Chiari" ↔ "малформация на Арнолд-Киари") still participate in the initial pass — they expand the *meaning*, not fragment it
- Per-keyword sub-searches used only in retry iterations when confidence is below threshold
- No index rebuild, no schema change, no API change

**Non-Goals:**
- Changing the medical dictionary data or synonym coverage
- Fixing the cross-encoder reranker scoring
- Adding NLP-based noun phrase extraction

## Decisions

### D1: Use full query + dictionary synonyms for initial gather; keywords only in retries

**Chosen:** Pass `[query] + dictionaryExpansions` to the first `GatherCandidatesAsync`, where `dictionaryExpansions` are only the terms added by the dictionary lookup (not the per-word fragments from `ExtractKeywords`). In retry iterations, use the full `expandedTerms` list including keywords.

**Alternative considered:** Remove `ExtractKeywords` entirely from `ExpandQueryAsync`. Rejected — individual keywords are still useful in the retry fallback and for dictionary lookups against single-word disease names.

**Alternative considered:** Add a minimum phrase-length filter (skip keywords < 2 words). Rejected — too brittle; single-word disease names like "тонзилит" are valid search terms in the right context.

**How to split:** `ExpandQueryAsync` returns all terms. The caller (`ExecuteSearchAsync`) separates them:
- *Phase 1 terms*: the original `query` plus any terms that were added by dictionary lookup (i.e. `expandedTerms` minus the fragments produced by `ExtractKeywords`)
- *Phase 2+ terms*: full `expandedTerms` (all keywords included)

The cleanest implementation: return two lists from the dictionary, or simply use the full query alone for phase 1 and pass `expandedTerms` starting from iter 0 of the retry loop.

**Simplest viable approach:** Use only `query` (single term) for the initial gather. Dictionary synonyms are already included in `expandedTerms` and will fire in iter 0 of the retry loop (which runs immediately if confidence < threshold). Given that `ConfidenceThreshold = 0.0` in current config, the retry loop always runs at least once anyway — making this change safe.

### D2: No changes to `MedicalDictionaryService.ExpandQueryAsync`

The dictionary service correctly expands by synonym lookup. The fragmentation problem is in how the caller uses the result. Keeping the service unchanged avoids breaking the dictionary search endpoint and `ChunkEnricher`.

## Risks / Trade-offs

- **Slightly fewer initial candidates for simple single-word queries** — for a query like "тонзилит", the initial pass now only searches "тонзилит" instead of also searching "tonsillitis" + aliases in the first pass. Mitigated: retry iter 0 fires immediately with all expanded terms since threshold is 0.0.
- **Retry loop iter 0 does more work** — it now carries the keyword/synonym load that was previously in the initial pass. Acceptable; iter 0 already runs on every query given current threshold settings.

## Migration Plan

1. Change `ExecuteSearchAsync` — single line: pass `[query]` instead of `expandedTerms` to the initial `GatherCandidatesAsync` call
2. Redeploy web container
3. No rollback risk — change is isolated to the gather phase, no data modified
