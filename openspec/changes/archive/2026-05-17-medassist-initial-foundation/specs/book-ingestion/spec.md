## ADDED Requirements

### Requirement: Indexer accepts pre-processed markdown files from Docling
The Indexer SHALL read markdown files from a configured `books/processed/` directory. Each markdown file SHALL correspond to one book. Raw PDF files SHALL be stored in `books/raw/` but are NOT processed at runtime — Docling preprocessing is a manual offline step.

#### Scenario: Indexer processes a markdown book file
- **WHEN** a markdown file exists in `books/processed/` and the Indexer is triggered
- **THEN** the Indexer reads the file, chunks it by heading structure, and begins embedding

### Requirement: Chunking follows heading hierarchy from markdown
The Indexer SHALL split markdown content into chunks at heading boundaries (`#`, `##`, `###`). Each chunk SHALL contain the heading path as context prefix (e.g., `"Клинична генетика > Синдром на Down"`). Chunks SHALL NOT exceed 512 tokens. Chunks smaller than 50 tokens SHALL be merged with the adjacent chunk.

#### Scenario: Section boundary produces correct chunk
- **WHEN** markdown contains `## Treatment` followed by body text
- **THEN** the resulting chunk includes the heading as prefix and the full section body

#### Scenario: Oversized section is split at sentence boundary
- **WHEN** a section exceeds 512 tokens
- **THEN** the section is split at the nearest sentence boundary within the token limit

### Requirement: Each chunk is embedded with multilingual-e5-large via ONNX
The Indexer SHALL embed each chunk using `intfloat/multilingual-e5-large` loaded via `Microsoft.ML.OnnxRuntime`. The same embedding model SHALL be used at query time in `MedAssist.AI`.

#### Scenario: Embedding produces correct vector dimension
- **WHEN** a chunk is embedded
- **THEN** the resulting vector has exactly 1024 dimensions

### Requirement: Vectors are upserted to Qdrant with structured payload
Each vector upserted to Qdrant SHALL include payload fields: `book_id`, `book_title`, `author`, `language`, `chapter_title`, `section_title`, `page_start`, `page_end`, `chunk_index`, `content_type` (`text` | `table` | `list`), `text` (full chunk text), `icd_codes` (array, may be empty).

#### Scenario: Upserted vector contains required payload
- **WHEN** a chunk is upserted to Qdrant
- **THEN** all required payload fields are present and non-null (except `icd_codes` which may be empty array)

### Requirement: Ingestion is resumable with checkpoints every 50 chunks
The Indexer SHALL write a checkpoint to the `ingestion_checkpoints` SQLite table after every 50 successfully upserted vectors. On startup, the Indexer SHALL read the last checkpoint for each book and skip already-indexed chunks.

#### Scenario: Indexer resumes after interruption
- **WHEN** Indexer is stopped after indexing 150 chunks and restarted
- **THEN** Indexer resumes from chunk 151 without re-indexing previous chunks

#### Scenario: Completed book is not re-indexed
- **WHEN** a book has `status: complete` in the checkpoint table and Indexer is triggered
- **THEN** Indexer skips that book entirely

### Requirement: Books registry is updated on successful ingestion
Upon completing ingestion of a book, the Indexer SHALL upsert a record in the `books` SQLite table with fields: `id`, `title`, `author`, `language`, `edition`, `total_chunks`, `status` (`indexed` | `in_progress` | `pending`), `indexed_at`.

#### Scenario: Book appears in registry after indexing
- **WHEN** ingestion of a book completes
- **THEN** the book record in SQLite has `status: indexed` and correct `total_chunks`
