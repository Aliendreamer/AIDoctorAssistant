## ADDED Requirements

### Requirement: Global BM25 stats table

The system SHALL store `total_documents` in a dedicated `bm25_stats` table with a single row (id=1), replacing the redundant per-row `total_documents` column in `bm25_vocab`.

#### Scenario: Stats row is created on first upsert
- **WHEN** `BM25VocabService.UpsertTermsAsync` is called with a new `totalDocs` value
- **THEN** an INSERT OR UPDATE is performed on `bm25_stats` (id=1) setting `total_documents` and `updated_at`
- **AND** the row count of `bm25_stats` remains exactly 1

#### Scenario: Stats are read during vocab load
- **WHEN** `BM25VocabService.LoadAsync` is called
- **THEN** `total_documents` is read from `bm25_stats` (id=1), not from `bm25_vocab`

#### Scenario: Vocab update no longer touches all rows
- **WHEN** `UpsertTermsAsync` is called with N new terms
- **THEN** only the N matching or new rows in `bm25_vocab` are written
- **AND** no `UPDATE bm25_vocab SET total_documents = ...` statement is executed

### Requirement: Strip base64 images before chunking

The system SHALL remove inline base64 image markdown (`![alt](data:<mime>;base64,<blob>)`) from markdown text before any chunking, tokenization, or embedding occurs.

#### Scenario: Base64 images are absent from chunk text
- **WHEN** a markdown document containing `![img](data:image/png;base64,iVBORw...)` is chunked
- **THEN** the resulting chunks contain no `data:` URI substrings
- **AND** the chunk text length is significantly shorter than the raw markdown length for image-heavy documents

#### Scenario: Normal markdown is unaffected
- **WHEN** a markdown document with regular image links `![alt](https://...)` or no images is chunked
- **THEN** the chunking output is identical to processing without the strip step

### Requirement: Force re-index clears vocab and stats

The system SHALL truncate `bm25_vocab` and reset `bm25_stats.total_documents` to 0 when a force re-index is triggered, in addition to clearing Qdrant and the ingestion checkpoint.

#### Scenario: Force re-index starts with empty vocab
- **WHEN** `POST /api/admin/index?force=true` is called
- **THEN** `bm25_vocab` is truncated (row count = 0)
- **AND** `bm25_stats.total_documents` is set to 0 (or the row is deleted)
- **AND** subsequent indexing builds vocab from scratch
