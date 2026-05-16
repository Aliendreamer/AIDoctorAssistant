## ADDED Requirements

### Requirement: Three distinct Semantic Kernel plugins handle query types
`MedAssist.AI` SHALL expose three SK plugins registered on the kernel: `SymptomsPlugin` (symptoms → likely causes/diagnoses), `DiseasePlugin` (disease name → clinical information), `TreatmentPlugin` (condition → treatment options). Each plugin SHALL be a separate class decorated with `[KernelFunction]`.

#### Scenario: SymptomsPlugin returns relevant results
- **WHEN** `SymptomsPlugin` is invoked with a list of symptoms
- **THEN** the plugin returns ranked results from Qdrant with source citations

#### Scenario: DiseasePlugin scopes to requested books
- **WHEN** `DiseasePlugin` is invoked with `bookIds: ["litvinenko-pediatrics"]`
- **THEN** only vectors from that book are included in results

### Requirement: All plugins accept language and book scope filters
Each plugin function SHALL accept parameters: `query` (string, required), `language` (`"both"` | `"en"` | `"bg"`, default `"both"`), `bookIds` (string array, optional — null means search all books).

#### Scenario: Language filter restricts results to Bulgarian
- **WHEN** a plugin is invoked with `language: "bg"`
- **THEN** all returned chunks have `language: "bg"` in their Qdrant payload

#### Scenario: No book filter returns results from all books
- **WHEN** a plugin is invoked with `bookIds: null`
- **THEN** results may include chunks from any indexed book

### Requirement: Query expansion uses ICD-10 dictionary
Before executing a Qdrant search, each plugin SHALL look up the query terms in the `illnesses` table. If a match is found, the query SHALL be expanded to include both BG and EN names plus aliases from that illness record.

#### Scenario: English query matches Bulgarian chunks via dictionary expansion
- **WHEN** query is `"Down Syndrome"` with `language: "both"`
- **THEN** Qdrant search includes `"Синдром на Down"` as an additional query term

### Requirement: AI model backend is configurable without code changes
`MedAssist.AI` SHALL read model provider configuration from `appsettings.json` key `AI:ModelProvider` (`ollama` | `azure-openai`). Switching provider SHALL require only a config change and restart, not code changes.

#### Scenario: Ollama provider connects to local Qwen model
- **WHEN** `AI:ModelProvider` is `ollama` and Ollama service is running
- **THEN** SK kernel uses Ollama chat completion with the configured model name

### Requirement: Query results include citations
Every result returned by a plugin SHALL include citation fields: `bookTitle`, `author`, `chapterTitle`, `sectionTitle`, `pageStart`, `pageEnd` (for book results). Citations SHALL be included in the SK function return value.

#### Scenario: Result contains book citation
- **WHEN** a plugin returns a result from an indexed book
- **THEN** the result includes non-empty `bookTitle` and `pageStart`
