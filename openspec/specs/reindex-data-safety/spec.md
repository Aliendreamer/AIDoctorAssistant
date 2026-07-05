# reindex-data-safety Specification

## Purpose
TBD - created by archiving change audit-remediation. Update Purpose after archive.
## Requirements
### Requirement: Destructive re-index is scoped to a single book

A `force=true` re-index of a book SHALL delete only that book's data. It SHALL NOT drop the
Qdrant collection and SHALL NOT truncate the shared `bm25_vocab` or `bm25_stats` tables.

Qdrant point deletion SHALL use a filter on the `BookId` payload. BM25 document-frequency
contribution SHALL be adjusted per book (the target book's contribution subtracted and re-added),
never globally cleared.

#### Scenario: Force re-index preserves other books

- **WHEN** books A, B, and C are indexed and book B is re-indexed with `force=true`
- **THEN** all of book A's and book C's Qdrant points remain searchable
- **AND** `bm25_vocab`/`bm25_stats` still reflect A's and C's terms

#### Scenario: Log message matches actual scope

- **WHEN** a single-book force re-index runs
- **THEN** any log stating the action is "for {BookId}" reflects a delete scoped to that book id only

