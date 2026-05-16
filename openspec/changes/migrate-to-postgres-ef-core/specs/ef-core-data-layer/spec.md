## ADDED Requirements

### Requirement: MedAssist.Data project with DbContext and entities
The solution SHALL contain a `MedAssist.Data` project that defines `MedAssistDbContext` and the following entity classes mapping to the existing schema: `BookEntity`, `IllnessEntity`, `IllnessAliasEntity`, `Bm25VocabEntity`, `IngestionCheckpointEntity`. All other projects SHALL reference `MedAssist.Data` instead of `Microsoft.Data.Sqlite`.

#### Scenario: DbContext resolves from DI
- **WHEN** any service in Web, AI, or Indexer requests `MedAssistDbContext` from the DI container
- **THEN** a configured instance connected to PostgreSQL is returned

#### Scenario: No raw SqliteConnection anywhere
- **WHEN** the solution is built
- **THEN** no `using Microsoft.Data.Sqlite` directive exists in any source file

### Requirement: Entity mappings preserve existing schema
The EF entity configurations SHALL produce the same column names, types, and constraints as the current hand-written DDL so the initial migration matches the SQLite schema exactly (modulo SQLite-specific syntax).

#### Scenario: books table columns
- **WHEN** the initial migration is applied
- **THEN** the `books` table has columns: `id` (text PK), `title`, `author`, `language`, `edition`, `total_chunks` (int), `status` (text), `indexed_at` (timestamptz, nullable)

#### Scenario: bm25_vocab table columns
- **WHEN** the initial migration is applied
- **THEN** the `bm25_vocab` table has columns: `id` (serial PK), `term` (text unique), `document_frequency` (int), `total_documents` (int), `updated_at` (timestamptz)

#### Scenario: illnesses and aliases FK constraint
- **WHEN** an `IllnessAliasEntity` references a non-existent `IllnessEntity`
- **THEN** PostgreSQL enforces the foreign key and rejects the insert

### Requirement: BM25 vocab bulk load via EF Core
`BM25VocabService` SHALL load the vocab snapshot using `MedAssistDbContext` with `AsNoTracking()` for efficient bulk read, filtering to terms with `DocumentFrequency >= 2`.

#### Scenario: Vocab loaded at startup
- **WHEN** `IBM25VocabStore.LoadAsync()` is called
- **THEN** all qualifying terms are returned as a `BM25VocabSnapshot` without change tracking overhead

### Requirement: Medical dictionary queries via EF Core
`MedicalDictionaryService` SHALL query illnesses and their aliases using `MedAssistDbContext` with `Include()` for the alias navigation property.

#### Scenario: Query expansion returns aliases
- **WHEN** `ExpandQueryAsync("hypertension")` is called
- **THEN** all aliases for matching illnesses are included in the expanded term list

### Requirement: Repository operations via EF Core
All write operations (upsert book, upsert checkpoint, bulk-upsert vocab) in `MedAssist.Indexer` SHALL use `MedAssistDbContext`. Upsert semantics SHALL use `ExecuteUpdateAsync` / `AddOrUpdate` patterns appropriate for PostgreSQL via EF Core.

#### Scenario: Book upsert on first index
- **WHEN** a book is indexed for the first time
- **THEN** a new `BookEntity` row is inserted

#### Scenario: Book upsert on re-index
- **WHEN** a book with an existing `id` is re-indexed
- **THEN** the existing row is updated, not duplicated

### Requirement: Remove SQLite entirely
`Microsoft.Data.Sqlite` SHALL be removed from all `.csproj` files. `DbInitializer` SHALL be deleted. No SQLite connection strings or file paths SHALL remain in configuration or code.

#### Scenario: Build contains no SQLite reference
- **WHEN** the solution is built after the migration
- **THEN** no project references `Microsoft.Data.Sqlite`
