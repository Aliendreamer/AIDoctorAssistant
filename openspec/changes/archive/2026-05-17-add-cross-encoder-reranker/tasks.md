## 1. Interface and Model Scaffolding

- [x] 1.1 Add `ICrossEncoderReranker` interface to `MedAssist.Shared/Interfaces/`
- [x] 1.2 Create `MedAssist.AI/Reranker/` folder and `CrossEncoderReranker.cs` stub
- [x] 1.3 Verify `Microsoft.ML.OnnxRuntime` is already referenced in `MedAssist.AI.csproj`; add if missing

## 2. Model Download

- [x] 2.1 Add reranker model files to `ModelInitializer._modelFiles` array (model.onnx, tokenizer.json, tokenizer_config.json, special_tokens_map.json from `cross-encoder/ms-marco-MiniLM-L-6-v2`)
- [x] 2.2 Add `Models:RerankerPath` to `config/appsettings.shared.json` (default: `models/ms-marco-MiniLM-L-6-v2`)

## 3. Cross-Encoder Implementation

- [x] 3.1 Implement tokenizer input construction: `[CLS] query [SEP] passage [SEP]`, truncate to 512 tokens
- [x] 3.2 Implement ONNX inference session loading from `Models:RerankerPath`
- [x] 3.3 Implement `RerankAsync`: score all candidates in parallel (`Parallel.ForEachAsync`), sort descending by score, return

## 4. Pipeline Integration

- [x] 4.1 Inject `ICrossEncoderReranker` into `RagPluginBase` constructor
- [x] 4.2 Replace `DistinctBy(...).Take(10)` in `ExecuteSearchAsync` with reranker call + `.Take(5)`
- [x] 4.3 Pass original user query (not expanded terms) to `RerankAsync`

## 5. Dependency Injection

- [x] 5.1 Register `CrossEncoderReranker` as `ICrossEncoderReranker` singleton in `MedAssist.AI` DI extensions

## 6. Housekeeping

- [x] 6.1 Add reranker-related words to `cspell.json` (`reranker`, `reranking`, `reranked`, `MiniLM`, `minilm`, `mmarco`, `marco`)
- [x] 6.2 Build solution — 0 errors, 0 warnings
- [x] 6.3 Run test suite — all 8 tests pass
