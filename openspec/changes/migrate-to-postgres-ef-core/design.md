## Context

The current stack uses raw ADO.NET + `Microsoft.Data.Sqlite` across four projects. The schema lives as a DDL string in `DbInitializer`, and five repositories/services contain hand-written parameterised SQL. There is no migration system — schema changes require manually dropping and recreating the database file. The existing Docker stack (Qdrant, SearXNG, OTel Collector, Tempo, Grafana) makes adding a PostgreSQL container trivial.

## Goals / Non-Goals

**Goals:**
- Replace all SQLite/raw SQL with EF Core + Npgsql (PostgreSQL provider)
- Introduce a `MedAssist.Data` project as the single home for DbContext, entities, and migrations
- Auto-apply migrations at startup via `MigrateAsync()`
- Add `postgres:17-alpine` to docker-compose with a persistent named volume

**Non-Goals:**
- SQLite compatibility shim or dual-provider support
- Repository pattern abstraction layer — services inject `MedAssistDbContext` directly
- Splitting into multiple DbContexts (one context for the whole app)

## Decisions

### Project: MedAssist.Data (new)
Houses `MedAssistDbContext`, entity classes, `IEntityTypeConfiguration<T>` configs, and the `Migrations/` folder. Referenced by `MedAssist.AI`, `MedAssist.Indexer`, and `MedAssist.Web`. `Microsoft.EntityFrameworkCore.Design` added here only (needed for `dotnet ef` tooling).

**Alternative considered**: putting DbContext in `MedAssist.Shared` — rejected because Shared has no EF dependency today and adding it pollutes the thin shared layer.

### Direct DbContext injection, no repository abstraction
Services that previously held `SqliteConnection` now receive `MedAssistDbContext` (or `IDbContextFactory<MedAssistDbContext>` for singletons that need to create short-lived scopes). No `IBookRepository` interface introduced — the DbContext is the repository.

**Alternative considered**: wrapping in repository interfaces — rejected as unnecessary indirection for a single-database app with no plans to swap providers.

### IDbContextFactory for singleton services
`BM25VocabService` and `MedicalDictionaryService` are registered as singletons but need to query the DB. They SHALL receive `IDbContextFactory<MedAssistDbContext>` and call `CreateDbContext()` per operation to avoid holding a long-lived DbContext in a singleton.

`BookCatalogService` (Web) is also a singleton — same pattern.

Scoped services in Indexer CLI commands receive `MedAssistDbContext` directly.

### MigrateAsync at startup
Both `MedAssist.Web/Program.cs` and `MedAssist.Indexer` CLI entry point call `db.Database.MigrateAsync()` before any other operation. This is safe for a single-container deployment; for multi-instance scale-out a migration job would be needed, but that is out of scope.

### Entity naming: `*Entity` suffix
EF entity classes use the `Entity` suffix (`BookEntity`, `IllnessEntity`, etc.) to avoid collision with the existing `BookInfo`, `BM25VocabSnapshot` etc. shared models. Existing shared models are retained as-is; services map between entity and domain model where needed.

### Column names: snake_case via `UseSnakeCaseNamingConvention()`
Npgsql's `UseSnakeCaseNamingConvention()` applied globally on `MedAssistDbContext` so C# `TotalChunks` maps to `total_chunks` automatically — matching the existing SQLite schema without per-property annotation.

## Risks / Trade-offs

- **Migration drift**: if EF-generated migration columns differ from old SQLite schema (e.g. `TEXT` vs `varchar`), existing data imports will need column-type awareness. Mitigation: review generated migration SQL before merging.
- **Singleton DbContext factory overhead**: `CreateDbContext()` per call allocates a new context. For the BM25 bulk load (called once at startup) this is negligible; for `BookCatalogService` (called per API request) a scoped registration would be cleaner — reconsider if Web moves to per-request scoped services.
- **MigrateAsync in both Web and Indexer**: if both run simultaneously against an empty DB, one migration run may conflict. Mitigation: postgres advisory locks are used by EF Core's migration history table, so concurrent `MigrateAsync` calls are safe.

## Migration Plan

1. Add `MedAssist.Data` project and entities
2. Add `postgres` service to docker-compose, update config
3. Register DbContext in all three consuming projects
4. Run `dotnet ef migrations add InitialCreate -p MedAssist.Data -s MedAssist.Indexer`
5. Replace raw SQL services/repositories one project at a time (AI → Web → Indexer)
6. Remove `Microsoft.Data.Sqlite` from all csproj files, delete `DbInitializer`
7. Build 0/0, tests pass, docker-compose up validates end-to-end
