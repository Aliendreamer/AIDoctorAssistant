## 1. EF Core

- [x] 1.1 `AsNoTracking()` on read-only queries (MedicalDictionaryService ×4, BookCatalogService, BookRepository GetAll/GetByBookId, ChatHistoryRepository GetRecent, UserRepository FindByUsername/List); left load-to-mutate queries tracked

## 2. Qdrant

- [x] 2.1 Payload indexes: keyword on BookId/Language/ChapterTitle/SectionTitle + bool on IsSummary (idempotent, back-fills existing collection)
- [x] 2.2 `IVectorStore.UpsertBatchAsync` (default per-point fallback) + `ChunkVector`; `QdrantVectorStore` real batch override; `BookIndexer` batches Pass B + summaries at checkpoint boundaries
- [x] 2.3 int8 scalar quantization (codes AlwaysRam) + on-disk originals + search rescoring (2× oversampling); applied at creation and back-filled onto the existing collection

## 3. .NET

- [x] 3.1 Value-tuple `DistinctBy` keys (CandidateRetriever, RagPluginBase) — alloc-free, collision-proof
- [x] 3.2 `MedicalDictionaryService` stopwords → `FrozenSet`

## 4. Verify

- [x] 4.1 `dotnet build MedAssist.slnx` — 0 warnings/0 errors
- [x] 4.2 `dotnet test MedAssist.Tests` — 163 pass
- [x] 4.3 Live query returns cited answers (no behavior regression)
