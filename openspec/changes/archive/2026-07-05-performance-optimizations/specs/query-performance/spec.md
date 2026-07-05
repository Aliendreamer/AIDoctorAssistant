## ADDED Requirements

### Requirement: Read-only queries do not track entities

Repository/service queries that only read (never mutate the loaded entity) SHALL use
`AsNoTracking()` to avoid EF change-tracking overhead. Queries that load an entity to update it SHALL
remain tracked.

#### Scenario: Dictionary expansion is untracked

- **WHEN** `MedicalDictionaryService.ExpandQueryAsync` queries illnesses
- **THEN** the query uses `AsNoTracking()`

#### Scenario: Update path stays tracked

- **WHEN** a book is loaded to update its status/outline
- **THEN** the query does NOT use `AsNoTracking()` so the change is saved

### Requirement: Filtered vector operations are index-backed

Payload fields used in Qdrant filters SHALL have payload indexes so filtered search and the
pure-filter section scroll use an index instead of scanning the collection.

#### Scenario: Filter fields are indexed

- **WHEN** the collection is ensured
- **THEN** keyword indexes exist on `BookId`, `Language`, `ChapterTitle`, `SectionTitle` and a bool
  index on `IsSummary`

### Requirement: Ingestion upserts vectors in batches

The indexer SHALL upsert points in batches (one request per checkpoint batch) rather than one
request per chunk, via `IVectorStore.UpsertBatchAsync`.

#### Scenario: Batch flushed at checkpoint

- **WHEN** the indexer reaches a checkpoint boundary
- **THEN** the accumulated points are upserted in a single batch before the checkpoint is written

### Requirement: Dense index is quantized with accuracy-preserving rescoring

The Qdrant dense vector index SHALL use int8 scalar quantization with the full-precision vectors on
disk and search-time rescoring, so memory scales sub-linearly with the corpus while retrieval
accuracy is preserved.

#### Scenario: Quantization with rescore

- **WHEN** the collection is created (or back-filled)
- **THEN** the dense vectors are int8 scalar-quantized with originals on disk
- **AND** searches request rescoring so the top candidates are re-ranked with full-precision vectors
