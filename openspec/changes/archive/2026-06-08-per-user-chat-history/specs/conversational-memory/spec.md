## ADDED Requirements

### Requirement: Chat messages persisted per user per query type

The system SHALL store each completed query exchange (user question + assistant answer) in a `chat_messages` PostgreSQL table keyed by `user_id` (username) and `query_type` (lowercase string: "disease", "symptoms", "treatment").

#### Scenario: Successful query persists both turns

- **WHEN** a query completes successfully
- **THEN** a "user" role message (the question) and an "assistant" role message (the answer) are both inserted into `chat_messages` with the correct `user_id` and `query_type`

#### Scenario: Failed query does not persist

- **WHEN** a query throws an exception or is canceled
- **THEN** no messages are written to `chat_messages`

#### Scenario: Messages are ordered by creation time

- **WHEN** history is loaded for a user and query type
- **THEN** messages are returned in ascending `created_at` order

### Requirement: Prior turns injected into LLM context

Before executing a query, the system SHALL load the last 10 messages (5 exchanges) for the current user and query type and prepend them to the `ChatHistory` after the system prompt and before the current RAG context.

#### Scenario: Prior turns appear before RAG context

- **WHEN** conversation history exists for a user
- **THEN** the `ChatHistory` passed to the LLM contains: [system prompt] → [prior turns in order] → [RAG context + current question]

#### Scenario: No history on first query

- **WHEN** a user has no prior messages for a query type
- **THEN** no prior turns are injected and the query executes as before

#### Scenario: History is scoped to query type

- **WHEN** a user has Disease history but submits a Symptoms query
- **THEN** no Disease history turns are injected into the Symptoms request
