## ADDED Requirements

### Requirement: IngestionStatus provides canonical status string values
`MedAssist.Shared.Constants.IngestionStatus` SHALL expose `const string` fields for every status value used in book and checkpoint DB columns: `Pending` (`"pending"`), `InProgress` (`"in_progress"`), `Indexed` (`"indexed"`), `Complete` (`"complete"`).

#### Scenario: BookEntity default uses constant
- **WHEN** a `BookEntity` is constructed without an explicit status
- **THEN** its `Status` property initializer SHALL reference `IngestionStatus.Pending`

#### Scenario: IngestionCheckpointEntity default uses constant
- **WHEN** an `IngestionCheckpointEntity` is constructed without an explicit status
- **THEN** its `Status` property initializer SHALL reference `IngestionStatus.InProgress`

#### Scenario: EF configuration defaults use constants
- **WHEN** `BookEntityConfiguration` sets `HasDefaultValue` for the status column
- **THEN** it SHALL pass `IngestionStatus.Pending`
- **WHEN** `IngestionCheckpointEntityConfiguration` sets `HasDefaultValue` for the status column
- **THEN** it SHALL pass `IngestionStatus.InProgress`

#### Scenario: BookIndexer checkpoint call uses constant
- **WHEN** `BookIndexer.SaveCheckpointAsync` is called during indexing
- **THEN** the status argument SHALL reference `IngestionStatus.InProgress`

#### Scenario: BookIndexer resume guard uses constant
- **WHEN** `BookIndexer` checks whether a checkpoint is already complete
- **THEN** the comparison value SHALL reference `IngestionStatus.Complete`

#### Scenario: BookCatalogService filter uses constant
- **WHEN** `BookCatalogService.GetAllBooksAsync` filters books by status
- **THEN** the `.Where` predicate SHALL compare against `IngestionStatus.Indexed`
