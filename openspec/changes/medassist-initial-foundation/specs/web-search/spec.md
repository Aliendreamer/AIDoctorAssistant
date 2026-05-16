## ADDED Requirements

### Requirement: Web search is opt-in per query session
The system SHALL NOT perform web searches by default. Web search SHALL only execute when the user explicitly enables it via a toggle in the UI for that query. The enabled state SHALL NOT persist across page navigations.

#### Scenario: Web search disabled by default
- **WHEN** a query is submitted without enabling web search
- **THEN** only book results are returned; no external HTTP calls are made

#### Scenario: User enables web search for a query
- **WHEN** user toggles "Search web if needed" and submits a query
- **THEN** web search is permitted as a fallback if book results are below threshold

### Requirement: PubMed is the primary web search source
The `WebSearchPlugin` SHALL query the PubMed E-utilities API as the primary source. The plugin SHALL construct a PubMed search query from the user's input and return up to 5 results with title, abstract snippet, PMID, and URL.

#### Scenario: PubMed returns results for a medical query
- **WHEN** web search is enabled and PubMed is queried for a recognised medical term
- **THEN** at least one result with PMID and abstract is returned

#### Scenario: PubMed unavailable falls back gracefully
- **WHEN** PubMed API is unreachable
- **THEN** web search returns empty results with an error message; book results are still displayed

### Requirement: Web results are visually distinguished from book results
In the UI, web search results SHALL be displayed in a separate section labeled "Web Sources" and SHALL include a source badge (e.g., "PubMed") and the full URL. Book results SHALL remain in a separate "Book Sources" section.

#### Scenario: Mixed results display correct source labels
- **WHEN** both book and web results are returned
- **THEN** book results show book title + page citation; web results show "PubMed" badge + URL

### Requirement: WebSearchPlugin is a Semantic Kernel plugin
`WebSearchPlugin` SHALL be a SK plugin class in `MedAssist.AI`, registered on the kernel only when web search is enabled for the request. It SHALL accept `query` (string) and `language` (`"en"` | `"bg"` | `"both"`) parameters.

#### Scenario: Plugin not registered when web search disabled
- **WHEN** web search is disabled for the request
- **THEN** `WebSearchPlugin` is not present in the kernel's plugin collection for that request
