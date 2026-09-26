## Why

The golden runs for `streamline-rag-prompts` exposed three problems in the query pipeline:

- **English questions never get an answer.** The corpus is Bulgarian. The cross-encoder scores
  English-query / Bulgarian-chunk pairs far below `MinRetryScore` (−9.5 to 0.8), so the query falls
  back to web search. The Latin-term guard then rejects anything left, because ordinary English words
  ("disease", "treatment") never appear verbatim in Bulgarian text. One question spent 167 s on
  retries before being rejected.
- **The follow-up rewrite call runs away.** qwen3 ignores `/no_think` on the ~120-token rewrite
  prompt and generates ~1,000 tokens of reasoning for a one-line query, adding ~20 s to every
  follow-up question.
- **There is no context headroom.** With `num_ctx = 8192`, answer prompts still reached 8,191 tokens
  and two calls context-shifted, because excerpts and history are not budgeted against the window.

## What Changes

- **Query preparation, one fast LLM call.** When a question is not in Bulgarian or is a follow-up,
  a single call produces a self-contained **Bulgarian search query**: it rewrites follow-ups and
  translates, keeping eponyms, drug names and ICD codes as written. The call runs with thinking
  disabled (`think: false`), a small output cap (`num_predict`) and temperature 0.
- **Retrieval and reranking use the Bulgarian search query**; the original question still anchors
  dense/BM25 retrieval. The reranker scores each candidate against the query in *that candidate's*
  language, so a future English book is scored against the original question.
- **The Latin-term guard checks the search query's Latin terms** — the eponyms and drug names that
  survive translation — instead of every English word.
- **The answer stays in the question's language**; the existing language line is unchanged.
- **Answer prompts are budgeted** against the context window with a reserve for the answer: history
  is trimmed first, then the lowest-ranked excerpts, so the prompt never exceeds the window.
- **All chat calls disable thinking** via `think: false`, so no call depends on `/no_think`.

## Capabilities

### New Capabilities

- `query-preparation`: fast rewrite/translation of the user's question into a Bulgarian search
  query, and language-aware reranking and Latin-term checks built on it.
- `answer-context-budget`: token budgeting of the answer prompt (excerpts + history) against the
  model's context window with an answer reserve.

### Modified Capabilities

(none — `iterative-rag-retrieval` gates and thresholds are unchanged; only their input query changes)

## Impact

- **Code:** `RagPluginBase` (`MaybeRewriteQueryAsync` → query preparation, reranking by language,
  Latin-term guard input), `AnswerPromptBuilder` (budgeting, execution settings), `PromptConstants`
  (answer reserve, rewrite output cap).
- **Latency:** +1 short LLM call (~1–2 s) for English questions; −~20 s for follow-ups.
- **Tests:** unit tests for the budget and the prepared-query plumbing; golden run 3 adds English
  questions back, compared against runs 1–2.
- No API, schema, or dependency changes.
