## ADDED Requirements

### Requirement: Query input accepts free-text clinical questions
The UI SHALL provide a text area for entering clinical queries. The input SHALL accept both Latin (English) and Cyrillic (Bulgarian) characters. A submit button SHALL trigger the query.

#### Scenario: User submits a Bulgarian query
- **WHEN** user types a query in Bulgarian and clicks Submit
- **THEN** the query is sent to the appropriate SK plugin and results are displayed

### Requirement: Query type selector routes to correct plugin
The UI SHALL display a selector for query type with three options: "Symptoms" (routes to SymptomsPlugin), "Disease Info" (routes to DiseasePlugin), "Treatment" (routes to TreatmentPlugin). Default selection SHALL be "Disease Info".

#### Scenario: Symptoms type routes to SymptomsPlugin
- **WHEN** user selects "Symptoms" and submits a query
- **THEN** SymptomsPlugin is invoked with the query

### Requirement: Language filter controls search scope
The UI SHALL display a language filter with options: "Both" (default), "English only", "Bulgarian only". The selected value SHALL be passed as the `language` parameter to the SK plugin.

#### Scenario: Bulgarian only filter excludes English chunks
- **WHEN** user selects "Bulgarian only" and submits
- **THEN** returned results contain only chunks with `language: "bg"`

### Requirement: Book filter allows scoping to specific books
The UI SHALL display a multi-select list of available books populated from the `books` SQLite table. When no books are selected, search covers all indexed books. When one or more books are selected, only those books are searched.

#### Scenario: Book filter scopes search to selected book
- **WHEN** user selects one book from the list and submits
- **THEN** results come only from that book

#### Scenario: No book selected searches all books
- **WHEN** no books are selected in the filter
- **THEN** results may include chunks from any indexed book

### Requirement: Results display citations with source information
Each result SHALL display: the answer text, and below it a citation block showing book title, author, chapter, section, and page range (for book results) or source name and URL (for web results).

#### Scenario: Book result shows page citation
- **WHEN** a book result is displayed
- **THEN** citation shows book title, author, and page numbers

### Requirement: Web search toggle is visible but disabled by default
The UI SHALL show a "Search web if needed" toggle, OFF by default. When toggled ON, an informational note SHALL appear: "Results may include PubMed sources."

#### Scenario: Toggle defaults to off on page load
- **WHEN** the page loads
- **THEN** web search toggle is in the OFF state
