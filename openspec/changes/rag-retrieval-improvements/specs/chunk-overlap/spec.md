## ADDED Requirements

### Requirement: Overlapping chunk generation

`MarkdownChunker` SHALL carry forward the last `OverlapChars` characters of chunk N as a prefix when generating chunk N+1 within the same heading section. The overlap text SHALL be prepended verbatim before the new section content.

The overlap SHALL only apply between consecutive chunks produced from the same section (same heading path). It SHALL NOT carry over across heading boundaries.

The overlap size SHALL default to 512 characters and SHALL be configurable via the `MarkdownChunker` constructor.

#### Scenario: Overlap prefix is prepended to subsequent chunk

- **WHEN** a section produces two or more chunks due to the max-token limit
- **THEN** chunk N+1 begins with the last `OverlapChars` characters of chunk N's text

#### Scenario: No overlap across heading boundaries

- **WHEN** a new heading is encountered and a new section begins
- **THEN** the overlap carry-forward resets and chunk N+1 does NOT include text from the previous section

#### Scenario: Single chunk section produces no overlap

- **WHEN** a section fits within the max-token limit and produces exactly one chunk
- **THEN** no overlap prefix is added to the next section's first chunk

#### Scenario: Overlap does not push chunk over max-token limit

- **WHEN** the overlap prefix plus the new content would exceed `_maxTokens`
- **THEN** the chunk is split further so each resulting chunk stays within `_maxTokens`
