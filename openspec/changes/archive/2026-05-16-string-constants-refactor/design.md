## Context

Four projects (`MedAssist.AI`, `MedAssist.Data`, `MedAssist.Indexer`, `MedAssist.Web`) use raw string literals for two families of values:

1. **ONNX tensor/file names** — `"input_ids"`, `"attention_mask"`, `"token_type_ids"`, `"last_hidden_state"`, `"logits"`, `"model.onnx"`, `"tokenizer.json"`, and three more model file names. These appear identically in `MultilingualE5Embedder`, `CrossEncoderReranker`, and `ModelInitializer`.

2. **Ingestion status strings** — `"pending"`, `"in_progress"`, `"indexed"`, `"complete"` span entity defaults, EF `HasDefaultValue` calls, a repository `.Where` filter, and a bookmark guard in `BookIndexer`.

`MedAssist.Shared` already has `VectorStoreConstants` and `LanguageCodes` as the established home for cross-project string constants, using nested static classes for grouping.

## Goals / Non-Goals

**Goals:**
- Single source of truth for every ONNX tensor name and model file name
- Single source of truth for every ingestion status string value
- Zero behavior change — pure substitution of literals with constant references

**Non-Goals:**
- Changing the `BookStatus` enum or how `BookRepository` serializes it
- Modifying EF migration files (generated artifacts, not edited manually)
- Extracting `WebSearchPlugin` `"all"` SearXNG locale (not a domain concept)
- Any new functionality or API changes

## Decisions

### Decision 1: Place all constants in `MedAssist.Shared`

**Chosen:** `MedAssist.Shared/Constants/` alongside `VectorStoreConstants` and `LanguageCodes`.

**Alternative considered:** Keep ONNX constants internal to `MedAssist.AI` (they are AI implementation details). Rejected because the codebase already uses `MedAssist.Shared` as the single constants location and splitting the pattern adds friction with no benefit at current scale.

### Decision 2: Mirror `VectorStoreConstants` nesting style for `OnnxConstants`

`OnnxConstants` uses nested static classes (`Inputs`, `Outputs`, `Files`) matching the `VectorStoreConstants.Vectors` / `VectorStoreConstants.Payload` pattern already in the codebase.

### Decision 3: E5 embedding prefixes stay private

`"query: "` and `"passage: "` are only used inside `MultilingualE5Embedder`. They are not ONNX protocol — they are E5 model-specific prompt formatting. Private `const` fields in the class are the right scope.

### Decision 4: `IngestionStatus` strings are independent of `BookStatus` enum

`BookRepository` serializes `BookStatus` enum as `ToString().ToLowerInvariant()`. The status column values in `books` (`"pending"`, `"inprogress"`, `"indexed"`) are produced from the enum, not from `IngestionStatus`.

`IngestionStatus` constants are used directly in:
- Entity property initializers and EF `HasDefaultValue`
- `BookIndexer.SaveCheckpointAsync` string argument (checkpoint table)
- `BookCatalogService` LINQ `.Where` filter (books table, value `"indexed"`)

The `"indexed"` value is shared between the two families: `BookStatus.Indexed.ToString().ToLowerInvariant() == IngestionStatus.Indexed`. This is an implicit contract that must be maintained if `BookStatus` enum member names change.

## Risks / Trade-offs

- **Enum/constant divergence** — If `BookStatus.Indexed` is ever renamed, `IngestionStatus.Indexed` must be updated in sync. No compiler enforcement exists. → Mitigation: document the constraint in `IngestionStatus.cs` with an inline comment.

- **`IngestionStatus.Complete` is currently dead code** — `BookIndexer` guards on `checkpoint?.Status == "complete"` but no code path ever writes `"complete"` to the checkpoint. Replacing with the constant is still correct and makes the dead guard visible. → No mitigation needed; the constant is valid.
