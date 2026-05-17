## 1. Strip Base64 Images in MarkdownChunker

- [x] 1.1 Add a `StripInlineImages` static method to `MarkdownChunker` using a compiled regex that matches `![...](<data:...;base64,...>)` and removes the entire match
- [x] 1.2 Call `StripInlineImages` at the top of `Chunk()` before line-by-line parsing

## 2. New BM25 Stats Entity and EF Config

- [x] 2.1 Create `MedAssist.Data/Entities/Bm25StatsEntity.cs` with properties `Id`, `TotalDocuments`, `UpdatedAt`
- [x] 2.2 Create `MedAssist.Data/Configuration/Bm25StatsEntityConfiguration.cs` mapping to `bm25_stats` table
- [x] 2.3 Add `DbSet<Bm25StatsEntity> Bm25Stats` to `MedAssistDbContext`
- [x] 2.4 Remove `TotalDocuments` property from `Bm25VocabEntity` and its column mapping from `Bm25VocabEntityConfiguration`

## 3. EF Core Migration

- [x] 3.1 Run `dotnet ef migrations add CleanBm25VocabAndSeparateTotalDocs --project MedAssist.Data --startup-project MedAssist.Web` on host
- [x] 3.2 Edit the generated migration `Up()` to add `migrationBuilder.Sql("TRUNCATE TABLE bm25_vocab;")` after creating `bm25_stats` and before any other data changes
- [x] 3.3 Verify the migration `Down()` drops `bm25_stats` and re-adds `total_documents` to `bm25_vocab`

## 4. Update BM25VocabService

- [x] 4.1 Update `LoadAsync` to read `total_documents` from `bm25_stats` (id=1) instead of `MAX(total_documents)` from `bm25_vocab`
- [x] 4.2 Update `GetTotalDocumentsAsync` to read from `bm25_stats`
- [x] 4.3 Update `UpsertTermsAsync`: replace `ExecuteUpdateAsync` on all vocab rows with a single upsert on `bm25_stats` (id=1); remove `TotalDocuments` assignment from vocab entity updates/inserts

## 5. Update Force Re-index Path

- [x] 5.1 In `TriggerIndexEndpoint`, when `force=true`: truncate `bm25_vocab` via `ExecuteDeleteAsync` and delete/reset the `bm25_stats` row using EF Core

## 6. Build and Deploy

- [x] 6.1 Run `dotnet build` on host — confirm zero errors
- [x] 6.2 Ask user to rebuild and restart the web container
- [x] 6.3 Confirm migrations applied: check `bm25_stats` table exists and `bm25_vocab` has no `total_documents` column

## 7. Re-index and Verify

- [ ] 7.1 Trigger force re-index: `POST /api/admin/index?id=1&force=true`
- [ ] 7.2 Confirm `bm25_vocab` row count is < 100K after re-index (vs 1.58M before)
- [ ] 7.3 Confirm `bm25_stats.total_documents` = 844 after re-index completes
