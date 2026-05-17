## ADDED Requirements

### Requirement: PostgreSQL container in docker-compose
The docker-compose stack SHALL include a `postgres` service using the official `postgres:17-alpine` image with a named volume for data persistence and a health-check so dependent services wait for readiness.

#### Scenario: Stack starts cleanly
- **WHEN** `docker compose up` is run on a fresh machine
- **THEN** the postgres container starts, passes its health-check, and the web service connects successfully

#### Scenario: Data survives restart
- **WHEN** `docker compose down` followed by `docker compose up` is run
- **THEN** previously written books and vocab records are still present in the database

### Requirement: Connection string configuration
The system SHALL read the PostgreSQL connection string from `Database:ConnectionString` in configuration. The shared config SHALL provide a default pointing to the Docker postgres service (`Host=postgres;Database=medassist;Username=medassist;Password=medassist`). The `Database:Path` key SHALL be removed entirely.

#### Scenario: Connection string present
- **WHEN** `Database:ConnectionString` is set in configuration
- **THEN** `MedAssistDbContext` connects to that PostgreSQL instance

#### Scenario: Connection string missing
- **WHEN** `Database:ConnectionString` is absent from configuration
- **THEN** application startup throws `InvalidOperationException` with a clear message

### Requirement: Schema managed by EF migrations
The PostgreSQL schema SHALL be created and evolved exclusively via EF Core migrations. On application startup the web and indexer processes SHALL call `MigrateAsync()` to apply any pending migrations automatically.

#### Scenario: First run on empty database
- **WHEN** the app starts against a postgres instance with no schema
- **THEN** `MigrateAsync()` creates all tables and the app proceeds normally

#### Scenario: Migration already applied
- **WHEN** the app starts against a postgres instance where migrations are already applied
- **THEN** `MigrateAsync()` is a no-op and the app proceeds normally
