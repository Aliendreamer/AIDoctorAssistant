## 1. MedAssist.Data Project

- [x] 1.1 Create `MedAssist.Data/MedAssist.Data.csproj` targeting `net10.0` with `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design`
- [x] 1.2 Add `<ProjectReference>` to `MedAssist.Shared` in `MedAssist.Data.csproj`
- [x] 1.3 Add `MedAssist.Data` to `MedAssist.slnx`
- [x] 1.4 Create entity classes: `BookEntity`, `IllnessEntity`, `IllnessAliasEntity`, `Bm25VocabEntity`, `IngestionCheckpointEntity` in `MedAssist.Data/Entities/`
- [x] 1.5 Create `MedAssistDbContext` with `DbSet<T>` for each entity, `UseSnakeCaseNamingConvention()`, and entity configurations (PK, unique indexes, FK for aliases→illnesses)
- [x] 1.6 Add `IEntityTypeConfiguration<T>` configs in `MedAssist.Data/Configuration/` for each entity to set explicit column constraints matching the existing schema

## 2. Docker and Configuration

- [x] 2.1 Add `postgres` service to `docker-compose.yml` (`postgres:17-alpine`, named volume `postgres_data`, health-check `pg_isready`, env vars `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`)
- [x] 2.2 Add `depends_on: postgres: condition: service_healthy` to the `web` service in `docker-compose.yml`
- [x] 2.3 Replace `Database:Path` with `Database:ConnectionString` in `config/appsettings.shared.json` (value: `Host=postgres;Database=medassist;Username=medassist;Password=medassist`)
- [x] 2.4 Add a local-dev override value in `config/appsettings.Development.json` pointing to `Host=localhost` for running outside Docker

## 3. EF Migrations

- [ ] 3.1 Add `<ProjectReference>` to `MedAssist.Data` in `MedAssist.Indexer.csproj` (startup project for `dotnet ef`)
- [ ] 3.2 Run `dotnet ef migrations add InitialCreate -p MedAssist.Data -s MedAssist.Indexer` to generate the first migration
- [ ] 3.3 Review generated migration SQL — verify column names and types match existing schema intent

## 4. DI Registration

- [ ] 4.1 Add `<ProjectReference>` to `MedAssist.Data` in `MedAssist.AI.csproj`, `MedAssist.Web.csproj`, and `MedAssist.Indexer.csproj`
- [ ] 4.2 Register `MedAssistDbContext` + `IDbContextFactory<MedAssistDbContext>` in `MedAssist.Web/Extensions/ServiceCollectionExtensions.cs` using the `Database:ConnectionString` config value (throw if missing)
- [ ] 4.3 Register `MedAssistDbContext` + `IDbContextFactory<MedAssistDbContext>` in `MedAssist.Indexer` DI setup

## 5. MedAssist.AI — Replace Raw SQL

- [ ] 5.1 Rewrite `BM25VocabService` to use `IDbContextFactory<MedAssistDbContext>`: create context, query `Bm25VocabEntities.AsNoTracking().Where(v => v.DocumentFrequency >= 2)`, map to `BM25VocabSnapshot`
- [ ] 5.2 Rewrite `MedicalDictionaryService` to use `IDbContextFactory<MedAssistDbContext>`: query `Illnesses.Include(i => i.Aliases).AsNoTracking()`
- [ ] 5.3 Remove `Microsoft.Data.Sqlite` package reference from `MedAssist.AI.csproj`

## 6. MedAssist.Web — Replace Raw SQL

- [ ] 6.1 Rewrite `BookCatalogService` to use `IDbContextFactory<MedAssistDbContext>`: query `Books.AsNoTracking().Where(b => b.Status == "indexed").OrderBy(b => b.Title)`, map to `BookInfo`
- [ ] 6.2 Remove `Microsoft.Data.Sqlite` package reference from `MedAssist.Web.csproj`
- [ ] 6.3 Update `ServiceCollectionExtensions.AddDataServices` — remove `BookCatalogService(dbPath)` constructor call, inject via DI factory

## 7. MedAssist.Indexer — Replace Raw SQL

- [ ] 7.1 Rewrite `BookRepository` using `MedAssistDbContext`: upsert via `ExecuteUpdateAsync` or find-then-set pattern
- [ ] 7.2 Rewrite `BM25VocabRepository` using `MedAssistDbContext`: bulk upsert vocab terms
- [ ] 7.3 Rewrite `CheckpointRepository` using `MedAssistDbContext`
- [ ] 7.4 Rewrite `IllnessDictionaryRepository` using `MedAssistDbContext`
- [ ] 7.5 Delete `MedAssist.Indexer/Database/DbInitializer.cs`
- [ ] 7.6 Remove `Microsoft.Data.Sqlite` package reference from `MedAssist.Indexer.csproj`
- [ ] 7.7 Update `CliCommands.cs` to register and use EF-based repositories; call `MigrateAsync()` at startup

## 8. MedAssist.Web — Auto-migrate at Startup

- [ ] 8.1 In `WebApplicationExtensions.cs` (or `Program.cs`), call `db.Database.MigrateAsync()` after `EnsureModelsReadyAsync()`

## 9. Cleanup and Validation

- [ ] 9.1 Remove `Database:Path` from all config files and any remaining references in code
- [ ] 9.2 Add `postgres`, `Npgsql`, `npgsql`, `dbcontext`, `DbContext` to `cspell.json`
- [ ] 9.3 Build solution — 0 errors, 0 warnings
- [ ] 9.4 Run test suite — all 8 tests pass
- [ ] 9.5 Verify `grep -r "Microsoft.Data.Sqlite" --include="*.csproj"` returns no results
