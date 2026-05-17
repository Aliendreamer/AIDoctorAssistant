## MODIFIED Requirements

### Requirement: Rank and deduplicate retrieved chunks
After collecting chunks from all expanded query terms, the system SHALL deduplicate by `(BookId, ChunkIndex)`, rerank the candidates using `ICrossEncoderReranker`, and return the top 5 by reranker score. The previous blind `Take(10)` truncation is replaced by this scored selection.

#### Scenario: Enough candidates after dedup
- **WHEN** deduplicated chunks number more than 5
- **THEN** reranker scores all candidates and the top 5 by score are used as LLM context

#### Scenario: Fewer than 5 candidates after dedup
- **WHEN** deduplicated chunks number 5 or fewer
- **THEN** all candidates are passed through reranking and returned in score order

#### Scenario: No candidates retrieved
- **WHEN** all vector store searches return empty results
- **THEN** the pipeline returns "No relevant information found" without invoking the reranker
