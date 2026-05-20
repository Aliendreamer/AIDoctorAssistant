## Why

Each query is currently stateless — the model has no memory of what was discussed moments ago. A physician asking follow-up questions must re-state all context every time. Storing conversation history per user per query type and injecting it into each request gives MedAssist genuine conversational memory, matching the experience of talking to a knowledgeable colleague.

## What Changes

- New `chat_messages` PostgreSQL table: stores user and assistant turns linked to user ID and query type
- `QueryService`: loads last 10 turns before executing, injects them into `ChatHistory`, saves the new turn after execution
- New `ChatHistoryRepository`: data access for reading, writing, and clearing chat messages
- New EF Core migration: `AddChatMessages`
- Blazor `Query.razor` page: shows the conversation thread above the input form; adds a "Clear history" button per query type
- New `ClearChatHistoryEndpoint`: FastEndpoints DELETE endpoint to clear a user's history for a given query type

## Capabilities

### New Capabilities

- `conversational-memory`: Per-user, per-query-type conversation history stored in PostgreSQL and injected into LLM context on each query
- `chat-history-management`: API and UI to view and clear conversation history per query type

### Modified Capabilities

- `rag-query-expansion`: Query execution now receives prior conversation turns as additional context alongside RAG excerpts

## Impact

- `MedAssist.Data`: new `ChatMessageEntity`, `ChatHistoryRepository`, EF migration
- `MedAssist.Web/Services/QueryService`: reads/writes history, injects turns into `ChatHistory`
- `MedAssist.Web/Endpoints/`: new `ClearChatHistoryEndpoint`
- `MedAssist.Web/Components/Pages/Query.razor`: conversation display + clear button
- `MedAssist.Shared/Models/`: new `ChatMessage` and `QueryType` already exists
- No changes to vector store, embedder, or reranker
