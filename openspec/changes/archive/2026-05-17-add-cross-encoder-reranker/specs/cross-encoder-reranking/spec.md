## ADDED Requirements

### Requirement: Load cross-encoder model from disk via ONNX
The system SHALL load a cross-encoder ONNX model from the path configured under `Models:RerankerPath` at startup via `ModelInitializer`. If the model files are absent they SHALL be downloaded from HuggingFace before the first query.

#### Scenario: Model files present at configured path
- **WHEN** the application starts and model files exist under `Models:RerankerPath`
- **THEN** `CrossEncoderReranker` loads the ONNX session without downloading

#### Scenario: Model files absent at startup
- **WHEN** the application starts and the reranker model directory is empty or missing
- **THEN** `ModelInitializer` downloads all required model files from HuggingFace before returning

### Requirement: Score candidate chunks against the query
The system SHALL accept a query string and a list of `MedicalChunk` candidates and return them sorted descending by cross-encoder relevance score.

#### Scenario: Reranking a non-empty candidate list
- **WHEN** `RerankAsync(query, candidates)` is called with N > 0 candidates
- **THEN** every candidate is scored and the list is returned sorted by score descending

#### Scenario: Reranking an empty candidate list
- **WHEN** `RerankAsync(query, candidates)` is called with an empty list
- **THEN** an empty list is returned immediately without invoking the ONNX model

### Requirement: Tokenize input for the cross-encoder
The system SHALL tokenize the concatenated (query, chunk.Text) pair using the model's tokenizer before running inference, truncating to the model's `max_seq_length`.

#### Scenario: Input within token limit
- **WHEN** the combined token count of query + chunk text is within `max_seq_length`
- **THEN** the full text is encoded without truncation

#### Scenario: Input exceeds token limit
- **WHEN** the combined token count exceeds `max_seq_length`
- **THEN** the chunk text is truncated so the total stays within the limit
