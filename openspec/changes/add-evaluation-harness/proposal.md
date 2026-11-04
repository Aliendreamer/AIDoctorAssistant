# Add an evaluation harness

## Why

`scripts/golden_run.py` proved useful for prompt tuning, but it only checks the form of an answer: citation
markers, language, leakage and latency. An 18/18 run says nothing about whether an answer is medically correct
or whether retrieval found the right passages. It also runs outside the app, with a hardcoded admin login.

The next planned work (context engineering, plus the model and embedder trials in `ROADMAP.md`) needs a
measurement that says whether a change made the system better or worse.

## What changes

- A new admin page at `/admin/evaluation` starts a run, shows progress, and lists past runs.
- Runs execute on the host in a background worker, through the same `QueryService` doctors use.
- Each run can override the model and context settings without a redeploy. Live queries are unaffected.
- Answers are scored for correctness, grounding, retrieval quality, format, reliability and cost.
- Every run stores its effective configuration and full results, and is compared with a baseline.
- `scripts/golden_run.py` is replaced.

## Non-goals

- Editing golden questions in the UI: they stay in a reviewed, versioned file.
- Automatic runs on deploy or in CI.
- A cloud judge: all inference stays local, in keeping with the deployment model.
- Changing any prompt or retrieval behaviour. This change measures; context engineering comes next.

## Impact

- New tables `evaluation_runs` and `evaluation_results` (migration `AddEvaluationRuns`).
- `RagPluginBase`, `AnswerPromptBuilder`, `QueryPreparation` and `QueryService` read an optional ambient
  scope. With no scope active their behaviour is unchanged.
