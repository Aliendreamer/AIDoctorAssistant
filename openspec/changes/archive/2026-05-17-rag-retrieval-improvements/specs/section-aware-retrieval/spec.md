## ADDED Requirements

### Requirement: Section expansion after each retrieval round

After each Qdrant search round in the RAG pipeline, the system SHALL collect all unique `(chapter_title, section_title)` pairs from the returned non-summary candidates. For each unique pair it SHALL scroll Qdrant (by payload filter, no vector) to retrieve all non-summary chunks belonging to that section, up to a maximum of 50 chunks per section.

The expanded chunks SHALL be merged into the existing candidate pool and deduplicated by `{BookId}:{ChunkIndex}` before reranking.

#### Scenario: Section siblings are pulled when any chunk hits

- **WHEN** Qdrant returns a chunk from section S
- **THEN** all other non-summary chunks in section S are fetched and added to the candidate pool

#### Scenario: Deduplication prevents double-counting

- **WHEN** a chunk from section S is returned both by vector search and by section scroll
- **THEN** it appears exactly once in the candidate pool passed to the reranker

#### Scenario: Section expansion does not apply to summary chunks

- **WHEN** a summary chunk is returned by vector search
- **THEN** its `(chapter_title, section_title)` is NOT used to trigger a section scroll (regular chunks from the same section may already be in the pool from other hits)

#### Scenario: Empty section title does not trigger expansion

- **WHEN** a candidate chunk has an empty or null `section_title`
- **THEN** no section scroll is performed for that chunk

### Requirement: Summary chunks used as retrieval triggers

Summary chunks returned by vector search SHALL trigger section expansion even though they are excluded from the final answer. A summary chunk hit on section S SHALL cause all non-summary chunks of section S to be added to the candidate pool.

#### Scenario: Summary hit expands its section

- **WHEN** a summary chunk for section S is among the Qdrant search results
- **THEN** all non-summary chunks of section S are fetched and merged into the candidate pool
- **AND** the summary chunk itself is excluded from the final top-K passed to the LLM
