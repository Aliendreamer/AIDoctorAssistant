## ADDED Requirements

### Requirement: Confidence-gated iterative retrieval

The RAG pipeline SHALL run up to `MaxIterations` additional search passes after the initial retrieval when the highest cross-encoder logit across all top candidates falls below `ConfidenceThreshold`.

Each pass SHALL widen the search according to a fixed strategy table (increasing topK, relaxing the language filter, reducing query terms to the longest keyword). The pipeline SHALL merge new candidates with existing ones, deduplicate by `BookId:ChunkIndex`, and re-rank the full pool before evaluating the threshold again.

The implementation SHALL hard-cap iterations at 5 regardless of the configured `MaxIterations` value.

#### Scenario: High confidence — no additional iterations

- **WHEN** the cross-encoder scores the top candidate above `ConfidenceThreshold` after the initial search
- **THEN** the pipeline returns results without running any fallback pass

#### Scenario: Low confidence — runs fallback passes

- **WHEN** the top candidate score is below `ConfidenceThreshold`
- **THEN** the pipeline runs up to `MaxIterations` additional passes with progressively wider search parameters

#### Scenario: MaxIterations cap enforced

- **WHEN** `MaxIterations` is configured to a value greater than 5
- **THEN** the pipeline runs at most 5 fallback passes

#### Scenario: Empty initial results

- **WHEN** the vector store returns no chunks on the initial search
- **THEN** the pipeline returns "No relevant information found" without entering the loop

### Requirement: Configurable retrieval options

The system SHALL read `Rag:ConfidenceThreshold` (float) and `Rag:MaxIterations` (int) from application configuration with defaults of `0.0` and `2` respectively.

The cap of 5 on `MaxIterations` SHALL be enforced at dependency injection binding time, not inside the loop.

#### Scenario: Default configuration produces safe behaviour

- **WHEN** no `Rag` section is present in configuration
- **THEN** the pipeline uses `ConfidenceThreshold = 0.0` and `MaxIterations = 2`

#### Scenario: MaxIterations override respected within cap

- **WHEN** `Rag:MaxIterations` is set to 4
- **THEN** the pipeline runs at most 4 fallback passes (not 5)

### Requirement: Scored reranker output

The cross-encoder reranker SHALL return `IReadOnlyList<ScoredChunk>` where each `ScoredChunk` pairs a `MedicalChunk` with its raw logit score.

Results SHALL be ordered by descending score.

#### Scenario: Scores accompany chunks

- **WHEN** the reranker processes a non-empty candidate list
- **THEN** every returned `ScoredChunk` carries the raw logit for that query-chunk pair

#### Scenario: Empty input produces empty output

- **WHEN** the candidate list is empty
- **THEN** the reranker returns an empty list without invoking the ONNX model
