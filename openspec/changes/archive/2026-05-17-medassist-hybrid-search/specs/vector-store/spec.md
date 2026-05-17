## MODIFIED Requirements

### Requirement: Upsert stores both dense and sparse named vectors
The system SHALL store each chunk as a Qdrant point with two named vector fields: `"dense"` (1024-dim float32 cosine) and `"sparse"` (BM25 sparse). The legacy unnamed vector field SHALL be removed.

#### Scenario: Upsert with both vectors succeeds
- **WHEN** `UpsertAsync` is called with a valid dense vector and a non-empty sparse vector
- **THEN** the Qdrant point is stored with both `"dense"` and `"sparse"` named vector fields and all payload fields intact

#### Scenario: Collection auto-created with named vector config
- **WHEN** `UpsertAsync` is called and the `medical_books` collection does not exist
- **THEN** the collection is created with named vectors: `"dense"` (size=1024, distance=Cosine) and `"sparse"` (sparse index type)

### Requirement: Search issues hybrid named-vector query
The system SHALL issue a Qdrant hybrid query referencing the `"dense"` named vector for the NN component and the `"sparse"` named vector for the BM25 component.

#### Scenario: Hybrid query returns top-K results
- **WHEN** `SearchAsync` is called with both dense and sparse query vectors
- **THEN** Qdrant's hybrid search with RRF fusion returns up to `topK` scored results

#### Scenario: Dense-only fallback when sparse is null
- **WHEN** `SearchAsync` is called with `sparseQueryVector == null`
- **THEN** only the `"dense"` named vector search is executed (no sparse component)
