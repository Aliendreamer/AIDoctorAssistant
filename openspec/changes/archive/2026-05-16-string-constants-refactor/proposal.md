## Why

Hardcoded string literals for ONNX tensor names, model file names, and ingestion status values are duplicated across four projects with no single source of truth, making them silently error-prone to typo and invisible to refactoring tools.

## What Changes

- Add `OnnxConstants` static class in `MedAssist.Shared.Constants` with nested `Inputs`, `Outputs`, and `Files` groups covering all ONNX tensor and file name strings
- Add `IngestionStatus` static class in `MedAssist.Shared.Constants` with canonical string values for book and checkpoint status columns
- Extend existing `LanguageCodes` with `EnglishName` and `BulgarianName` display-name constants
- Add private `QueryPrefix`/`PassagePrefix` consts inside `MultilingualE5Embedder` (E5-specific, not shared)
- Replace all raw string literals in `MultilingualE5Embedder`, `CrossEncoderReranker`, `ModelInitializer`, `RagPluginBase`, `BookEntity`, `IngestionCheckpointEntity`, two EF configurations, `BookIndexer`, and `BookCatalogService`

## Capabilities

### New Capabilities

- `onnx-constants`: Typed constants for ONNX tensor input/output names and model file names used by the embedding and reranker ONNX sessions
- `ingestion-status-constants`: Canonical string constants for book and checkpoint status column values shared across Data, Indexer, and Web layers

### Modified Capabilities

- `private-field-naming`: `LanguageCodes` gains two display-name entries (`EnglishName`, `BulgarianName`) — existing short-code values unchanged

## Impact

- `MedAssist.Shared` — 2 new files, 1 modified
- `MedAssist.AI` — 3 files updated (Embedder, Reranker, ModelInitializer, RagPluginBase)
- `MedAssist.Data` — 4 files updated (2 entities, 2 EF configurations)
- `MedAssist.Indexer` — 1 file updated (BookIndexer)
- `MedAssist.Web` — 1 file updated (BookCatalogService)
- No schema changes, no API changes, no behavior changes — pure substitution
