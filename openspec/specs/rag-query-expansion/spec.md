## ADDED Requirements

### Requirement: Keyword-level query expansion

`ExpandQueryAsync` SHALL tokenise the input query on whitespace and punctuation, filter out Bulgarian and English function stopwords and tokens shorter than 4 characters, and add the surviving words as independent search terms alongside the original phrase.

This expansion SHALL occur regardless of whether any matching entry exists in the illness dictionary, so that individual keywords reach the vector store as separate embedding queries.

#### Scenario: Phrase query produces keyword terms

- **WHEN** the query is "болест на гравес"
- **THEN** `ExpandQueryAsync` returns at minimum `["болест на гравес", "гравес"]` (stopword "на" and short word "болест" filtered depending on stopword list)

#### Scenario: Stopwords are excluded

- **WHEN** the query contains only stopwords (e.g., "на и с")
- **THEN** no additional keyword terms are added beyond the original phrase

#### Scenario: Single-word query produces no additional terms

- **WHEN** the query is a single meaningful word with no stopwords
- **THEN** `ExpandQueryAsync` returns only that word (no duplication)

#### Scenario: Dictionary match still applies

- **WHEN** an extracted keyword matches an entry in the illness dictionary
- **THEN** all aliases from that entry are also added to the expanded term set
