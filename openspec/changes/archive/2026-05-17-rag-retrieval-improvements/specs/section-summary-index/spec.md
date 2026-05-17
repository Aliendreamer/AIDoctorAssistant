## ADDED Requirements

### Requirement: Section summary chunk generation

During book indexing, `BookIndexer` SHALL generate one additional "section summary" chunk per unique heading section, in addition to the regular content chunks.

The summary chunk text SHALL be: `"{HeadingPath}\n\n{first 800 characters of the first content chunk in the section}"`.

The summary chunk SHALL have `IsSummary = true` on the `MedicalChunk` model and `is_summary: true` in the Qdrant payload.

Summary chunks SHALL share the same `book_id`, `chapter_title`, `section_title`, and `language` payload fields as the regular chunks in their section.

#### Scenario: One summary per section is stored

- **WHEN** a section produces N regular chunks
- **THEN** exactly one summary chunk is stored in Qdrant for that section, in addition to the N regular chunks

#### Scenario: Summary text encodes heading and opening content

- **WHEN** the summary chunk is stored
- **THEN** its text begins with the full heading path followed by a blank line and the first 800 characters of the section's opening chunk

#### Scenario: Summary is flagged in payload

- **WHEN** the summary chunk is stored in Qdrant
- **THEN** its payload contains `is_summary: true`

### Requirement: Summary chunks excluded from LLM answer sources

The RAG pipeline SHALL exclude chunks with `IsSummary = true` from the final top-K list returned to the LLM. Summary chunks MAY be used as retrieval triggers (to identify relevant sections) but SHALL NOT appear as answer sources.

#### Scenario: Summary not included in final answer

- **WHEN** a summary chunk scores in the top-K after reranking
- **THEN** it is removed from the result set before the chunks are passed to the LLM prompt
