## Why

Raw ADO.NET with SQLite is holding back the project: schema changes require editing a string constant, there are no migrations, repositories are verbose hand-written SQL with no type safety, and SQLite's single-writer model is a poor fit for a containerised stack that already runs Qdrant, SearXNG, and OTel. Replacing it with EF Core + PostgreSQL gives us typed models, incremental migrations, and a proper multi-connection database that fits naturally alongside the existing Docker services.

## What Changes

- **BREAKING** Remove `Microsoft.Data.Sqlite` from all projects — no SQLite fallback
- **BREAKING** Replace `Database:Path` config key with `Database:ConnectionString` (standard Postgres connection string)
- Add new `MedAssist.Data` project: `MedAssistDbContext`, EF Core entity classes, Npgsql provider, initial migration
- Delete `MedAssist.Indexer/Database/DbInitializer.cs` and all 4 raw repositories
- Delete `MedAssist.AI/Dictionary/BM25VocabService.cs` and `MedicalDictionaryService.cs` raw SQL, replace with EF Core equivalents
- Delete `MedAssist.Web/Services/BookCatalogService.cs` raw SQL, replace with EF Core
- Add `postgres` service to `docker-compose.yml`
- Add `Database:ConnectionString` to `config/appsettings.shared.json` pointing to the Docker Postgres instance
- Register `MedAssistDbContext` in DI for all consuming projects
- Run `dotnet ef migrations add InitialCreate` to generate the first migration

## Capabilities

### New Capabilities
- `postgres-database`: PostgreSQL container wired into docker-compose with health-check, persistent volume, and connection string config
- `ef-core-data-layer`: `MedAssist.Data` project with `MedAssistDbContext`, typed entity classes (`BookEntity`, `IllnessEntity`, `IllnessAliasEntity`, `Bm25VocabEntity`, `IngestionCheckpointEntity`), Npgsql provider, and EF migrations replacing all raw SQL

### Modified Capabilities

## Impact

- `MedAssist.Data` — new project, referenced by AI, Indexer, and Web
- `MedAssist.AI` — removes `Microsoft.Data.Sqlite`, BM25VocabService and MedicalDictionaryService rewritten against DbContext
- `MedAssist.Indexer` — removes `Microsoft.Data.Sqlite`, DbInitializer and all 4 repositories deleted, schema managed by EF migrations
- `MedAssist.Web` — removes `Microsoft.Data.Sqlite`, BookCatalogService rewritten against DbContext
- `config/appsettings.shared.json` — `Database:Path` → `Database:ConnectionString`
- `docker-compose.yml` — adds `postgres` service
- New NuGet packages: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (Indexer for migrations)
