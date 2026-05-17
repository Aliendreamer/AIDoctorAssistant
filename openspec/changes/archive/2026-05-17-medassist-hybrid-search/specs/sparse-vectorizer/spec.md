## ADDED Requirements

### Requirement: BM25 sparse vector generation for passages
The system SHALL provide a `SparseVectorizer` that computes a BM25 sparse vector (term-id → BM25 weight) for a given passage string using the persisted corpus vocabulary and IDF values.

#### Scenario: Passage vectorisation at index time
- **WHEN** `VectorizePassage(text)` is called for a chunk during ingestion
- **THEN** the method returns a `SparseVector` (non-zero entries only) with BM25-weighted term ids

#### Scenario: Empty or whitespace input
- **WHEN** `VectorizePassage` is called with an empty or whitespace string
- **THEN** the method returns an empty `SparseVector` with no entries

### Requirement: BM25 sparse vector generation for queries
The system SHALL provide a `VectorizeQuery(text)` method that computes the query-side BM25 sparse vector using IDF weights from the persisted vocabulary.

#### Scenario: Query with known vocabulary terms
- **WHEN** `VectorizeQuery("синдром на Даун")` is called after vocabulary is built
- **THEN** each vocabulary term present in the query gets a non-zero weight in the returned vector

#### Scenario: Query with out-of-vocabulary terms
- **WHEN** `VectorizeQuery` is called with terms absent from the vocabulary
- **THEN** those terms are silently ignored; the remaining in-vocabulary terms are weighted normally

### Requirement: Corpus vocabulary persisted to SQLite
The system SHALL persist the BM25 vocabulary (term, term_id, document_frequency, total_documents) to a `bm25_vocab` table in `medassist.db` so that it survives process restarts.

#### Scenario: Vocabulary loaded on startup
- **WHEN** `SparseVectorizer` is initialised at startup
- **THEN** it loads all rows from `bm25_vocab` into an in-memory index without re-scanning books

#### Scenario: Vocabulary updated after indexing a new book
- **WHEN** a book is fully indexed (all chunks processed)
- **THEN** new terms and updated document frequencies are written to `bm25_vocab`

### Requirement: Minimum document frequency threshold
The system SHALL exclude terms with document frequency below 2 from the vocabulary to prune noise and reduce sparse vector dimensionality.

#### Scenario: Hapax legomena excluded
- **WHEN** a term appears in only one document (df = 1)
- **THEN** that term MUST NOT be present in the persisted vocabulary or any generated sparse vector

### Requirement: Unicode word-boundary tokenisation
The system SHALL tokenise text using Unicode letter sequences (`\p{L}+`) lowercased, supporting both Latin and Cyrillic scripts without special configuration.

#### Scenario: Cyrillic text tokenises correctly
- **WHEN** tokenising "Синдром на Даун"
- **THEN** tokens produced are ["синдром", "на", "даун"] (lowercased Unicode words)
