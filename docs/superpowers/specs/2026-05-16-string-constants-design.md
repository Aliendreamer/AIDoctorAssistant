# String Constants Refactoring

**Date:** 2026-05-16
**Scope:** Replace hardcoded string literals across MedAssist.AI, MedAssist.Data, MedAssist.Indexer, and MedAssist.Web with named constants in MedAssist.Shared.

---

## Problem

Two families of raw string literals are scattered across multiple projects with no single source of truth:

1. **ONNX tensor/file names** — `"input_ids"`, `"attention_mask"`, `"token_type_ids"`, `"last_hidden_state"`, `"logits"`, `"model.onnx"`, `"tokenizer.json"`, etc. appear identically in both `MultilingualE5Embedder` and `CrossEncoderReranker`, with the file names also repeated in `ModelInitializer`.

2. **Ingestion status strings** — `"pending"`, `"in_progress"`, `"indexed"`, `"complete"` are used as raw literals in entity defaults, EF configurations, repository queries, and business logic across four projects.

A third minor case: language display names `"english"` and `"bulgarian"` appear as raw strings in `RagPluginBase` alongside the `LanguageCodes` constants that already hold the short codes.

---

## Design

### Placement

All new constants go in `MedAssist.Shared/Constants/`, following the `VectorStoreConstants` / `LanguageCodes` precedent. The ONNX constants are AI implementation details, but placing them in Shared gives them the same discoverability as the existing constants and avoids a separate internal-constants pattern in `MedAssist.AI`.

---

### New file 1: `MedAssist.Shared/Constants/OnnxConstants.cs`

Mirrors the `VectorStoreConstants` nesting style.

```csharp
namespace MedAssist.Shared.Constants;

public static class OnnxConstants
{
    public static class Inputs
    {
        public const string InputIds = "input_ids";
        public const string AttentionMask = "attention_mask";
        public const string TokenTypeIds = "token_type_ids";
    }

    public static class Outputs
    {
        public const string LastHiddenState = "last_hidden_state";
        public const string Logits = "logits";
    }

    public static class Files
    {
        public const string ModelOnnx = "model.onnx";
        public const string ModelOnnxData = "model.onnx_data";
        public const string TokenizerJson = "tokenizer.json";
        public const string TokenizerConfigJson = "tokenizer_config.json";
        public const string SpecialTokensMapJson = "special_tokens_map.json";
    }
}
```

---

### New file 2: `MedAssist.Shared/Constants/IngestionStatus.cs`

Canonical string values for book and checkpoint status columns.

```csharp
namespace MedAssist.Shared.Constants;

public static class IngestionStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Indexed = "indexed";
    public const string Complete = "complete";
}
```

**Constraint:** `BookRepository.UpsertAsync` writes status as `BookStatus.ToString().ToLowerInvariant()`. The `BookStatus` enum member names must therefore remain consistent with these constants:

| Enum value | `.ToString().ToLowerInvariant()` | Must equal |
|---|---|---|
| `BookStatus.Pending` | `"pending"` | `IngestionStatus.Pending` |
| `BookStatus.InProgress` | `"inprogress"` | ⚠ does NOT equal `IngestionStatus.InProgress` (`"in_progress"`) |
| `BookStatus.Indexed` | `"indexed"` | `IngestionStatus.Indexed` |

> **Note:** `BookStatus.InProgress.ToString().ToLowerInvariant()` produces `"inprogress"` (no underscore), but the DB column stores `"in_progress"` (with underscore) set via `IngestionCheckpointEntity` defaults and `SaveCheckpointAsync`. These are two different columns on two different tables — `books.status` and `ingestion_checkpoints.status` — so there is no actual mismatch in practice. The checkpoint status is never round-tripped through `BookStatus`. `BookCatalogService` only filters `books.status == "indexed"` which correctly matches `BookStatus.Indexed.ToString().ToLowerInvariant()`.

---

### Extension: `MedAssist.Shared/Constants/LanguageCodes.cs`

Add display-name constants alongside the existing short codes:

```csharp
public const string EnglishName = "english";
public const string BulgarianName = "bulgarian";
```

---

### Private consts in `MultilingualE5Embedder`

The E5 query/passage prefixes are used only inside `MultilingualE5Embedder`. They do not belong in Shared — keep them as private class-level consts:

```csharp
private const string QueryPrefix = "query: ";
private const string PassagePrefix = "passage: ";
```

---

## Files Changed

| File | Change |
|---|---|
| `MedAssist.Shared/Constants/OnnxConstants.cs` | **New** |
| `MedAssist.Shared/Constants/IngestionStatus.cs` | **New** |
| `MedAssist.Shared/Constants/LanguageCodes.cs` | Add `EnglishName`, `BulgarianName` |
| `MedAssist.AI/Embedding/MultilingualE5Embedder.cs` | `OnnxConstants.Inputs.*`, `.Outputs.LastHiddenState`, `.Files.ModelOnnx`, `.Files.TokenizerJson`; add private `QueryPrefix`/`PassagePrefix` |
| `MedAssist.AI/Reranker/CrossEncoderReranker.cs` | `OnnxConstants.Inputs.*`, `.Outputs.Logits`, `.Files.ModelOnnx`, `.Files.TokenizerJson` |
| `MedAssist.AI/Embedding/ModelInitializer.cs` | `OnnxConstants.Files.*` for all five file names |
| `MedAssist.AI/Plugins/RagPluginBase.cs` | `LanguageCodes.EnglishName`, `.BulgarianName` in switch arms |
| `MedAssist.Data/Entities/BookEntity.cs` | `IngestionStatus.Pending` as default |
| `MedAssist.Data/Entities/IngestionCheckpointEntity.cs` | `IngestionStatus.InProgress` as default |
| `MedAssist.Data/Configuration/BookEntityConfiguration.cs` | `IngestionStatus.Pending` in `HasDefaultValue` |
| `MedAssist.Data/Configuration/IngestionCheckpointEntityConfiguration.cs` | `IngestionStatus.InProgress` in `HasDefaultValue` |
| `MedAssist.Indexer/Ingestion/BookIndexer.cs` | `IngestionStatus.InProgress` in `SaveCheckpointAsync` call; `IngestionStatus.Complete` in resume guard |
| `MedAssist.Web/Services/BookCatalogService.cs` | `IngestionStatus.Indexed` in `.Where` filter |

---

## Out of Scope

- Changing the `BookStatus` enum or how `BookRepository` serializes it — the existing `ToString().ToLowerInvariant()` round-trip works and the tables are independent.
- Migrations — no schema changes, only C# source changes.
- `WebSearchPlugin` language codes `"bg"`, `"en"`, `"all"` — `"bg"` and `"en"` are already covered by `LanguageCodes`; `"all"` is a SearXNG-specific value not represented in the domain model. Leave as-is.
- EF-generated migration files — `Migrations/*.cs` use the same string literals but are generated artifacts; they should not be edited manually.
