## 1. Pipeline hook (TDD)

- [x] 1.1 Add `RagRunOverrides`, `RagTrace` and the ambient `RagRunScope`
- [x] 1.2 Honour overrides in the answer and query-preparation settings, budget, excerpt cap and top-K
- [x] 1.3 Record ranking, excerpts, outcome and tokens; expose swallowed errors; skip web when book-only
- [x] 1.4 Prove model, context and JSON format reach the wire through the real Ollama connector

## 2. Scoring (TDD)

- [x] 2.1 Golden set with reference facts, forbidden claims and relevance terms, plus a validating loader
- [x] 2.2 Format, retrieval and pass/fail scoring
- [x] 2.3 Local judge with retry, and per-question judging that treats judge failure as unjudged
- [x] 2.4 Run summary and baseline comparison

## 3. Storage

- [x] 3.1 Entities, configuration, repository and the `AddEvaluationRuns` migration
- [x] 3.2 Repository tests on real SQLite

## 4. Running

- [x] 4.1 Coordinator, worker, runner, snapshot factory and model catalog
- [x] 4.2 Reconcile interrupted runs on startup; remove throwaway chat history
- [x] 4.3 Runner tests: isolation, cancellation, failures, phase order

## 5. Admin UI

- [x] 5.1 Run form, progress, run list and navigation
- [x] 5.2 Run detail: verdict, setting changes, flipped questions, metrics, per-question drill-down
- [x] 5.3 Render tests for both pages

## 6. Finish

- [x] 6.1 Docs and project memory
- [x] 6.2 Delete `scripts/golden_run.py`
- [x] 6.3 Start the stack, let the migration apply, and complete a first real run
- [x] 6.4 Pin the first good run as the baseline
- [ ] 6.5 Physician review of `golden-set.json`, setting `reviewed: true` per case — **deferred**: a physician has to do this; it is not code work (archived 2026-11-11)
