# answer-citation-markers Specification

## Purpose
TBD - created by archiving change cited-answer-markers. Update Purpose after archive.
## Requirements
### Requirement: Numbered excerpts map to the source list
The RAG answer builder SHALL number the excerpts it feeds to the model `[1]`, `[2]`, … in the same
order the `sources` list for that answer is constructed, such that citation marker `n` corresponds
to `sources[n-1]`. When web sources are merged into a book answer, book sources SHALL retain their
positions (and therefore their numbers) in the merged list.

#### Scenario: Excerpt numbering matches source order
- **WHEN** the answer builder assembles the model context from the retrieved chunks
- **THEN** each excerpt is prefixed with its 1-based position, and that position equals the
  position (+1) of the corresponding entry in the answer's `sources` list

#### Scenario: Book source numbers preserved after web merge
- **WHEN** web sources are appended to a book answer's sources
- **THEN** the book sources keep their original indices `1..K` and web sources take indices `K+1…`

### Requirement: Model-emitted citation markers
The book-RAG system prompts SHALL instruct the model to support factual clinical claims by appending
the supporting excerpt number(s) in square brackets (e.g. `[1]` or `[2][4]`), while preserving the
existing continuous-prose style (no lists, no markdown, answering in the question's language). The
instruction SHALL tell the model to cite only excerpts that support the specific claim and never to
invent a number not present in the excerpts.

#### Scenario: Cited claim
- **WHEN** the model states a claim supported by excerpt 2
- **THEN** it appends `[2]` after that claim, within flowing prose

#### Scenario: Prose style preserved
- **WHEN** an answer is generated with citation markers enabled
- **THEN** it is still continuous prose with no bullet lists, numbered lists, headings, or bold /
  italic markdown

#### Scenario: Language preserved
- **WHEN** the question is in Bulgarian
- **THEN** the answer is in Bulgarian with the same bracketed `[n]` markers

### Requirement: Markers preserved through web enrichment
When a book answer is enriched with web sources, the enrichment step SHALL preserve any existing
`[n]` markers from the book answer unchanged, without renumbering or removing them. Pure-web answers
SHALL contain no citation markers.

#### Scenario: Enriched answer keeps book markers
- **WHEN** a book answer containing `[1]` and `[3]` is enriched with web excerpts
- **THEN** the enriched answer still contains `[1]` and `[3]` referring to the same book sources

#### Scenario: Pure web answer has no markers
- **WHEN** an answer is generated only from web sources (no book results)
- **THEN** it contains no `[n]` markers and cites web sources by article title

### Requirement: Markdown stripping preserves markers
The answer post-processing that strips light markdown SHALL NOT remove or alter `[n]` citation
markers.

#### Scenario: Markers survive stripping
- **WHEN** an answer containing `[1]` and `[2][3]` is passed through markdown stripping
- **THEN** those markers appear unchanged in the stripped output

### Requirement: Range-guarded marker rendering
The Query UI SHALL render `[n]` (and comma-grouped `[a, b]`) markers in an answer as styled
superscript references, each showing a tooltip naming the corresponding source. A marker whose
number falls outside `1..sourceCount` for that message SHALL be rendered as plain text rather than a
reference.

#### Scenario: In-range marker rendered as reference
- **WHEN** an answer with 3 sources contains `[2]`
- **THEN** `[2]` renders as a superscript reference marker whose tooltip names source 2

#### Scenario: Out-of-range marker rendered as text
- **WHEN** an answer with 3 sources contains `[9]`
- **THEN** `[9]` renders as plain text, not a reference marker

#### Scenario: Comma group renders multiple markers
- **WHEN** an answer contains `[1, 3]` and both are in range
- **THEN** two reference markers (1 and 3) are rendered

### Requirement: Graceful degradation without markers or sources
An answer that contains no markers SHALL render identically to the pre-change behaviour. An answer
whose per-message sources are not available in the current session (e.g. loaded from history) SHALL
render any bracket text as plain text and SHALL NOT produce broken references.

#### Scenario: No markers present
- **WHEN** an answer contains no `[n]` markers
- **THEN** it renders as continuous prose exactly as before this change

#### Scenario: History-loaded answer
- **WHEN** an answer loaded from chat history contains `[1]` but has no in-session sources
- **THEN** `[1]` renders as plain text with no broken link

