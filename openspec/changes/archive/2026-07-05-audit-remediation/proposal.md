## Why

A full static audit of the solution (five parallel review dimensions — security, architecture,
correctness, performance, quality/tests — see `audit-report.md` in this change) surfaced
**4 Critical (P0)** and **14 High (P1)** issues, several cross-confirmed by more than one
dimension. Two are outright dangerous:

- A single-book `?force=true` re-index **deletes the entire vector store and all BM25 vocab**
  for every book, while logging that it acted "for {BookId}" (silent catastrophic data loss).
- The JWT signing secret is a **hardcoded, self-describing placeholder that is actually in use**,
  and it ships in the Docker image over plaintext HTTP alongside a documented default admin
  password — a trivial full-admin takeover path.

Alongside these, the ICD dictionary lookup endpoint **throws on every request** (an
EF-untranslatable query), the first book indexed silently loses its sparse vectors, re-indexing
duplicates all points, and the ingestion pipeline runs as fire-and-forget `Task.Run` inside HTTP
handlers (lost on restart, unbounded concurrency).

This change is the remediation plan. It is organized by severity so the dangerous items land
first, and it deliberately does **not** re-open the many verified-correct areas the audit cleared
(PBKDF2 hashing, parameterized SQL, endpoint authorization, DI lifetimes, E5 pooling — see the
"Verified NON-issues" section of the report).

## What Changes

### P0 — Critical (ship first)
- **Scope destructive re-index to one book.** `force=true` deletes only the target book's Qdrant
  points (payload `BookId` filter) and only that book's BM25 contribution — never
  `DeleteCollectionAsync` or a global `bm25_vocab`/`bm25_stats` truncate.
- **Remove the hardcoded JWT secret from use.** Load the signing key from a secret source; add a
  startup guard that refuses to boot in Production with the placeholder or a <32-byte key.
- **Bootstrap a strong admin credential.** Generate a random admin password on first seed (or
  require it via secret), stop committing plaintext passwords, and decouple the admin UI from
  seed credentials.
- **Fix `GetByIcdAsync`.** Replace the EF-untranslatable `string.Equals(…, StringComparison)`
  with a translatable comparison; add a real-provider test.

### P1 — High
- **Book upload/index safety:** allowlist-validate `BookId` (`^[a-z0-9][a-z0-9-]{0,63}$`) and
  resolve+contain all derived paths; validate PDF magic bytes and a realistic size cap.
- **Transport security:** enforce TLS + `UseHttpsRedirection()`; auth cookie `SecurePolicy=Always`,
  `SameSite=Lax`, `HttpOnly=true`.
- **Secrets out of the image:** stop `COPY config/` into the image; mount config at runtime; add a
  `.dockerignore` for `config/`; rotate leaked secrets.
- **SSRF guard:** enforce the domain allowlist and block private/loopback/link-local resolved IPs
  on the actual URL fetched (not just as a `site:` hint); reject cross-host redirects.
- **Ingestion correctness:** two-phase index so BM25 vocab exists before vectors are written
  (no empty-sparse first book); deterministic Qdrant point ids (UUIDv5 from `bookId:chunkIndex`)
  so re-index overwrites instead of duplicating; atomically mark `InProgress` to close the
  concurrent-trigger race.
- **Durable background ingestion:** move OCR/index/extract off fire-and-forget `Task.Run` into a
  `Channel<T>` + `BackgroundService` that honors `ApplicationStopping`; persist extraction state.
- **Hot-path performance:** singleton/cached BM25 vocab snapshot with invalidation; hoist the
  illness list out of the per-chunk enrichment loop (single inverted term→ICD map); rerank query
  encoded once + score only new chunks per retry + batched inference.
- **Resilience & observability:** wrap the RAG path in error handling with an injected logger and
  user-safe result; add timeout+retry to the Ollama client; emit `indexer_chunks_total`, register
  the AI meter, and add real `ActivitySource` spans (or drop the dead `AddSource`).
- **Admin UI boundary:** call services in-process from Blazor instead of self-calling the REST API
  over loopback with config credentials.

### P2 / P3
Tracked in `tasks.md` (medium: repository consolidation + interfaces, AI↔DbContext decoupling,
duplicate DI registrations, prompt-injection fencing, PHI-log redaction, rate limiting, upload
hardening, missing indexes, etc.; low: chunk-merge heading bug, generated regex, README/CLAUDE.md
drift, stale specs). These land after P0/P1 and are grouped, not individually gated here.

## Capabilities

### Added Capabilities
- `reindex-data-safety` — destructive re-index is scoped to a single book
- `book-upload-validation` — `BookId` allowlisting + path containment + PDF validation
- `web-search-ssrf-guard` — allowlist + private-IP enforcement on the fetched URL
- `secret-and-transport-security` — JWT secret guard, TLS/secure-cookie enforcement, no secrets in image
- `background-ingestion` — durable queue + hosted service for OCR/index/extract
- `pipeline-observability` — indexer metric + custom RAG spans + AI meter registration

### Modified Capabilities
- `dictionary-icd-lookup` — `GetByIcdAsync` uses a translatable query (endpoint no longer 500s)
- `hybrid-search-ingestion` — two-phase vocab-before-vectors; deterministic point ids; atomic status

### Removed Capabilities
- None

## Impact

- **Web/API:** `Program.cs`, `Extensions/ServiceCollectionExtensions.cs`,
  `Extensions/WebApplicationExtensions.cs`, `Endpoints/Books/*`, `Endpoints/Dictionary/*`,
  `Services/QueryService.cs`, `Services/AdminApiClient.cs`, `Services/AdminBookService.cs`,
  `Components/Pages/Query.razor`, `Startup/UserSeeder.cs`
- **AI:** `VectorStore/QdrantVectorStore.cs`, `Ingestion/BookIndexer.cs`,
  `Ingestion/ChunkEnricher.cs`, `Ingestion/VocabularyBuilder.cs`, `Ingestion/MarkerClient.cs`,
  `Embedding/SparseVectorizer.cs`, `Dictionary/MedicalDictionaryService.cs`,
  `Dictionary/BM25VocabService.cs`, `Reranker/CrossEncoderReranker.cs`, `Kernel/KernelFactory.cs`,
  `Plugins/WebSearchPlugin.cs`, `Plugins/RagPluginBase.cs`
- **Data:** `Configurations/IllnessEntityConfiguration.cs` (+ a new migration for indexes),
  repository interfaces
- **Infra/config:** `MedAssist.Web/Dockerfile`, `.dockerignore`, `docker-compose.yml`,
  `config/appsettings.shared.json` (secrets removed), `CLAUDE.md`, `README.md`
- **Tests:** new coverage for auth/JWT/password, `GetByIcdAsync` (real Postgres), reindex safety,
  BookId validation, SSRF guard, `QdrantVectorStore`, `BookIndexer` resume, reranker budgeting
- **Specs:** `openspec/specs/ingestion-status-constants` and `openspec/specs/private-field-naming`
  are stale/contradictory and are corrected or retired
