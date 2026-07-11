# Roadmap

Deferred work and known follow-ups that aren't yet a formal OpenSpec change. Implemented work is
archived under `openspec/changes/archive/`; this file tracks tuning and ideas that are quality
levers rather than defects.

## Follow-ups

### Citation-marker reliability — from `cited-answer-markers`

Inline `[n]` citation markers are implemented and render correctly (superscript markers tied to the
numbered source list, range-guarded, graceful degradation when absent). **Emission is model-bound**,
not a code gap: the local `qwen3:8b` produced no markers from the system-prompt instruction alone
and only complied once a citation reminder was repeated at the *end* of the user message — and even
then not on every query. Because rendering degrades gracefully (no markers → clean prose), this is a
consistency lever, not a bug. Options to make markers more reliable:

- **Larger / more instruction-tuned local model** in the shared Ollama (the simplest lever).
- **Stricter prompt** — e.g. a per-query-type one-shot example, or a firmer "cite every factual
  paragraph" directive, accepting some rigidity/latency.
- **Post-generation citation pass** — a second, cheap LLM call that inserts `[n]` into the finished
  prose, if determinism matters more than cost/latency.
