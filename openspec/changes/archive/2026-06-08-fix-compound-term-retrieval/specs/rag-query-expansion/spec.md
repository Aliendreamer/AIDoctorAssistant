## MODIFIED Requirements

### Requirement: Keyword-level query expansion

`ExpandQueryAsync` SHALL tokenise the input query on whitespace and punctuation, filter out Bulgarian and English function stopwords and tokens shorter than 4 characters, and add the surviving words as independent search terms alongside the original phrase.

This expansion SHALL occur regardless of whether any matching entry exists in the illness dictionary, so that individual keywords are available for fallback searches. However, callers SHALL NOT use the per-keyword fragments in the initial retrieval pass — they are reserved for iterative retry iterations only.

#### Scenario: Phrase query produces keyword terms

- **WHEN** the query is "болест на гравес"
- **THEN** `ExpandQueryAsync` returns at minimum `["болест на гравес", "гравес"]`

#### Scenario: Stopwords are excluded

- **WHEN** the query contains only stopwords (e.g., "на и с")
- **THEN** no additional keyword terms are added beyond the original phrase

#### Scenario: Single-word query produces no additional terms

- **WHEN** the query is a single meaningful word with no stopwords
- **THEN** `ExpandQueryAsync` returns only that word

#### Scenario: Dictionary match still applies

- **WHEN** an extracted keyword matches an entry in the illness dictionary
- **THEN** all aliases from that entry are also added to the expanded term set

#### Scenario: Caller uses only full query for initial search

- **WHEN** a RAG plugin initiates the first gather pass
- **THEN** it passes only the original query string to `GatherCandidatesAsync`, ignoring per-keyword fragments until the retry loop
