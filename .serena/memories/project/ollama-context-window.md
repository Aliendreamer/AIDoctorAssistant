# Ollama context window and prompt truncation

**Type:** project (non-obvious runtime behaviour)

Ollama serves requests at a **4,096-token** context by default unless the request sets `num_ctx`.
RAG answer prompts (5 excerpts + history) run **2.7k–7.2k tokens**. At 4k they were clipped, and
Ollama drops the **start** of an over-long prompt (system prompt, early instructions). A context
shift then discarded half the context mid-answer (`truncated = 1`). Symptoms: Bulgarian questions
answered in English, missing `[n]` markers, 504 timeouts.

**Fix (change `streamline-rag-prompts`, 2026-09-26):** every chat call passes
`AnswerPromptBuilder.ExecutionSettings` (`num_ctx = PromptConstants.ContextWindowTokens`, 8,192).
The language and citation lines sit at the **end** of the final user message, so they survive
front truncation. The golden set went from 2/5 to 12/12 correct language.

**How to check:** `docker logs personalcommandcenter-ollama-1 | grep -E "n_ctx_slot|truncated ="`.
The log shows each prompt's `task.n_tokens`, which is the easiest way to measure prompt size.

**Follow-up (change `tune-query-pipeline`):** answer prompts are budgeted at
`ContextWindowTokens − AnswerReserveTokens` (6,144) using **2 chars/token**. Measured with qwen3:
Bulgarian is 2.1–2.3 chars/token, English ~4.6; `/ 3` under-counted Cyrillic by ~40%. All chat calls
send `think: false` (qwen3 ignored `/no_think` and reasoned silently); the query-prep call is capped
at 96 tokens. Result: no truncation, largest prompt 5.2k, mean latency 37.8 s → 17.6 s.

**Host port:** the app is published on **8081**, not 8080 — on this machine the MiniTool
ShadowMaker agent service (`MTAgentService`) holds host port 8080.

Related: `mem:suggested_commands`.
