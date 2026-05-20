## ADDED Requirements

### Requirement: Initial gather uses full query only

The initial `GatherCandidatesAsync` call in `ExecuteSearchAsync` SHALL use only the full original query as its search term. Expanded keywords and synonym fragments from `ExpandQueryAsync` SHALL NOT be passed to the first-pass gather.

#### Scenario: Compound anatomical query retrieves correct domain

- **WHEN** the query is "малкомозъчните тонзили" (cerebellar tonsils)
- **THEN** the initial candidate pool contains chunks about cerebellar anatomy, not ENT/tonsil content

#### Scenario: Full query is always the first search term

- **WHEN** `ExecuteSearchAsync` is called with any query string
- **THEN** `GatherCandidatesAsync` is first called with `[query]` as the terms list, not `expandedTerms`

### Requirement: Expanded keywords used in retry fallback only

Per-keyword expanded terms from `ExpandQueryAsync` SHALL be used in the iterative retry loop (iterations 0 through maxIter) but NOT in the initial gather pass.

#### Scenario: Retry iterations use expanded terms

- **WHEN** initial candidates score below `ConfidenceThreshold`
- **THEN** retry iteration 0 calls `GatherCandidatesAsync` with the full `expandedTerms` list (including per-keyword fragments)

#### Scenario: Single-word query is unaffected

- **WHEN** the query is a single word (e.g., "тонзилит")
- **THEN** behaviour is identical to before — only one term exists and it is used in both the initial gather and retries
