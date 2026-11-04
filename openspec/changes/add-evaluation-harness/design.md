# Design

## Pipeline hook: `RagRunScope`

The overrides and the retrieved chunks have to cross `QueryService`, Semantic Kernel and the plugins. An
`AsyncLocal` scope does this without changing any signature or the public `QueryResult`, so retrieved text
cannot leak through the API. Live queries have no scope and use the production values.

Rejected: adding the chunks to `QueryResult` (they would be serialized to API clients), and a second
evaluation-only pipeline (it would measure something other than what doctors use).

`PromptExecutionSettings.ModelId` is honoured by the Ollama connector. `RagRunScopeTests` proves this against
the real connector, because a silently ignored override would make every model comparison fake.

## Measuring

| Group | Metrics | Source |
| --- | --- | --- |
| Correctness | Pass rate, fact recall, unsafe claims, answer rate, abstention accuracy | Reference + judge |
| Grounding | Faithfulness, citation accuracy | Judge |
| Retrieval | Hit@1, Hit@5, MRR, nDCG@5, excerpt precision | Relevance terms, no model |
| Retrieval | Fact coverage | Judge |
| Format | Markers, range, language, leakage | Rules, ported from `golden_run.py` |
| Reliability and cost | Errors, overflows, dropped excerpts, latency, prompt tokens | Trace |

Each reference fact is checked twice, against the excerpts and against the answer. That separates the two
failures context engineering must tell apart:

- **Lost in retrieval:** never reached the model. Fix retrieval.
- **Lost in generation:** retrieved, then dropped. Fix the prompt or the context shape.
- **Not from excerpts:** in the answer but not the excerpts, so it came from model memory rather than the books.

### Relevance is an approximation

A chunk is relevant when it contains the case's stems. This is cheap, deterministic and needs no labelled
chunk ids, which would go stale on every re-index. Its limits: it measures topic, not usefulness, and its
recall is bounded by what was ranked. Fact coverage is the stronger signal.

### Judge

The judge answers narrow yes/no questions about supplied text, which a small local model does far more
reliably than an overall quality score. It runs at temperature 0 with JSON output, and evaluated text is fenced
as data. A reply that stays malformed after one retry leaves the question unjudged. An unjudged question that failed
no other check is *inconclusive*: outside the pass rate, neither a pass nor a failure. Counting it as a pass
would let a dead judge produce a high score; counting it as a failure would make a flaky judge look like a
regression.

Defaulting the judge to the answer model means it grades its own work, and an 8B judge is itself noisy. The
judge is selectable per run and recorded, and comparisons should keep it fixed.

## Running

Every answer is generated before any is judged, so latency is free of judge interference and Ollama swaps
models once per run. Each question gets its own throwaway conversation, except follow-ups, which join their
parent's. The old script shared history across a query type, so scores are not comparable with its reports.

Web search is off during a run: SearXNG results change over time and would add noise. The temperature field
defaults to 0 so a re-run differs only by what changed; production uses the model's default temperature, so
absolute scores can differ slightly from live behaviour.

One run at a time, enforced by `EvaluationCoordinator`, because a second run would spoil latency numbers. A run
is not isolated from live traffic: a run on a different model makes Ollama swap models, which slows concurrent
doctor queries. Run evaluations when the system is quiet.

## Comparing

A snapshot records model, context settings, thresholds, a hash of the composed prompts, corpus size, and the
golden-set hash, so a comparison shows what actually differed. The baseline is the pinned run, otherwise the
previous completed run.

With 20–30 questions one flip moves a rate by 3–6 points, so rate metrics tolerate 6 points and judged ones 8.
Unsafe claims, pipeline errors and context overflows tolerate nothing. Latency and token cost are reported but
never fail a run, because a change may legitimately trade them for quality.

The verdict is **Regressed** when any gating metric got worse, whatever else is missing. Otherwise it is
**Inconclusive** when the baseline measured a gating metric this run did not (for example the judge was off), or
when more than 10% of questions went unjudged. Absence of evidence never reads as No regression.

## Golden set

`MedAssist.AI/Evaluation/golden-set.json` keeps the 18 original questions and adds two unanswerable ones with
invented syndrome names, so abstention is measured. The reference facts were drafted from general paediatric
knowledge, without access to the encrypted books, and every case has `reviewed: false`. The UI states this until
a physician has checked them.

G5 and E5 nearly duplicate the few-shot example in `PromptProfiles.DifferentialDiagnosis`, which contains the
answer, so their scores are inflated. They stay for continuity; removing that overlap is a context-engineering
decision.
