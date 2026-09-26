# Golden-run comparison — tune-query-pipeline

Runs 1–2 are from `streamline-rag-prompts`. Runs 3–4 add six English questions (E1–E6), asked after
the twelve Bulgarian ones so G1–G12 see the same history as run 2. Each run uses a fresh user.

| | Run 2 | Run 3 | Run 4 |
| --- | --- | --- | --- |
| Change | 8k context, language line last | + query prep, language-aware rerank, budget (`chars / 3`, 1,536 reserve) | + calibrated budget (`chars / 2`, 2,048 reserve) |
| File | `../2026-09-26-streamline-rag-prompts/run2-8k-context.md` | `run3.md` | `run4.md` |
| Markers / in range / language | 12/12 | 18/18 | 18/18 |
| English questions answered from books | 0/6 (run 1) | 6/6 | 6/6 |
| List / markdown leakage | 0/12 | 0/18 | 0/18 |
| Mean latency | 37.8 s | 21.2 s | 17.6 s |
| Largest answer prompt (Ollama `task.n_tokens`) | 8,191 | 8,191 | 5,221 |
| Context shifts / `truncated = 1` | 2 / 2 | 3 / 3 | 0 / 0 |
| Query-prep call output | ~1,000 tokens | 12–43 tokens | 12–43 tokens |

## Findings

- **English questions work.** Translating to a Bulgarian search query and reranking against it takes
  every English question past the `MinRetryScore` gate and the Latin-term guard. Answers stay in
  English, with in-range citations to the Bulgarian books.
- **`think: false` is honoured** through SK → OllamaSharp. The prep call decodes 12–43 tokens instead of
  ~1,000. Answer calls also got faster (37.8 s → 17.6 s mean), consistent with hidden reasoning being
  generated and then stripped before.
- **The first budget cut was wrong.** Measured via Ollama `prompt_eval_count`: Bulgarian text is
  2.1–2.3 chars/token, English ~4.6. `chars / 3` under-counted Cyrillic by ~40%. At `chars / 2` no
  prompt exceeded 5,221 tokens and nothing was truncated.
- **Cost of the budget:** several answers kept 4 excerpts instead of 5. The estimate over-counts
  slightly (5.2k real vs a 6.1k budget), so ~900 tokens of headroom are unused. It can be tuned later
  if the fifth excerpt matters.
