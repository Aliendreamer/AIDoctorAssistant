## Why

Dense vector search alone suffers from the "vocabulary mismatch" problem — when a physician queries "синдром на Даун" the embedder may not surface passages using slightly different terminology, abbreviations, or ICD codes. BM25 sparse retrieval excels at exact-term matching and compensates precisely where dense search is weak; combining both (hybrid search) produces measurably better recall for medical RAG workloads where precise terminology is critical.

## What Changes

- Add a **sparse BM25 vector** field to the `medical_books` Qdrant collection alongside the existing dense vector field.
- Update `BookIndexer` to compute and store a sparse BM25 vector for each chunk at index time.
- Update `QdrantVectorStore.SearchAsync` to issue a **hybrid query** (dense + sparse, fused via RRF) instead of a pure dense query.
- Extend `IVectorStore` and `IEmbedder` contracts to support sparse vector generation.
- Expose a `SparseVectorizer` service (TF-IDF/BM25 over the corpus vocabulary) in `MedAssist.AI`.
- Collection schema change requires a **one-time re-index** of all books — document in migration notes.

## Capabilities

### New Capabilities

- `hybrid-retrieval`: Combined dense + sparse (BM25) Qdrant search using Reciprocal Rank Fusion; replaces pure dense `SearchAsync`.
- `sparse-vectorizer`: BM25 sparse vector computation for passages and queries; builds and persists vocabulary from indexed corpus.

### Modified Capabilities

- `vector-store`: `SearchAsync` signature gains sparse query vector parameter; `UpsertAsync` gains sparse vector parameter.

## Impact

- **`MedAssist.AI/VectorStore/QdrantVectorStore.cs`** — hybrid query replaces single-vector search; `UpsertAsync` stores both dense + sparse named vectors.
- **`MedAssist.AI/Embedding/`** — new `SparseVectorizer.cs` class; `IEmbedder` interface unchanged (sparse is separate concern).
- **`MedAssist.Shared/Interfaces/IVectorStore.cs`** — `UpsertAsync` and `SearchAsync` gain `float[]? sparseVector` / `SparseVector? sparseQueryVector` parameters.
- **`MedAssist.Indexer/Ingestion/BookIndexer.cs`** — calls `SparseVectorizer` per chunk at index time.
- **`MedAssist.Web/Services/QueryService.cs`** — passes sparse query vector through to `IVectorStore`.
- **Qdrant collection** — named vector config change requires collection recreation; existing indexed books must be re-indexed.
- **Dependencies** — no new NuGet packages needed; Qdrant.Client 1.18.1 supports named vectors and sparse search natively.
