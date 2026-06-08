## 1. Core Fix

- [x] 1.1 In `RagPluginBase.ExecuteSearchAsync`, change the initial `GatherCandidatesAsync` call to pass `[query]` instead of `expandedTerms`
- [x] 1.2 Verify the retry loop iterations still receive the full `expandedTerms` list (no change needed — confirm it's already correct)

## 2. Tests

- [x] 2.1 Add test: compound query "малкомозъчните тонзили" — assert initial candidates do NOT contain ENT/tonsil chunks
- [x] 2.2 Add test: single-word query "тонзилит" — assert behaviour is unchanged (single term, same in both phases)
- [x] 2.3 Update any existing `RagIterativeLoopTests` that assert `expandedTerms` is used in the first pass

## 3. Verification

- [x] 3.1 Run all tests — `dotnet test`
- [x] 3.2 Manual smoke test: query "Арнолд-Киари малформация" in the UI — verify result is about cerebellar herniation, not tonsillectomy
- [x] 3.3 Manual smoke test: query "тонзилит" — verify tonsil content still returned correctly
