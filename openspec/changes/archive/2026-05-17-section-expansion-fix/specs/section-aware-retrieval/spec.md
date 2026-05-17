## MODIFIED Requirements

### Requirement: Section expansion triggered only by summary chunk hits

Section scroll expansion SHALL only be triggered by candidates where `IsSummary == true`. Regular non-summary candidate chunks SHALL NOT cause section expansion, regardless of which section they belong to.

#### Scenario: Summary chunk found — expansion triggers
- **WHEN** the initial vector search returns at least one chunk with `IsSummary = true` for section X
- **THEN** `ScrollSectionAsync` is called for section X to fetch all regular chunks
- **AND** those chunks are merged into the candidate pool before reranking

#### Scenario: No summary chunks found — no expansion
- **WHEN** the initial vector search returns only non-summary chunks
- **THEN** `ExpandBySectionAsync` returns the original candidate list unchanged
- **AND** `ScrollSectionAsync` is NOT called

#### Scenario: Wrong-section regular chunks do not trigger expansion
- **WHEN** the initial search returns 5 non-summary chunks from section Y (an irrelevant section)
- **THEN** section Y is NOT scrolled
- **AND** the candidate pool is not flooded with chunks from section Y

#### Scenario: Summary chunks excluded from final answer (unchanged)
- **WHEN** reranking completes and top-5 are selected
- **THEN** any chunks with `IsSummary = true` are filtered out before building the answer
- **AND** the final answer contains only regular content chunks
