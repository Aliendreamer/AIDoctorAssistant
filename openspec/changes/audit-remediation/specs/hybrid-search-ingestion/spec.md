## ADDED Requirements

### Requirement: BM25 vocabulary exists before sparse vectors are produced

Indexing SHALL build and persist the BM25 vocabulary (document frequencies) and refresh the
in-memory snapshot BEFORE any chunk's sparse vector is computed and upserted. No book — including
the first book indexed on a fresh database — SHALL be stored with empty sparse vectors while it
contains indexable terms.

#### Scenario: First book on a fresh database has sparse vectors

- **WHEN** the first book is indexed against an empty `bm25_vocab`
- **THEN** its upserted Qdrant points contain non-empty sparse named vectors

### Requirement: Deterministic Qdrant point ids

Each Qdrant point id SHALL be derived deterministically from its identity (UUIDv5 of
`"{bookId}:{chunkIndex}"` for chunks, `"summary:{bookId}:{section}"` for section summaries) so that
re-indexing overwrites existing points instead of creating duplicates.

#### Scenario: Re-indexing does not duplicate points

- **WHEN** a book is indexed twice with the same content
- **THEN** the Qdrant point count for that book is identical after the second run

### Requirement: Index start is atomic against concurrent triggers

An index trigger SHALL mark the book `InProgress` atomically (a conditional update or equivalent
guard) before returning 202, so a second concurrent trigger for the same book observes the updated
status and is rejected.

#### Scenario: Concurrent triggers yield one accept

- **WHEN** two index requests for the same book arrive simultaneously
- **THEN** exactly one receives 202 and the other receives 409
