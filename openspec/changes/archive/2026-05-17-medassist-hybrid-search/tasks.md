## 1. SQLite Vocabulary Schema

- [x] 1.1 Add `bm25_vocab` table to `DbInitializer.cs` (columns: id, term TEXT UNIQUE, term_id INTEGER, document_frequency INTEGER, total_documents INTEGER, updated_at TEXT)
- [x] 1.2 Create `Repositories/BM25VocabRepository.cs` in `MedAssist.Indexer` with methods: `LoadAllAsync`, `UpsertTermsAsync(IEnumerable<(string term, int df)> terms, int totalDocs)`, `GetTotalDocumentsAsync`

## 2. SparseVector Model

- [x] 2.1 Create `MedAssist.Shared/Models/SparseVector.cs` — record with `IReadOnlyDictionary<uint, float> Entries` and static `Empty` instance
- [x] 2.2 Update `MedAssist.Shared/Interfaces/IVectorStore.cs` — add overloads: `UpsertAsync(..., SparseVector sparseVector, ...)` and `SearchAsync(..., SparseVector? sparseQuery, ...)`; keep existing signatures delegating to new ones with `SparseVector.Empty`

## 3. SparseVectorizer (MedAssist.AI)

- [x] 3.1 Create `MedAssist.AI/Embedding/SparseVectorizer.cs` — constructor takes `IBM25VocabStore` (interface below); exposes `VectorizePassage(string text): SparseVector` and `VectorizeQuery(string text): SparseVector`
- [x] 3.2 Implement Unicode word-boundary tokeniser in `SparseVectorizer` using `Regex.Matches(text.ToLowerInvariant(), @"\p{L}+")` 
- [x] 3.3 Implement BM25 weight formula: `idf(t) * (tf(t,d) * (k1+1)) / (tf(t,d) + k1 * (1 - b + b * |d|/avgdl))` with k1=1.5, b=0 (no length normalisation)
- [x] 3.4 Create `MedAssist.Shared/Interfaces/ISparseVectorizer.cs` with `VectorizePassage` and `VectorizeQuery` method signatures
- [x] 3.5 Register `SparseVectorizer` as singleton in both `MedAssist.Web/Program.cs` and `MedAssist.Indexer/Program.cs`

## 4. Vocabulary Builder (MedAssist.Indexer)

- [x] 4.1 Create `Ingestion/VocabularyBuilder.cs` — accumulates term frequencies across all chunks of a book, then calls `BM25VocabRepository.UpsertTermsAsync` after full book is indexed
- [x] 4.2 Integrate `VocabularyBuilder` into `BookIndexer.cs` — call `AddChunk(text)` per chunk, call `FlushAsync()` after all chunks of a book are processed
- [x] 4.3 Add minimum df threshold (df ≥ 2) filter in `BM25VocabRepository.UpsertTermsAsync` — discard terms with document_frequency < 2 after update
- [x] 4.4 Add CLI command `rebuild-vocab` to `MedAssist.Indexer/Program.cs` — re-scans all indexed book chunks from Qdrant payloads and rebuilds `bm25_vocab` from scratch

## 5. Qdrant Named Vector Migration

- [x] 5.1 Update `QdrantVectorStore.cs` — change collection creation to use named vectors config: `"dense"` (size=1024, cosine) and `"sparse"` (sparse vector index)
- [x] 5.2 Update `QdrantVectorStore.UpsertAsync` — accept `SparseVector sparseVector` parameter; store point with both `"dense"` and `"sparse"` named vector entries
- [x] 5.3 Update `QdrantVectorStore.SearchAsync` — when `sparseQuery` is non-null, issue a `QueryRequest` with `Prefetch` (dense NN on `"dense"`) + `Prefetch` (sparse on `"sparse"`) and `Query = new FusionQuery(Fusion.Rrf)`; fall back to single dense search when null
- [x] 5.4 Add `--recreate-collection` flag to the `index` CLI command — when set, deletes and recreates the `medical_books` collection before indexing

## 6. Indexer Hybrid Wiring

- [x] 6.1 Update `BookIndexer.cs` — call `ISparseVectorizer.VectorizePassage(chunk.Text)` per chunk alongside existing `IEmbedder.EmbedPassageAsync`; pass both vectors to `IVectorStore.UpsertAsync`
- [x] 6.2 Update `MedAssist.Indexer/Program.cs` — instantiate and inject `SparseVectorizer` (load vocabulary from `BM25VocabRepository` at startup) and `VocabularyBuilder`

## 7. Web Query Hybrid Wiring

- [x] 7.1 Update `MedAssist.Web/Services/QueryService.cs` — call `ISparseVectorizer.VectorizeQuery(expandedQuery)` after dense embedding; pass sparse vector to `IVectorStore.SearchAsync`
- [x] 7.2 Update `MedAssist.Web/Program.cs` — register `ISparseVectorizer` singleton (loads vocabulary read-only from SQLite at startup)

## 8. Plugin Base Update

- [x] 8.1 Update `MedAssist.AI/Plugins/RagPluginBase.cs` — inject `ISparseVectorizer`; call `VectorizeQuery` on the expanded query string; pass to `IVectorStore.SearchAsync`

## 9. Verification

- [x] 9.1 Run `dotnet build MedAssist.slnx` — verify zero warnings and zero errors
- [x] 9.2 Write unit test `SparseVectorizerTests.cs` in `MedAssist.Tests` — verify Cyrillic tokenisation, BM25 weight non-zero for known terms, empty vector for empty input
- [x] 9.3 Smoke-test: run Indexer with `--recreate-collection` against one markdown book, verify Qdrant collection has `"dense"` and `"sparse"` named vectors on a point
- [x] 9.4 Smoke-test: run a query through the Web service, confirm hybrid search returns results (check logs for Qdrant response payload)
