## 1. Data Layer

- [x] 1.1 Create `ChatMessageEntity` in `MedAssist.Data/Entities/` with columns: `Id` (serial), `UserId` (varchar), `QueryType` (varchar), `Role` (varchar: "user"/"assistant"), `Content` (text), `CreatedAt` (timestamptz)
- [x] 1.2 Add `DbSet<ChatMessageEntity> ChatMessages` to `MedAssistDbContext`
- [x] 1.3 Add EF entity configuration: table name `chat_messages`, check constraint on `query_type` (disease/symptoms/treatment), index on `(user_id, query_type, created_at)`
- [x] 1.4 Generate EF migration: `dotnet ef migrations add AddChatMessages --project MedAssist.Data --startup-project MedAssist.Web`
- [x] 1.5 Create `ChatHistoryRepository` in `MedAssist.Data/Repositories/` with three methods: `GetRecentAsync(userId, queryType, limit)`, `AddMessageAsync(entity)`, `ClearAsync(userId, queryType)`

## 2. Shared Models

- [x] 2.1 Add `ChatMessageDto` record to `MedAssist.Shared/Models/`: `Role` (string), `Content` (string)

## 3. QueryService

- [x] 3.1 Register `ChatHistoryRepository` as transient in `ServiceCollectionExtensions`
- [x] 3.2 Add `ChatHistoryRepository` constructor parameter to `QueryService`
- [x] 3.3 Add `userId` parameter (nullable string) to `QueryService.ExecuteAsync`
- [x] 3.4 In `ExecuteAsync`: before invoking the plugin, load last 10 messages via `GetRecentAsync`; pass them as `conversationHistory` to `InvokePluginAsync`
- [x] 3.5 In `InvokePluginAsync`: pass `conversationHistory` through to plugin `KernelArguments`
- [x] 3.6 In `RagPluginBase.BuildResultAsync`: inject prior turns into `ChatHistory` after system prompt, before RAG context
- [x] 3.7 In `ExecuteAsync`: after successful result, call `AddMessageAsync` twice (user turn, assistant turn)

## 4. Clear History Endpoint

- [x] 4.1 Create `ClearChatHistoryEndpoint` at `MedAssist.Web/Endpoints/Chat/ClearChatHistoryEndpoint.cs`: `DELETE /api/chat/history/{queryType}`, requires auth roles Admin/Doctor
- [x] 4.2 Validate `queryType` path param is one of disease/symptoms/treatment; return 400 otherwise
- [x] 4.3 Call `ChatHistoryRepository.ClearAsync(User.Identity.Name, queryType)` and return 200

## 5. Blazor Query Page

- [x] 5.1 Inject `AuthenticationStateProvider` into `Query.razor`; resolve `userId` from auth state on init
- [x] 5.2 Load conversation history on page init and on query type change: call a local `LoadHistoryAsync()` that reads recent messages via a new `ChatHistoryService` or directly via the repository (scoped via DI)
- [x] 5.3 Pass `userId` to `QueryService.ExecuteAsync(request, userId)`
- [x] 5.4 After each successful query, append the new user + assistant messages to the local `_history` list (no reload needed)
- [x] 5.5 Render conversation thread above the input form: for each message show role label ("You" / "MedAssist") and content
- [x] 5.6 Add "Clear history" button next to query type selector; on click call `DELETE /api/chat/history/{queryType}` and clear local `_history`

## 6. Verification

- [x] 6.1 Run `dotnet test` — all tests pass
- [x] 6.2 Rebuild and restart web container: `docker compose up -d --build web`
- [x] 6.3 Manual test: submit two related queries — second answer references first
- [x] 6.4 Manual test: clear history — thread disappears, next query has no prior context
- [x] 6.5 Manual test: switch query type — history does not carry over between types
