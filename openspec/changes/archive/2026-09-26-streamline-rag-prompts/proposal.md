## Why

Every RAG answer ships a prompt that says the same things several times: the citation instruction
three times plus the worked example, the answer-language rule twice, and "prose only, no lists / no
markdown" four or five ways. The persona and shared rules are hand-copied into five prompts that have
already drifted apart, and the last ten chat messages are re-sent in full — including prior answers
whose `[n]` markers point at excerpts that are no longer in the prompt, so they collide with the
current `[1]…[n]` numbering. Repetition costs prompt tokens (and latency on the local `qwen3:8b`)
on every query, and dilutes the instructions that matter.

The same fix already proved out in SmartTVApp's Claude Code hooks: say each thing **once**, in the
place it is most effective, and write only the **delta** over what is already present — then
**measure** the saving rather than assume it.

## What Changes

- Introduce a single prompt-composition point: one shared **core** (persona, prose style, citation
  rule, insufficient-evidence rule) plus a small per-plugin **delta** (e.g. DDx's "most likely
  first", Global Search's breadth guidance). Plugins stop carrying full hand-copied prompts.
- State each instruction exactly once:
  - **Citation** — once, as the end-of-user-message reminder (the placement `qwen3:8b` actually obeys),
    plus the few-shot example. Remove the system-prompt paragraph and RULES bullet that restate it.
  - **Answer language** — once, via the existing script-detected per-query line. Remove the
    "respond in the user's language" RULES bullet.
  - **Prose / no markdown** — once, as a single rule; the example demonstrates it and
    `MarkdownStripper` remains the post-hoc safety net.
- Slim the conversation history sent to the answer model: strip `[n]` citation markers from prior
  assistant turns and cap history by size, not only by message count.
- Drop the "Sources searched" book list from the system prompt (each excerpt already carries its book
  title).
- Web enrichment / web-only prompts reuse the same shared core rather than a third copy of it.
- Record prompt-token usage per query as a metric, and add a small EN + BG golden-question check run
  before and after, gating adoption on citation-marker rate and answer-language correctness not
  regressing.

## Capabilities

### New Capabilities

- `rag-prompt-composition`: how answer prompts are assembled — shared core + per-plugin delta, each
  instruction stated once, history sanitisation/budget, and prompt-token observability.

### Modified Capabilities

- `answer-citation-markers`: the "Model-emitted citation markers" requirement changes from "the
  system prompts SHALL instruct…" to "the answer prompt SHALL instruct… exactly once", and history
  sent to the model SHALL NOT carry stale markers from prior answers.

## Impact

- **Code:** `MedAssist.AI/Plugins/RagPluginBase.cs` (prompt assembly, history), the per-plugin
  `GetSystemPrompt` overrides (`GlobalSearchPlugin`, `DifferentialDiagnosisPlugin`), and
  `MedAssist.Web/Services/QueryService.cs` (web enrichment / web-only prompts). Prompt text constants
  move under `MedAssist.Shared/Constants` or a dedicated prompt builder in `MedAssist.AI`.
- **Behaviour:** answer wording may shift; retrieval, source ordering, citation numbering and the
  `[n]` rendering contract are unchanged.
- **Observability:** one new metric (prompt tokens per query, by plugin type).
- **Tests:** new unit tests over the pure prompt builder (each instruction present exactly once,
  markers stripped from history, budget respected).
- No API, schema, or dependency changes.
