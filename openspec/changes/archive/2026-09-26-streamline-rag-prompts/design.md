## Context

Answer prompts are assembled in three places today:

- `RagPluginBase.BuildResultAsync` — system prompt (`GetSystemPrompt()` + "Sources searched" book
  list), full conversation history, then a user message carrying the language line, the question,
  the numbered excerpts, a citation reminder and `/no_think`.
- `GetSystemPrompt` overrides in `GlobalSearchPlugin` and `DifferentialDiagnosisPlugin` — each a full
  standalone copy of persona, prose rules and citation rules. `Disease`, `Symptoms` and `Treatment`
  inherit the base prompt.
- `QueryService.EnrichWithWebAsync` / `AnswerFromWebAsync` — two more standalone system prompts.

History is the last 10 persisted messages for the user and query type (`QueryService.cs:82`),
re-sent verbatim, including prior answers with their `[n]` markers.

The `cited-answer-markers` tuning established one hard fact: `qwen3:8b` emitted markers only once the
instruction was repeated at the **end** of the user message. That reminder is the one placement that
must survive.

The pattern being borrowed (SmartTVApp `.claude/claude-hooks/README.md`): say each thing once, write
only the delta over what is already present, measure before and after.

## Goals / Non-Goals

**Goals:**

- One composition point; each instruction appears exactly once in the final prompt.
- Per-plugin prompts are deltas over a shared core, not copies.
- History carries no stale citation markers and is bounded by size.
- Prompt-token cost is measured, and quality is gated on a before/after EN + BG check.

**Non-Goals:**

- Changing the model, retrieval, reranking, chunk count, or the `[n]` numbering/rendering contract.
- A prompt-templating engine or externalised prompt files (Handlebars/Liquid, YAML). Plain C# is
  enough at this size.
- Guaranteeing marker emission — that remains model-bound (see `ROADMAP.md`).

## Decisions

### 1. A pure `AnswerPromptBuilder` in `MedAssist.AI`

A static/pure builder takes `(PromptProfile profile, string query, IReadOnlyList<MedicalChunk>
chunks, IReadOnlyList<ChatMessageDto> history)` and returns the `ChatHistory` (or an intermediate
record of system text, history turns and final user text). Plugins supply a `PromptProfile` — the
delta — instead of overriding a full `GetSystemPrompt()`.

*Why:* makes "each instruction once" unit-testable without Ollama, and gives one place to reason
about ordering. *Alternative:* keep `GetSystemPrompt()` overrides and just trim them — rejected, the
five copies would drift again.

### 2. Core + delta layout

- **Core system text** (shared): persona; prose-only rule stated once (no lists, headings, bold or
  italics); "if excerpts are insufficient, say so in one sentence and stop"; "mention the source book
  or section naturally when relevant".
- **Profile delta**: task framing (e.g. DDx: "reason through the differential, most likely first";
  Global: "cover aetiology, presentation, pathophysiology, diagnosis and management as the sources
  allow") and the few-shot example for that profile.
- **Final user message** (per query): question → numbered excerpts → language line → citation
  instruction → `/no_think`. The instructions go last because Ollama drops the *start* of an
  over-long prompt (see decision 6).

The citation instruction lives only in the final user message; the example still shows `[n]` usage.
The language instruction lives only in the final user message (script-detected). Both are the
placements closest to generation, which weaker local models weight most.

*Alternative:* citation only in the system prompt — rejected; that is exactly the configuration that
produced no markers during `cited-answer-markers` tuning.

### 3. History sanitisation and budget

- Strip `[n]` / `[n][m]` markers from prior **assistant** turns before sending (reuse the marker
  pattern from `CitationMarkers`).
- Keep the existing 10-message window but add a character budget (constant in
  `MedAssist.Shared/Constants`, e.g. ~6k chars), dropping oldest turns first and always keeping
  whole user/assistant pairs.
- `MaybeRewriteQueryAsync` is unchanged — it already makes follow-ups self-contained for retrieval.

*Alternative:* send only prior user questions — cheaper, but loses the model's own prior framing
needed for "and what about in children?"-style follow-ups. Revisit if the budget alone is not enough.

### 4. Web prompts reuse the core

`EnrichWithWebAsync` and `AnswerFromWebAsync` compose from the same core plus their own delta
(untrusted `<web_source>` guard, "preserve book `[n]` markers" / "no markers for web-only"). The
untrusted-content guard stays verbatim — it is a security control, not a style repeat.

### 5. Measurement

- Metric `rag_prompt_tokens` (histogram, tags `plugin_type`, `estimated`) on the `MedAssist.AI` meter
  (already exported via OTel), recorded where generation happens — the plugins and the two web calls.
  Populated from the model-reported input tokens when present, else a chars/4 estimate.
- A manual golden set: ~6 EN + ~6 BG questions across the query types, run against the current
  stack and again after the change. Record per answer: prompt tokens, marker present (Y/N), markers
  in range, answer language correct, list/markdown leakage. Results go in the change folder.

### 6. Request an 8k context window (added after golden run 1)

Ollama served every call with its 4,096-token default. Golden run 1 showed answer prompts clipped at
4,095 tokens (losing the system prompt and the language line → English answers) and mid-answer
context shifts discarding half the context. Every chat call now passes one shared
`PromptExecutionSettings` with `num_ctx = PromptConstants.ContextWindowTokens` (8,192), which
OllamaSharp maps onto the request options — app-side, without changing PCC's shared Ollama.

*Alternative:* `OLLAMA_CONTEXT_LENGTH` on the shared Ollama — rejected for now, it changes behaviour
for every PCC consumer. *Trade-off:* a model loaded at another `num_ctx` by a different client is
reloaded on first use.

## Risks / Trade-offs

- [Removing system-prompt citation text lowers marker rate] → Golden-set gate; if the rate drops,
  restore a one-line citation mention in the core and re-measure. Numbers decide, not intuition.
- [Removing the language RULES bullet lets BG answers drift into English] → The script-detected line
  is kept and is stronger; golden set includes BG follow-ups to catch drift.
- [Mixed-script questions (BG with Latin eponyms) mis-detected] → Unchanged behaviour from today;
  out of scope, noted.
- [Stripping markers from history confuses the model about earlier claims] → Low risk; prior
  sources are not in the prompt anyway, so the markers were already meaningless to it.
- [Prompt-eval count unavailable through the SK Ollama connector] → Fall back to the estimate; the
  golden-set comparison stays valid because both runs use the same method.

## Open Questions

- ~~Does the SK Ollama connector expose the prompt-eval count?~~ Resolved: the 1.76 connector runs
  through `IChatClient` / `AsChatCompletionService`, which stores Microsoft.Extensions.AI
  `UsageDetails` in `Metadata["Usage"]`; OllamaSharp maps `prompt_eval_count` to `InputTokenCount`.
  Confirm on the first live run (the `estimated` tag should be `false`).
- Exact history character budget — pick after seeing real per-turn sizes in the golden run.
