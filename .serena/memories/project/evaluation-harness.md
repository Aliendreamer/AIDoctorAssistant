# Evaluation harness

**Type:** project (how quality is measured)

Change `add-evaluation-harness` (2026-11-04) moved evaluation into the app at `/admin/evaluation`. Run it
after any prompt, model, retrieval or context change and compare with the baseline. It replaces
`scripts/golden_run.py`.

## Non-obvious facts

- **Per-run overrides travel through `RagRunScope`** (an `AsyncLocal`). Live queries have no scope, so new
  context knobs belong on `RagRunOverrides` and must read from the scope, not from `PromptConstants`.
- **Lost facts are attributed.** Each reference fact is checked against the excerpts and the answer: lost in
  retrieval, lost in generation, or not from excerpts (model memory). This is the main signal for context
  engineering.
- **Reference facts are unreviewed drafts.** They were written without access to the encrypted books. Treat
  correctness scores as provisional until a physician sets `reviewed: true` in
  `MedAssist.AI/Evaluation/golden-set.json`.
- **G5 and E5 are contaminated.** They nearly duplicate the few-shot example in
  `PromptProfiles.DifferentialDiagnosis`, which contains the answer, so their scores run high.
- **Scores are not comparable with the old reports.** Each question now gets its own conversation; the script
  shared history per query type.
- **Keep the judge model fixed across runs you compare**, or the measuring instrument changes too.
- **Runs are not isolated from live traffic.** A run on another model makes Ollama swap models.

## Adding a migration

`dotnet ef migrations add` with `--no-build` reads the stale DLL and generates an **empty** migration. Always
let it build.

Related: `mem:project/ollama-context-window`.
