## ADDED Requirements

### Requirement: Bulgarian search query

Before retrieval, the system SHALL derive a Bulgarian search query from the user's question. A
Bulgarian (Cyrillic) question that is not a follow-up SHALL be used unchanged, without an LLM call.
A non-Bulgarian question, or a follow-up, SHALL be turned into a concise, self-contained Bulgarian
search query by a single LLM call that keeps eponyms, drug names and ICD codes as written.

#### Scenario: Bulgarian question is used as-is

- **WHEN** the question is "Фебрилни гърчове при деца" and there is no history
- **THEN** no LLM call is made and the search query equals the question

#### Scenario: English question is translated

- **WHEN** the question is "What is Kawasaki disease?"
- **THEN** one LLM call returns a Bulgarian search query, and retrieval uses it together with the
  original question

#### Scenario: Follow-up is made self-contained

- **WHEN** the previous question was about gastroenteritis treatment and the follow-up is
  "А при кърмачета?"
- **THEN** the search query is a self-contained Bulgarian query about treating gastroenteritis in
  infants

### Requirement: Fast query-preparation call

The query-preparation call SHALL disable model thinking, cap its output tokens, use temperature 0,
and request the same context window as answer calls. All answer calls SHALL also disable thinking.

#### Scenario: Rewrite stays short

- **WHEN** a follow-up question triggers query preparation
- **THEN** the model generates at most the configured output cap (96 tokens)

### Requirement: Language-aware reranking

Candidates SHALL be reranked against the query in their own language: Bulgarian chunks against
the Bulgarian search query, other-language chunks against the original question.

#### Scenario: English question over Bulgarian books

- **WHEN** an English question retrieves only Bulgarian chunks
- **THEN** every chunk is scored against the Bulgarian search query

### Requirement: Latin-term guard uses the search query

The Latin-term domain-drift guard SHALL extract its terms from the Bulgarian search query, so that
only terms that survive translation (eponyms, drug names) must appear in the top chunks.

#### Scenario: Generic English words are not required verbatim

- **WHEN** the question is "Treatment of acute otitis media in children"
- **THEN** words such as "treatment" and "children" are not required to appear in the chunks

#### Scenario: Eponym still guarded

- **WHEN** the search query contains "Chiari" and none of the top chunks mention it
- **THEN** the answer is rejected as insufficiently relevant, as before
