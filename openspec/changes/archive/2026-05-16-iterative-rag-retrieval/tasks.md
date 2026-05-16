## 1. Shared Models

- [x] 1.1 Add `ScoredChunk` readonly record struct to `MedAssist.Shared/Models/ScoredChunk.cs`
- [x] 1.2 Add `RagOptions` class to `MedAssist.Shared/Models/RagOptions.cs` with `ConfidenceThreshold` and `MaxIterations`

## 2. Reranker Interface & Implementation

- [x] 2.1 Update `ICrossEncoderReranker.RerankAsync` return type to `IReadOnlyList<ScoredChunk>`
- [x] 2.2 Update `CrossEncoderReranker.RerankAsync` to return scored results ordered by descending logit

## 3. Query Expansion

- [x] 3.1 Add `ExtractKeywords` private static method to `MedicalDictionaryService` (stopword filter + length guard)
- [x] 3.2 Update `ExpandQueryAsync` to add extracted keywords as independent search terms before dictionary lookup

## 4. Iterative Retrieval Loop

- [x] 4.1 Add `RagOptions` field and constructor parameter to `RagPluginBase`
- [x] 4.2 Define `RetryStrategy` sealed record and the 5-entry `_strategies` static array in `RagPluginBase`
- [x] 4.3 Extract `GatherCandidatesAsync` helper (embed + vectorize + search per term)
- [x] 4.4 Extract `SelectTerms` helper (LongestOnly / all terms)
- [x] 4.5 Implement the confidence-gated loop in `ExecuteSearchAsync` with early exit and `Math.Min(MaxIterations, 5)` cap
- [x] 4.6 Extract `BuildResult` helper (top-5 selection, answer string, source citations)

## 5. Plugin & Kernel Wiring

- [x] 5.1 Add `RagOptions options` parameter to `SymptomsPlugin`, `DiseasePlugin`, `TreatmentPlugin` constructors
- [x] 5.2 Add `RagOptions options` parameter to `KernelFactory.Build` and pass to all three plugins

## 6. Configuration & DI

- [x] 6.1 Bind `Rag:ConfidenceThreshold` and `Rag:MaxIterations` in `ServiceCollectionExtensions.AddAiServices` with cap enforcement
- [x] 6.2 Add `Rag` section to `config/appsettings.shared.json` with default values

## 7. Tests

- [x] 7.1 Write `RagIterativeLoopTests` covering: high confidence stops early, low confidence iterates, MaxIterations cap, zero iterations, empty store, top-5 cap, answer format
- [x] 7.2 Verify all 15 tests pass (8 sparse vectorizer + 7 new iterative loop tests)
