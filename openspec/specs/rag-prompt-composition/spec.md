# rag-prompt-composition Specification

## Purpose
TBD - created by archiving change streamline-rag-prompts. Update Purpose after archive.
## Requirements
### Requirement: Single prompt-composition point

All book-RAG answer prompts SHALL be assembled by one prompt builder from a shared core and a
per-query-type profile. A query type SHALL contribute only its delta (task framing and few-shot
example) and SHALL NOT restate persona, prose-style, citation, or language rules owned by the core.

#### Scenario: Plugin supplies only its delta

- **WHEN** the differential-diagnosis query type builds an answer prompt
- **THEN** the prompt contains the shared core once and the DDx framing ("most likely first") once,
  and the persona sentence appears exactly once

#### Scenario: Core change applies everywhere

- **WHEN** the shared prose-style rule is edited
- **THEN** every book query type's prompt reflects the edit without any per-plugin change

### Requirement: Each instruction stated once

The final prompt sent to the answer model SHALL contain the citation instruction exactly once
(placed in the final user message, after the numbered excerpts), the answer-language instruction
exactly once (the script-detected line in the final user message), and the prose / no-markdown
rule exactly once. The few-shot example is not counted as a restatement.

#### Scenario: Citation instruction appears once

- **WHEN** an answer prompt is built for any book query type
- **THEN** the citation instruction occurs once, in the final user message, after the excerpts

#### Scenario: Language instruction appears once

- **WHEN** an answer prompt is built for a Bulgarian question
- **THEN** exactly one answer-language instruction is present and it requires Bulgarian

### Requirement: Excerpts carry their own provenance

The answer prompt SHALL NOT include a separate list of all searched books; each numbered excerpt
SHALL carry its book, chapter and section title.

#### Scenario: No searched-books list

- **WHEN** an answer prompt is built with five books selected
- **THEN** the prompt contains no "Sources searched" list and each excerpt header names its book

### Requirement: Sanitised, bounded conversation history

Prior assistant turns sent to the answer model SHALL have `[n]` citation markers removed. The
history SHALL be bounded by a configured character budget in addition to the message-count window,
dropping the oldest turns first and never splitting a user/assistant pair.

#### Scenario: Stale markers removed

- **WHEN** a prior assistant answer `"Graves' disease is autoimmune [1][3]."` is included in history
- **THEN** the model receives `"Graves' disease is autoimmune."` with no bracketed markers

#### Scenario: Budget drops oldest pairs

- **WHEN** the history exceeds the character budget
- **THEN** the oldest user/assistant pairs are dropped until it fits, and the most recent pair is
  always kept whole

### Requirement: Web prompts reuse the shared core

The web-enrichment and web-only answer prompts SHALL be composed from the same shared core plus
their own delta. The untrusted `<web_source>` content guard SHALL remain present in both.

#### Scenario: Web-only prompt keeps the injection guard

- **WHEN** a web-only answer prompt is built
- **THEN** it contains the shared core once and the untrusted-content guard once

### Requirement: Prompt-token observability

The system SHALL record the prompt token count of each answer-generation call as a metric tagged by
query type, using the model-reported count when available and a character-based estimate otherwise.

#### Scenario: Metric recorded per answer

- **WHEN** an answer is generated for a Disease query
- **THEN** one prompt-token observation tagged `plugin_type=Disease` is recorded

### Requirement: Context window sized for RAG prompts

Every chat call to the model SHALL request a context window of at least 8,192 tokens, and the
answer prompt SHALL place the answer-language and citation instructions after the numbered
excerpts, so that they survive if an over-long prompt is truncated from the front.

#### Scenario: Context window requested

- **WHEN** any answer, rewrite, or web chat call is sent to Ollama
- **THEN** its request options carry `num_ctx` of at least 8,192

#### Scenario: Instructions placed after excerpts

- **WHEN** an answer prompt is built
- **THEN** the language line and the citation instruction both follow the last excerpt

