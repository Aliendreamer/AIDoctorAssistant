## ADDED Requirements

### Requirement: Admin-run evaluation

The system SHALL let an Admin start an evaluation from `/admin/evaluation`, and SHALL deny the page to other
roles. A run SHALL execute on the host, so it continues if the admin leaves the page, and it SHALL send the
golden questions through the same query service doctors use. Only one run SHALL be active at a time.

#### Scenario: Run outlives the page

- **WHEN** an admin starts a run and closes the page
- **THEN** the run continues and its results are available when the admin returns

#### Scenario: Second run is refused

- **WHEN** a run is active and an admin starts another
- **THEN** the second run is refused with a message saying one is already running

#### Scenario: Run is cancelled

- **WHEN** an admin cancels a run
- **THEN** the run stops at the next safe point, is marked Cancelled, and keeps its partial answers

#### Scenario: Restart interrupts a run

- **WHEN** the application restarts during a run
- **THEN** the run is marked Failed on startup instead of appearing active forever

### Requirement: Per-run model and context settings

An admin SHALL be able to override, for one run, the answer model, judge model, context window, answer reserve,
excerpt cap, retrieval top-K, history budget and temperature. Empty fields SHALL use production values. Overrides
SHALL apply only within the run, and SHALL NOT affect concurrent live queries. A model Ollama does not have SHALL
be rejected before the run starts.

#### Scenario: Override reaches the model

- **WHEN** a run sets a different model and context window
- **THEN** the requests sent to Ollama for that run carry that model and `num_ctx`

#### Scenario: Live query during a run

- **WHEN** a doctor asks a question while a run with overrides is active
- **THEN** that query uses the production settings

### Requirement: Scored quality dimensions

Each run SHALL score correctness against reference facts and forbidden claims, grounding of answer sentences and
citations in the excerpts, retrieval ranking and fact coverage, answer format, and reliability and cost. Each
reference fact SHALL be checked separately against the excerpts and against the answer, so a lost fact can be
attributed to retrieval or to generation. Web search SHALL be off during a run.

#### Scenario: Fact retrieved but not used

- **WHEN** a reference fact is in the excerpts but not the answer
- **THEN** it counts as lost in generation

#### Scenario: Unanswerable question

- **WHEN** a question names a condition the books cannot cover and the system generates an answer anyway
- **THEN** the question fails unless the answer states the information is insufficient

#### Scenario: Hidden pipeline failure

- **WHEN** the query service turns an exception into a normal-looking answer
- **THEN** the question counts as a pipeline error and fails

### Requirement: Judge failures are not scores

A question the judge could not score SHALL be left unjudged. It SHALL be excluded from judged metrics and reported
in a separate count. If it failed no other check it SHALL be inconclusive, counting as neither a pass nor a failure.
A run SHALL NOT report No regression when the evidence for that is missing.

#### Scenario: Malformed judge reply

- **WHEN** the judge's reply is still malformed after one retry
- **THEN** the question is inconclusive, outside the pass rate, and the judge-failure count increases

#### Scenario: Judge unavailable for a run

- **WHEN** more than 10% of questions could not be judged and no gating metric got worse
- **THEN** the verdict is Inconclusive

#### Scenario: Judge turned off

- **WHEN** a run without the judge is compared with a judged baseline and no gating metric got worse
- **THEN** the verdict is Inconclusive and the judged metrics show as not measured

### Requirement: Recorded runs and comparison

Each run SHALL store its effective configuration, including a fingerprint of the composed prompts and a hash of
the golden set, and the full answer, excerpts and scores of each question. A completed run SHALL be compared with
a baseline: the pinned run, otherwise the previous completed run. The comparison SHALL show changed settings,
metric movement and questions that flipped, and SHALL give a verdict.

#### Scenario: Regression

- **WHEN** a gating metric drops by more than its tolerance
- **THEN** the verdict is Regressed

#### Scenario: Noise

- **WHEN** one question flips and no other gating metric moves
- **THEN** the verdict is No regression

#### Scenario: New unsafe claim

- **WHEN** a run makes one more forbidden claim than its baseline
- **THEN** the verdict is Regressed

#### Scenario: Cost change only

- **WHEN** latency rises but no quality metric drops
- **THEN** the increase is shown and the verdict is not Regressed

#### Scenario: Different references

- **WHEN** the two runs used different golden sets
- **THEN** the comparison warns that scores may have moved because of the references

### Requirement: Reference review status

Each golden case SHALL record whether a physician has reviewed it, and the evaluation page SHALL state how many
references are unreviewed.

#### Scenario: Unreviewed references

- **WHEN** any golden case is unreviewed
- **THEN** the evaluation page warns that correctness scores depend on unreviewed references
