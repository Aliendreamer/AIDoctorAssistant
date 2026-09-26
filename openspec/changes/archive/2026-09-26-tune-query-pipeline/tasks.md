## 1. Fast calls (TDD)

- [x] 1.1 Test + add `think = false` to the shared answer `ExecutionSettings`
- [x] 1.2 Test + add a `QueryPrepSettings` (same `num_ctx`, `think = false`, `num_predict = 96`,
      `temperature = 0`) with the cap as a constant in `PromptConstants`

## 2. Query preparation (TDD)

- [x] 2.1 Test the "needs preparation" rule: Cyrillic non-follow-up → unchanged, no call; Latin-script
      question or follow-up → one call
- [x] 2.2 Replace `MaybeRewriteQueryAsync` with `PrepareSearchQueryAsync` (rewrite + translate prompt,
      `QueryPrepSettings`), keeping the original question as a second retrieval term
- [x] 2.3 Rerank by chunk language: Bulgarian chunks against the search query, others against the
      original; merge by score (test with a fake reranker)
- [x] 2.4 Run the Latin-term guard on the search query; tests for generic English words and the
      "Chiari" eponym

## 3. Context budget (TDD)

- [x] 3.1 Add `AnswerReserveTokens` (2,048) and the `chars / 2` estimate to `PromptConstants`
      (first cut was 1,536 and `chars / 3`; recalibrated after run 3, see design.md)
- [x] 3.2 Tests: under budget unchanged; history trimmed before excerpts; lowest-ranked excerpts
      dropped last, at least one kept
- [x] 3.3 Implement the budget in `AnswerPromptBuilder`; build `sources` from the kept excerpts only
      (citation-contract test)
- [x] 3.4 `dotnet build` with 0 warnings; `dotnet test` green

## 4. Verify live

- [x] 4.1 Rebuild `web`; run golden set 3 (G1–G12 Bulgarian + E1–E6 English, fresh eval user) —
      `run3.md`; after recalibrating the budget, run 4 — `run4.md`
- [x] 4.2 Check the Ollama log: rewrite/translate calls decode ≲ 96 tokens; no answer prompt above
      the budget; no `truncated = 1`; compare `task.n_tokens` with the `chars / 3` estimate
- [x] 4.3 Gate: English questions answer from the books (markers, correct English answer language);
      Bulgarian results no worse than run 2; follow-up latency down
- [x] 4.4 Record the results in `comparison.md` and `ROADMAP.md`
