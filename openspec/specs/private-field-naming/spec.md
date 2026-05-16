# private-field-naming Specification

## Purpose
TBD - created by archiving change fix-private-field-naming. Update Purpose after archive.
## Requirements
### Requirement: Private const fields use PascalCase without underscore prefix
The `.editorconfig` SHALL define a naming rule for `private const` fields that requires PascalCase capitalization and no required prefix, taking precedence over the general private-field `_camelCase` rule.

#### Scenario: Constant field exempted from underscore rule
- **WHEN** a `private const` field is declared with PascalCase (e.g., `MaxTokens`, `HfBase`)
- **THEN** `dotnet format --diagnostics IDE1006` SHALL report no violation for that field

#### Scenario: Non-constant private field still requires underscore
- **WHEN** a `private readonly` or `private static readonly` field is declared without `_` prefix
- **THEN** `dotnet format --diagnostics IDE1006` SHALL report an IDE1006 violation

### Requirement: Private static readonly fields use _camelCase
All `private static readonly` fields in the solution SHALL be named with a leading `_` and camelCase (e.g., `_modelFiles`, `_meter`).

#### Scenario: Static readonly field renamed with _ prefix
- **WHEN** a previously non-prefixed `private static readonly` field is renamed to `_camelCase`
- **THEN** all usages within the declaring class compile without error
- **THEN** `dotnet build` reports 0 errors and 0 warnings

### Requirement: Zero IDE1006 violations solution-wide
Running `dotnet format --verify-no-changes --diagnostics IDE1006` on the full solution SHALL exit with code 0.

#### Scenario: Clean format check
- **WHEN** `dotnet format MedAssist.slnx --verify-no-changes --diagnostics IDE1006` is executed
- **THEN** exit code is 0 and no IDE1006 errors are printed

### Requirement: Zero whitespace format violations
Running `dotnet format --verify-no-changes` (without `--diagnostics` filter) SHALL report no WHITESPACE errors in `ChunkEnricher.cs`, `IllnessDictionaryRepository.cs`, or `QueryService.cs`.

#### Scenario: Whitespace violations resolved
- **WHEN** `dotnet format MedAssist.slnx --verify-no-changes` is executed
- **THEN** no WHITESPACE errors appear for the three previously-affected files

