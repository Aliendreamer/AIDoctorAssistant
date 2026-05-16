## Why

The `.editorconfig` naming rule requires a `_` prefix on all private fields, but currently applies to `private const` fields too — which should be PascalCase by .NET convention. This produces IDE1006 violations in 9 files across all four projects and prevents a clean `dotnet format --verify-no-changes` run, which blocks enforcing the rule in CI.

## What Changes

- Tighten `.editorconfig`: add a higher-specificity rule that carves out `private const` fields as PascalCase (no prefix), overriding the general `_camelCase` rule for constants
- Rename `private static readonly` fields that genuinely violate the rule (`ModelFiles`, `Meter`, `QueryDuration`, `QdrantResults`)
- Fix 3 incidental whitespace violations surfaced by the same `dotnet format` run

## Capabilities

### New Capabilities
- `private-field-naming`: Naming convention correctness for private fields — editorconfig rules, affected field renames, and zero IDE1006 violations under `dotnet format`

### Modified Capabilities

## Impact

- `.editorconfig` — new constant naming rule added
- `MedAssist.AI/Embedding/ModelInitializer.cs` — `ModelFiles` → `_modelFiles`
- `MedAssist.Web/Services/QueryService.cs` — `Meter` → `_meter`, `QueryDuration` → `_queryDuration`, `QdrantResults` → `_qdrantResults` (all usages in same file)
- `MedAssist.Indexer/Ingestion/ChunkEnricher.cs` — whitespace fix line 27
- `MedAssist.Indexer/Repositories/IllnessDictionaryRepository.cs` — whitespace fix line 110
- No public API changes, no test impact
