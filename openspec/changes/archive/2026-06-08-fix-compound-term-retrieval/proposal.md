## Why

Splitting multi-word medical queries into individual keywords causes retrieval to match on the wrong anatomical domain. "малкомозъчните тонзили" (cerebellar tonsils) splits into "тонзили" which retrieves ENT/tonsillectomy content instead of neurological content — producing a completely incorrect answer.

## What Changes

- `RagPluginBase.ExecuteSearchAsync`: initial `GatherCandidatesAsync` call uses only the full query, not individual expanded keywords
- `MedicalDictionaryService.ExpandQueryAsync`: still expands via dictionary, but per-keyword sub-searches are moved to the iterative fallback loop only
- Iterative retry strategies already widen progressively — individual keywords become a later-stage fallback, not the first pass

## Capabilities

### New Capabilities

- `compound-term-retrieval`: Query expansion that preserves multi-word phrase integrity during initial retrieval, falling back to per-keyword search only when full-query confidence is insufficient

### Modified Capabilities

- `rag-query-expansion`: The expansion terms are no longer searched in the initial gather pass; they are reserved for iterative fallback iterations

## Impact

- `MedAssist.AI/Plugins/RagPluginBase.cs` — change how `expandedTerms` are used in `ExecuteSearchAsync`
- `MedAssist.AI/Dictionary/MedicalDictionaryService.cs` — `ExpandQueryAsync` return value usage changes (no code change needed here, change is in the caller)
- No API or schema changes
- Re-index not required
