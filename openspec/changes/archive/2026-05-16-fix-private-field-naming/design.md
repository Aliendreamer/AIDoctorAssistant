## Context

The `.editorconfig` defines a `private_fields` naming symbol with `applicable_kinds = field` and no `applicable_modifiers` filter, so the `_camelCase` rule fires on `private const` declarations. Constants in .NET are conventionally PascalCase without a prefix (`MaxTokens`, `HfBase`, not `_maxTokens`). The violations are caught only by `dotnet format`, not `dotnet build`, because `EnforceCodeStyleInBuild` only enforces warnings promoted to errors — and naming rules remain warnings.

**Violation inventory (15 unique, across 9 files):**

| File | Fields | Kind |
|---|---|---|
| `ModelInitializer.cs` | `HfBase`, `ModelFiles` | const, static readonly |
| `WebSearchPlugin.cs` | `ESearchBase`, `EFetchBase` | const |
| `SharedConfigurationExtensions.cs` | `ConfigPathEnvVar`, `SharedFileName` | const |
| `SparseVectorizer.cs` | `K1` | const |
| `MultilingualE5Embedder.cs` | `MaxTokens` | const |
| `QdrantVectorStore.cs` | `VectorSize` | const |
| `BookIndexer.cs` | `CheckpointInterval` | const |
| `MarkdownChunker.cs` | `MaxTokens`, `MinTokens` | const |
| `DbInitializer.cs` | `Schema` | const |
| `QueryService.cs` | `Meter`, `QueryDuration`, `QdrantResults` | static readonly |

## Goals / Non-Goals

**Goals:**
- Zero IDE1006 errors in `dotnet format --verify-no-changes --diagnostics IDE1006`
- `private const` fields exempt from `_` prefix rule (PascalCase stays as-is)
- `private static readonly` fields renamed to `_camelCase` with all usages updated
- Whitespace violations fixed in the same pass

**Non-Goals:**
- Changing access modifiers, making constants internal/public
- Renaming parameters, locals, or properties
- Enforcing the naming rule as a build error (stays as warning)

## Decisions

**Fix the rule, not the constants.**
Add a higher-specificity editorconfig rule for `private const` using `applicable_modifiers = const`. Editorconfig resolves ambiguity by preferring the more specific rule (more modifiers specified = more specific). This exempts constants from the `_camelCase` rule without touching any source files for constants — zero diff noise, zero risk of breaking references.

Alternative considered: rename all constants to `_camelCase` (e.g., `K1` → `_k1`). Rejected — nonstandard for .NET, makes constants look like mutable instance state.

**Rename only `static readonly` fields.**
`ModelFiles`, `Meter`, `QueryDuration`, `QdrantResults` are genuinely variable-like (not compile-time constants) and should follow the `_camelCase` rule. These are used only within their declaring class so the rename scope is narrow.

**Fix whitespace in the same PR.**
Three whitespace violations came from the same `dotnet format` run. Fixing them separately adds churn; bundling them keeps the format check clean in one shot.

## Risks / Trade-offs

- [Risk] Renaming `_meter`, `_queryDuration`, `_qdrantResults` in `QueryService` while the static fields are initialised in the declaration — callers chain off the renamed field. Mitigation: all usages are in the same file; a simple find-replace within the file covers them.
- [Risk] editorconfig specificity rules are poorly documented and may behave differently across Roslyn versions. Mitigation: verify with `dotnet format --verify-no-changes` after the change.
