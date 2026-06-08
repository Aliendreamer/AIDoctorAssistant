## ADDED Requirements

### Requirement: Clear history endpoint

The system SHALL expose `DELETE /api/chat/history/{queryType}` requiring authentication. It SHALL hard-delete all `chat_messages` rows for the authenticated user and the specified query type.

#### Scenario: Authenticated user clears their history

- **WHEN** an authenticated user calls `DELETE /api/chat/history/disease`
- **THEN** all chat_messages rows for that user and query type "disease" are deleted and 200 OK is returned

#### Scenario: Unauthenticated request is rejected

- **WHEN** an unauthenticated request hits the clear endpoint
- **THEN** 401 Unauthorized is returned and no data is deleted

#### Scenario: Unknown query type returns 400

- **WHEN** the query type path segment is not one of disease/symptoms/treatment
- **THEN** 400 Bad Request is returned

### Requirement: Conversation thread displayed in UI

The Blazor Query page SHALL display the conversation history for the current user and active query type above the input form. Each message SHALL show the role ("You" / "MedAssist") and content.

#### Scenario: Thread updates after each query

- **WHEN** a query completes
- **THEN** the new user question and assistant answer appear at the bottom of the conversation thread without a page reload

#### Scenario: Clear button removes thread from UI

- **WHEN** the user clicks "Clear history" for the active query type
- **THEN** the conversation thread is cleared in the UI and the history is deleted via the API

#### Scenario: Thread is empty on first visit

- **WHEN** a user visits the query page with no prior history
- **THEN** no conversation thread is shown and only the input form is visible
