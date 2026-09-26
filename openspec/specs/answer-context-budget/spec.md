# answer-context-budget Specification

## Purpose
TBD - created by archiving change tune-query-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Answer prompt fits the context window

The answer prompt SHALL be kept within the context window minus a reserve for the answer
(`ContextWindowTokens − AnswerReserveTokens`), using a conservative token estimate. When over
budget, the builder SHALL drop the oldest history exchanges first, then the lowest-ranked excerpts,
and SHALL always keep at least one excerpt.

#### Scenario: Prompt within budget is unchanged

- **WHEN** the estimated prompt size is below the budget
- **THEN** all history and all excerpts are sent

#### Scenario: History is trimmed before excerpts

- **WHEN** the estimated prompt size exceeds the budget and history is present
- **THEN** oldest history exchanges are dropped before any excerpt is dropped

#### Scenario: Lowest-ranked excerpts dropped last

- **WHEN** the prompt is still over budget with no history left
- **THEN** the lowest-ranked excerpts are dropped until it fits, keeping at least one

### Requirement: Citations follow the kept excerpts

When excerpts are dropped for budget, the answer's `sources` SHALL contain only the kept excerpts,
numbered contiguously, so that marker `[n]` still maps to `sources[n-1]`.

#### Scenario: Dropped excerpt is not cited

- **WHEN** 5 excerpts are retrieved and the budget keeps 4
- **THEN** the prompt numbers excerpts 1–4 and `sources` has exactly those 4 entries

