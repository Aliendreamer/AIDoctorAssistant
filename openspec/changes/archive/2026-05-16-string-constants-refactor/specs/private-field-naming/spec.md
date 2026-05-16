## ADDED Requirements

### Requirement: LanguageCodes exposes display-name constants
`MedAssist.Shared.Constants.LanguageCodes` SHALL add `EnglishName` (`"english"`) and `BulgarianName` (`"bulgarian"`) alongside the existing short-code constants `English` (`"en"`) and `Bulgarian` (`"bg"`).

#### Scenario: RagPluginBase switch arms use display-name constants
- **WHEN** `RagPluginBase.ParseLanguage` evaluates a language string
- **THEN** the `"english"` and `"bulgarian"` switch arms SHALL reference `LanguageCodes.EnglishName` and `LanguageCodes.BulgarianName` respectively
