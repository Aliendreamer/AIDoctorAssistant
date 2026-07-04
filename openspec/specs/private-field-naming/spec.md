# private-field-naming Specification

## Purpose
Enforce one consistent private-field naming convention across the solution via `.editorconfig`, and
provide the language display-name constants used by language parsing. Corrected to match the current
`.editorconfig`, which applies `_camelCase` to **all** private fields including `const` (the earlier
"private const uses PascalCase" rule was removed).

## Requirements

### Requirement: All private fields use _camelCase (including const)
The `.editorconfig` SHALL define a single naming rule applying to every `private` field —
instance, `static readonly`, and `const` alike — requiring a leading underscore and camelCase
(`_camelCase`). There SHALL be no separate PascalCase rule for `private const`.

#### Scenario: Private const field uses _camelCase
- **WHEN** a `private const` field is declared (e.g. `_checkpointInterval` in `BookIndexer`, `_k1` in `SparseVectorizer`)
- **THEN** it SHALL be named `_camelCase` and `dotnet build` SHALL report no naming warning

#### Scenario: Private static readonly field uses _camelCase
- **WHEN** a `private static readonly` field is declared (e.g. `_meter`)
- **THEN** it SHALL be named `_camelCase`

#### Scenario: Non-prefixed private field is flagged
- **WHEN** a `private` field is declared without a leading underscore
- **THEN** the `.editorconfig` naming rule SHALL report a violation (an error under `TreatWarningsAsErrors`)

### Requirement: Solution builds clean under the naming rule
The solution SHALL build with 0 warnings and 0 errors. Because `TreatWarningsAsErrors` is enabled, any naming-rule violation fails the build, so a green build is proof of solution-wide compliance.

#### Scenario: Clean build
- **WHEN** `dotnet build MedAssist.slnx` is executed
- **THEN** it SHALL report 0 warnings and 0 errors

### Requirement: LanguageCodes exposes display-name constants
`MedAssist.Shared.Constants.LanguageCodes` SHALL expose `EnglishName` (`"english"`) and
`BulgarianName` (`"bulgarian"`) alongside the short-code constants `English` (`"en"`) and
`Bulgarian` (`"bg"`).

#### Scenario: RagPluginBase language parsing uses the constants
- **WHEN** `RagPluginBase.ParseLanguage` evaluates a language string
- **THEN** the english/bulgarian arms SHALL reference `LanguageCodes.EnglishName`/`LanguageCodes.BulgarianName` (alongside the short codes `English`/`Bulgarian`)
