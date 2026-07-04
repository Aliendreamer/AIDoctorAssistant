# ingestion-status-constants Specification

## Purpose
Represent book and checkpoint ingestion status as a single strongly-typed value across the code and
the database, eliminating raw string literals. The original string-constants approach
(`IngestionStatus` const strings) has been **superseded by the `BookStatus` enum**; this spec
documents the enum as the canonical representation.

## Requirements

### Requirement: BookStatus enum is the canonical status representation
`MedAssist.Shared.Models.BookStatus` SHALL be the single type for book and checkpoint ingestion
status. Its members SHALL be `Pending`, `InProgress`, `Indexed`, and `Failed`. No raw status string
literals SHALL be used in entities, EF configuration, or service logic. (There is no
`IngestionStatus` string-constants class; a prior spec that mandated one is retired by this content.)

#### Scenario: BookEntity default status
- **WHEN** a `BookEntity` is constructed without an explicit status
- **THEN** its `Status` property SHALL default to `BookStatus.Pending`

#### Scenario: IngestionCheckpointEntity default status
- **WHEN** an `IngestionCheckpointEntity` is constructed without an explicit status
- **THEN** its `Status` property SHALL default to `BookStatus.InProgress`

### Requirement: Status persists as a Postgres enum type
The `BookStatus` enum SHALL be mapped to a Postgres enum type named `book_status`
(`opt.MapEnum<BookStatus>("book_status")`), and the book `status` column default SHALL be set with
the enum value (`HasDefaultValue(BookStatus.Pending)`), not a string literal.

#### Scenario: EF configuration default uses the enum
- **WHEN** `BookEntityConfiguration` sets `HasDefaultValue` for the status column
- **THEN** it SHALL pass `BookStatus.Pending` (the enum member, not a string)

### Requirement: Indexing transitions use enum members
`BookIndexer` and the catalog query SHALL reference `BookStatus` members directly.

#### Scenario: Checkpoint writes use the enum
- **WHEN** `BookIndexer` writes a checkpoint during indexing
- **THEN** the status SHALL be a `BookStatus` member (`InProgress` while running, `Indexed` on completion)

#### Scenario: Catalog filter uses the enum
- **WHEN** the book catalog lists searchable books
- **THEN** the filter SHALL compare against `BookStatus.Indexed`
