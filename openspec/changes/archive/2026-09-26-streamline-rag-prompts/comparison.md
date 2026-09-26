# Golden-run comparison

Both runs use the new (de-duplicated) prompts against the live stack (`qwen3:8b`, ten Bulgarian
paediatrics books). A run on the *old* prompts was not made — building a second image from `main`
was declined — so the comparison isolates the context-window fix and the language-line move, and
the prompt de-duplication is covered by unit tests plus the token figures below.

| | Run 1 — 4k context | Run 2 — 8k context, language line last |
| --- | --- | --- |
| File | `run1-4k-context.md` | `run2-8k-context.md` |
| Questions reaching the answer prompt | 5/12 (G1–G6 were English → web fallback; G7 timed out) | 12/12 |
| Answers with `[n]` markers | 5/5 | 12/12 |
| All markers in range | 5/5 | 12/12 |
| Correct answer language | 2/5 (G10–G12 answered in English) | 12/12 |
| List / markdown leakage | 0/5 | 0/12 |
| Request timeouts (504) | 1 (G7) | 0 |
| Mean latency (answered) | 42.1 s (G8–G12) | 37.8 s |

## What the Ollama log showed

- **Run 1:** answer prompts reached **4,095 tokens** — clipped at Ollama's default 4,096 window. Ollama
  drops the *start* of an over-long prompt, so the system prompt and the (then first) language line
  were lost; the model answered in English. Generation then overflowed and a context shift discarded
  2,045 tokens mid-answer (`truncated = 1`). Every wrong-language answer had a clipped prompt.
- **Run 2:** `n_ctx_slot = 8192`. Answer prompts were 2,682–7,211 tokens (median ≈ 4,900), dominated by
  excerpts and history. One prompt still hit **8,191** and two calls context-shifted — 8k is enough
  for most queries but borderline for the largest.

## Follow-ups found

- **Context headroom:** consider trimming excerpt count/length for the largest queries, or 12k context
  if VRAM allows, rather than relying on the tail-placed instructions surviving truncation.
- **Query rewrite runs away:** the follow-up rewrite call (≈110–140-token prompt) generates ≈1,000
  tokens — qwen3 appears to ignore `/no_think` there — adding ~20 s to every follow-up. Candidate fix:
  Ollama's `think: false` request option or a small `num_predict` cap.
- **English questions never reach the prompt** against this all-Bulgarian corpus: reranker scores fall
  below `MinRetryScore` (1.5) and the query falls back to web search. Retrieval, not prompts.
- **`DELETE /api/chat/history/{type}` is broken** — returns 400 or fails serialising a `Task`.
