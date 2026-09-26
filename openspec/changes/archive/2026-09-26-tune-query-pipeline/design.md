## Context

Current flow in `RagPluginBase.ExecuteSearchAsync`:

1. `MaybeRewriteQueryAsync` — only for short follow-ups with history; an LLM call with `/no_think`.
2. ICD dictionary expansion of the search query.
3. Hybrid retrieval with `[query, searchQuery]`, section expansion.
4. Cross-encoder rerank against `searchQuery`; if the top score is below `MinRetryScore`, flag web
   fallback and stop.
5. Iterative widening, a final `MinAnswerScore` floor, and the Latin-term guard over the top 3.
6. `AnswerPromptBuilder.BuildBookAnswer` with the top 5 non-summary chunks and up to 6,000 chars of
   history.

Evidence from the golden runs (see `streamline-rag-prompts/comparison.md`): English questions fail at
step 4 or step 5; the rewrite call emits ~1,000 tokens; answer prompts reach 8,191 of 8,192 tokens.

## Goals / Non-Goals

**Goals:**

- English (and other non-Bulgarian) questions retrieve and answer from the Bulgarian books.
- Follow-up rewrite costs ~1–2 s, not ~20 s.
- An answer prompt never exceeds the context window minus an answer reserve.

**Non-Goals:**

- Changing thresholds (`MinRetryScore`, `MinAnswerScore`) or the embedder / reranker models.
- Translating the *answer* or the excerpts.
- Handling a mixed corpus perfectly — only keep the design correct if English books are added.

## Decisions

### 1. One "prepare query" call replaces the rewrite

`PrepareSearchQueryAsync(query, history)` returns the Bulgarian search query:

- Question is Cyrillic and not a follow-up → return it unchanged, **no LLM call**.
- Otherwise one call: *"Rewrite as a concise, self-contained Bulgarian medical search query. Use the
  previous question for context if this is a follow-up. Keep eponyms, drug names and ICD codes exactly
  as written. Output only the query."*

*Why one call:* translation and rewriting are the same operation (question → search query), and it
keeps English questions at +1 call. *Alternative:* a separate translation service or dictionary
lookup — rejected; the local model is already there and handles medical Bulgarian.

### 2. Fast-call settings

A second shared `PromptExecutionSettings` for the prepare call: `num_ctx` (same 8,192 — a different
value would force Ollama to reload the model), `think = false`, `num_predict = 96`,
`temperature = 0`. All answer calls also get `think = false`. `/no_think` stays in the prompts as a
harmless fallback for models that ignore the option.

### 3. Language-aware reranking

Candidates carry `MedicalChunk.Language`. Rerank Bulgarian candidates against the Bulgarian search
query and any other-language candidates against the original question, then merge by score (same
model, comparable scale). With today's all-Bulgarian corpus this is a single call against the
Bulgarian query. *Alternative:* always rerank against the Bulgarian query — simpler, but would
re-create today's bug for English books.

### 4. Latin-term guard on the search query

Run `ExtractLatinTerms` on the prepared Bulgarian query. Eponyms and drug names survive translation
in Latin script, so the guard keeps its purpose (catching domain drift on "Chiari"-style terms) while
generic English words disappear. For a Bulgarian question without rewrite nothing changes.

### 5. Token budget for the answer prompt

`AnswerPromptBuilder` estimates tokens as `chars / 2`. Budget =
`ContextWindowTokens − AnswerReserveTokens (2,048)` = 6,144 tokens.

*Calibration (after golden run 3):* the first cut used `chars / 3` and a 1,536 reserve; prompts still
reached 7,984–8,191 tokens with 3 context shifts. Measured with qwen3's tokenizer via Ollama
(`prompt_eval_count`): Bulgarian text is **2.1–2.3 chars/token**, English ~4.6. `/ 3` under-counted
Cyrillic by ~40%. One answer also decoded 2,116 tokens, above the old reserve. If the prompt is over budget: drop oldest
history exchanges first, then the lowest-ranked excerpts, never below one excerpt. Excerpt numbering
stays contiguous, and `sources` is built from the kept excerpts only (citation contract).

*Why history first:* excerpts are the evidence; history is context the rewrite already folded into
the search query.

## Risks / Trade-offs

- [Translation drifts from the question's meaning] → the original question still anchors retrieval
  (step 3 keeps both terms); golden run 3 checks English answers against their Bulgarian twins.
- [`think` option not honoured through SK → OllamaSharp] → verify in the Ollama log (`n_decoded`
  for the rewrite call ≲ 60); fall back to a `num_predict` cap alone, which still bounds the cost.
- [The estimate drifts from the real tokenizer] → calibrated in run 3; run 4 checks the Ollama log.
- [Budget drops an excerpt the answer needed] → it only triggers on prompts that would otherwise be
  truncated, which already lost content silently.

## Open Questions

- Should the web-fallback path also use the Bulgarian search query? Not needed today: web search is
  opt-in and SearXNG handles English well.
