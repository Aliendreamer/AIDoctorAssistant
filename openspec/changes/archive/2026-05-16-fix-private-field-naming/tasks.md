## 1. Fix editorconfig naming rule

- [x] 1.1 Add a `private_constants` naming symbol with `applicable_modifiers = const` and a `pascal_case_style` (no required prefix) to `.editorconfig`, placed before the existing `private_fields` rule so it takes precedence

## 2. Rename private static readonly fields

- [x] 2.1 In `MedAssist.AI/Embedding/ModelInitializer.cs`: rename `ModelFiles` → `_modelFiles` and update all usages in the same file
- [x] 2.2 In `MedAssist.Web/Services/QueryService.cs`: rename `Meter` → `_meter`, `QueryDuration` → `_queryDuration`, `QdrantResults` → `_qdrantResults` and update all usages in the same file

## 3. Fix whitespace violations

- [x] 3.1 Run `dotnet format MedAssist.slnx` (without `--verify-no-changes`) to auto-fix the three WHITESPACE errors in `ChunkEnricher.cs`, `IllnessDictionaryRepository.cs`, and `QueryService.cs`

## 4. Verify

- [x] 4.1 Run `dotnet build MedAssist.slnx --nologo` and confirm 0 errors, 0 warnings
- [x] 4.2 Run `dotnet test MedAssist.Tests --nologo` and confirm all 8 tests pass
- [x] 4.3 Run `dotnet format MedAssist.slnx --verify-no-changes --diagnostics IDE1006` and confirm exit code 0
