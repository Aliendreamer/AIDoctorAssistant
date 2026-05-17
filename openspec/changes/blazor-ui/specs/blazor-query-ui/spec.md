## ADDED Requirements

### Requirement: Query page accessible to Doctor and Admin roles
The `/query` page SHALL require authentication and SHALL be accessible to users with either the Doctor or Admin role. Users without a valid session SHALL be redirected to `/login`.

#### Scenario: Doctor accesses query page
- **WHEN** a user with the Doctor role navigates to `/query`
- **THEN** the query page renders with the query form

#### Scenario: Admin accesses query page
- **WHEN** a user with the Admin role navigates to `/query`
- **THEN** the query page renders with the query form

### Requirement: Language selector
The query form SHALL include a language selector with options for English and Bulgarian. The selected language SHALL be passed to the RAG query as the `language` parameter.

#### Scenario: Select Bulgarian language
- **WHEN** the user selects "Bulgarian" from the language dropdown
- **THEN** the query is submitted with `language=bg`

#### Scenario: Default language is English
- **WHEN** the query page first renders
- **THEN** the language selector defaults to "English"

### Requirement: Query type selector
The query form SHALL include a query type selector. Supported types are "Book Search" (RAG only) and "Book Search + Web" (RAG with web search fallback). The selected type SHALL control whether the web-search plugin is enabled in the kernel invocation.

#### Scenario: Book search only
- **WHEN** the user selects "Book Search" and submits
- **THEN** the query runs without web-search fallback

#### Scenario: Book + web search
- **WHEN** the user selects "Book Search + Web" and submits
- **THEN** the query runs with web-search enabled

### Requirement: Book filter checkboxes
The query form SHALL display a checkbox list of all indexed books (title and language). The user MAY select a subset to restrict search to those books. If no books are checked, the search covers all indexed books.

#### Scenario: Filter to one book
- **WHEN** the user checks exactly one book and submits
- **THEN** the query is sent with `bookIds` containing only that book's ID

#### Scenario: No filter — all books searched
- **WHEN** no book checkbox is checked and the user submits
- **THEN** the query is sent with no `bookIds` filter

### Requirement: Answer display with source citations
After a query completes, the page SHALL display the answer text and a collapsible "Sources" section listing each citation (book title, author, chapter, section, page range).

#### Scenario: Answer with sources
- **WHEN** the RAG pipeline returns an answer with source citations
- **THEN** the page shows the answer text followed by a "Sources" section listing each citation

#### Scenario: No results
- **WHEN** the RAG pipeline returns "No relevant information found"
- **THEN** the page shows that message with no sources section

### Requirement: Navigation — public area
The public nav SHALL display the MedAssist brand name, a "Query" link to `/query`, and a "Logout" link. Doctor-role users SHALL NOT see admin links.

#### Scenario: Doctor nav
- **WHEN** a Doctor-role user is logged in
- **THEN** the nav shows "Query" and "Logout" but no "Admin" or "Books" links

#### Scenario: Admin nav includes admin section
- **WHEN** an Admin-role user is logged in
- **THEN** the nav shows "Query", "Books" (under an admin section), and "Logout"
