# dictionary-icd-lookup Specification

## Purpose
TBD - created by archiving change audit-remediation. Update Purpose after archive.
## Requirements
### Requirement: ICD lookup uses an EF-translatable query

`GetByIcdAsync` SHALL query by ICD code using a database-translatable comparison (case-normalized
equality such as `IcdCode == icdUpper` or `EF.Functions.ILike`). It SHALL NOT use the
`string.Equals(string, StringComparison)` overload, which EF Core cannot translate and which
throws at execution time.

#### Scenario: Existing ICD code returns the illness

- **WHEN** `GET /api/dictionary/{icd}` is called with an ICD code present in the dictionary
- **THEN** the endpoint returns 200 with the matching illness (no `InvalidOperationException`)

#### Scenario: Case-insensitive match

- **WHEN** the request supplies the ICD code in lower or mixed case
- **THEN** it resolves to the same illness as the canonical upper-case form

#### Scenario: Verified against a real provider

- **WHEN** the lookup is tested
- **THEN** the test runs against a real relational provider (not the EF InMemory provider, which
  would not surface the translation failure)

