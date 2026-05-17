## Context

`MedAssist` currently indexes medical book chunks as single dense 1024-dim cosine vectors in a Qdrant `medical_books` collection. The `QdrantVectorStore.SearchAsync` issues a pure nearest-neighbour query. Medical text is terminology-dense and bilingual (EN + BG), making it susceptible to vocabulary mismatch: a query phrased differently from the indexed passage may score poorly even when semantically equivalent.

Qdrant ≥ 1.7 supports **named vectors** (multiple vector fields per point) and **sparse vectors** with built-in BM25 scoring, enabling hybrid queries that fuse dense and sparse rankings.

## Goals / Non-Goals

**Goals:**
- Store both a dense and a sparse (BM25) vector per chunk in Qdrant using named vectors.
- Issue hybrid queries (dense NN + sparse BM25) at search time, fused with Reciprocal Rank Fusion (RRF).
- Keep `IVectorStore` and `IEmbedder` interfaces backward-compatible except for explicit sparse parameters.
- Provide a `SparseVectorizer` that computes BM25 sparse vectors from raw text at index time and query time.
- Produce a corpus vocabulary that is persisted to SQLite so it survives restarts and can be extended incrementally.

**Non-Goals:**
- Online (incremental) vocabulary updates after initial corpus build — vocabulary is rebuilt on re-index.
- Neural sparse models (SPLADE, etc.) — pure BM25 is sufficient and requires no extra model download.
- Changing the embedding model or dense vector dimensions.
- Exposing BM25-only search as a standalone query path.

## Decisions

### D1: BM25 implementation — in-process vs. Qdrant built-in sparse indexing

**Decision:** Compute BM25 sparse vectors in-process (C# `SparseVectorizer`) and store as Qdrant sparse named vectors.

**Rationale:** Qdrant's sparse vector field type (introduced in Qdrant 1.7) accepts user-supplied sparse vectors (term-id → weight pairs) and indexes them with an inverted index for fast dot-product retrieval. Computing BM25 in-process means we control tokenisation (important for Cyrillic), IDF from our own corpus, and vocabulary growth. We do not depend on a sidecar service.

**Alternative considered:** Use Qdrant's `FastEmbed` BM25 model endpoint. Rejected because it requires network round-trips per chunk and does not support custom Cyrillic tokenisation.

---

### D2: Vocabulary and IDF storage

**Decision:** Persist vocabulary (term → term_id, df) to the SQLite `medassist.db` as a `bm25_vocab` table. Load into memory at Indexer startup; Web service loads read-only at startup.

**Rationale:** Vocabulary must survive process restart and be shared between Indexer (writes) and Web (reads). SQLite WAL mode is already in use for this pattern.

**Alternative considered:** Flat file (JSON/binary). Rejected — adds another file dependency; SQLite WAL concurrency is already proven in this project.

---

### D3: Fusion strategy — RRF vs. linear score combination

**Decision:** Use Qdrant's built-in **RRF (Reciprocal Rank Fusion)** for hybrid query fusion.

**Rationale:** RRF is rank-based (not score-based) so it is insensitive to score scale differences between cosine similarity and BM25 dot products. Qdrant's `hybrid` query type with `rrf` fusion is a single API call with no extra tuning parameters.

**Alternative considered:** Linear combination (`alpha * dense_score + (1-alpha) * bm25_score`). Rejected — requires manual alpha tuning per domain and is sensitive to score normalisation.

---

### D4: Named vector field names

**Decision:** `"dense"` for the existing 1024-dim cosine field, `"sparse"` for the new BM25 sparse field.

**Rationale:** Explicit names avoid positional ambiguity. Current collection uses a default (unnamed) vector; migration requires collection recreation.

---

### D5: Collection migration strategy

**Decision:** Delete and recreate the `medical_books` collection with the new named-vector schema on first Indexer run after upgrade. Provide a CLI flag `--recreate-collection` to trigger explicitly.

**Rationale:** Qdrant does not support adding a new named vector field to an existing collection without recreating it. Since books are re-indexed from markdown sources (deterministic), re-indexing is the correct migration path. The flag prevents accidental wipe in production.

---

### D6: IVectorStore interface change

**Decision:** Add overloads — keep existing signatures for backward compatibility, add new methods with sparse parameters:
```csharp
Task UpsertAsync(MedicalChunk chunk, float[] denseVector, SparseVector sparseVector, ...);
Task<IReadOnlyList<MedicalChunk>> SearchAsync(float[] denseQuery, SparseVector sparseQuery, LanguageFilter, IReadOnlyList<string>? bookIds, int topK, ...);
```
The old signatures call through with `null` sparse and fall back to dense-only.

**Rationale:** Avoids breaking the Web service or tests before they are updated to supply sparse vectors.

## Risks / Trade-offs

- **Re-index required** → Mitigation: Document clearly; `--recreate-collection` flag makes intent explicit; indexing is resumable via checkpoints.
- **Vocabulary cold start** — first query before any book is indexed returns empty sparse vector → Mitigation: SparseVectorizer returns empty sparse vector (no terms match), falling back to dense-only effectively.
- **Memory for vocabulary** — large corpus could yield >1M terms → Mitigation: Minimum document frequency threshold (df ≥ 2) prunes hapax legomena; typical medical vocabulary is bounded.
- **Cyrillic tokenisation** — BG text must tokenise correctly → Mitigation: Use Unicode word-boundary tokeniser (`\p{L}+` regex) which handles Cyrillic natively; apply same lowercasing as query side.

## Migration Plan

1. Upgrade: deploy new `MedAssist.Indexer` binary.
2. Run `indexer index --recreate-collection --book <file> ...` for each book (order does not matter).
3. Vocabulary is built incrementally per book; IDF is recomputed after all books are indexed via `indexer rebuild-vocab` command.
4. Web service picks up new collection on next start (no code change required for read path once sparse query is plumbed).
5. Rollback: delete `medical_books` collection, redeploy old Indexer, re-index — no persistent state beyond SQLite and Qdrant.

## Open Questions

- Should `alpha` weighting be exposed as config even with RRF? (Deferred — RRF has no alpha, but a future dense-vs-sparse score-blend mode may be wanted.)
- Should vocabulary be language-partitioned (separate IDF tables for EN vs BG)? (Deferred — unified vocab is simpler; language filter already narrows Qdrant results.)
