## ADDED Requirements

### Requirement: Hybrid search combines dense and sparse rankings
The system SHALL execute a hybrid Qdrant query that combines a dense (cosine) named vector search with a sparse (BM25) named vector search using Reciprocal Rank Fusion (RRF) to produce a single merged result list.

#### Scenario: Query returns fused results
- **WHEN** `SearchAsync` is called with both a dense query vector and a sparse query vector
- **THEN** Qdrant returns up to `topK` chunks ranked by RRF fusion of dense and sparse scores

#### Scenario: Exact-term match is surfaced despite low cosine similarity
- **WHEN** the query contains a rare medical term that appears verbatim in a passage
- **THEN** that passage MUST appear in the top-K results (BM25 component ensures exact-term recall)

### Requirement: Language and book filters apply to hybrid query
The system SHALL apply the same `language` and `bookIds` payload filters to hybrid queries as it does to dense-only queries.

#### Scenario: Bulgarian-only filter on hybrid query
- **WHEN** `SearchAsync` is called with `LanguageFilter.Bulgarian` and a hybrid query
- **THEN** only chunks with `language == "bg"` are returned regardless of dense or sparse scores

#### Scenario: Book ID filter on hybrid query
- **WHEN** `SearchAsync` is called with a non-empty `bookIds` list
- **THEN** results are restricted to chunks whose `book_id` payload value is in `bookIds`

### Requirement: Graceful degradation to dense-only when sparse vector is absent
The system SHALL fall back to dense-only nearest-neighbour search when `sparseQueryVector` is null or empty.

#### Scenario: Null sparse vector falls back to dense
- **WHEN** `SearchAsync` is called with a null `sparseQueryVector`
- **THEN** the system issues a dense-only query and returns results identical to the pre-hybrid behaviour
