## Context

`QueryService.ExecuteAsync` builds a fresh `ChatHistory` for every request — system prompt, RAG context, user question — with no awareness of prior turns. The Blazor `Query.razor` page renders a single answer box. User identity is available as `ClaimTypes.Name` (username string) from cookie auth, accessible in Blazor Server components via `AuthenticationStateProvider`.

## Goals / Non-Goals

**Goals:**
- Persist conversation turns (user question + assistant answer only — not RAG excerpts) to PostgreSQL per username per query type
- Inject last 10 turns into `ChatHistory` on every query so the model has conversational context
- Show conversation thread in the UI above the input form
- Clear history per query type via UI button and DELETE API endpoint

**Non-Goals:**
- Cross-query-type context (Disease history does not bleed into Treatment thread)
- Storing or re-injecting RAG chunk excerpts from prior turns
- Pagination of history in the UI (show last 10 turns only)
- Full audit/export of history

## Decisions

### D1: `user_id` is the username string (not a foreign key to `users`)

`ClaimTypes.Name` carries the username. Using it directly avoids a join and keeps the repository simple. The `users` table stores app users but is managed separately; a FK would add coupling and a migration dependency. Username is stable for the life of a session.

### D2: `query_type` stored as a lowercase string column, not a PG enum

`QueryType` is a C# enum (`Disease`, `Symptoms`, `Treatment`). Storing as `text` (`"disease"`, `"symptoms"`, `"treatment"`) avoids a PG enum migration and keeps `ChatHistoryRepository` free of EF enum mapping complexity. A check constraint enforces valid values.

### D3: Pass `userId` explicitly to `QueryService.ExecuteAsync`

`QueryService` is a scoped service with no HTTP context dependency today. Passing `userId` as a parameter keeps it testable and avoids injecting `IHttpContextAccessor` (which would break Blazor Server's server-side execution model). The Blazor component reads the username from `AuthenticationStateProvider` and passes it in.

### D4: History stored after successful response only

User message + assistant answer are saved only after the LLM returns successfully. A failed or canceled query does not pollute history. This matches user expectation — only completed exchanges appear in the thread.

### D5: `ClearChatHistoryEndpoint` is a DELETE endpoint, hard delete

Hard delete is appropriate — the UI presents it as "Clear chat". Soft delete would complicate the load query without user benefit. The button confirms intent clearly enough.

## Risks / Trade-offs

- **Long conversation context increases token count** — 10 turns × ~200 tokens each = ~2000 extra tokens per query. Within the 4096 context window this is tight alongside RAG excerpts. Mitigated: inject history turns *before* RAG context so if truncation occurs, older turns are dropped first by the model's context window. Can tune `MaxHistoryTurns` down to 5 if needed.
- **Username as user_id** — if a username is renamed or deleted and recreated, history from the old account would surface. Acceptable for this system since usernames are admin-managed and rare to change.

## Migration Plan

1. Add `ChatMessageEntity` + `ChatHistoryRepository` to `MedAssist.Data`
2. Add `ChatMessages` DbSet to `MedAssistDbContext`
3. Generate EF migration `AddChatMessages`
4. Update `QueryService.ExecuteAsync` signature to accept `userId`; add history read/write
5. Add `ClearChatHistoryEndpoint`
6. Update `Query.razor`: inject auth state, pass userId, render thread, add clear button
7. Rebuild and redeploy web container
