# MedAssist.AI — Full Code Audit (2026-07-04)

Static audit of the .NET 10 solution across five dimensions (security, architecture,
correctness, performance, quality/tests) by parallel review agents. Every finding was verified
against source with `file:line` citations. Findings are deduped and cross-referenced below;
items flagged by more than one dimension are marked **[cross-confirmed]** (higher confidence).

Severity legend: **P0** = Critical (data loss / full compromise), **P1** = High,
**P2** = Medium, **P3** = Low.

---

## P0 — Critical

| # | Area | Finding | Location |
|---|---|---|---|
| P0-1 | Correctness | **Single-book `?force=true` re-index wipes the ENTIRE vector store + all BM25 vocab.** Calls `DeleteCollectionAsync` (drops all books' Qdrant points) and `ExecuteDeleteAsync` on `bm25_vocab`/`bm25_stats` (global truncate), while logging "for {BookId}". Silent catastrophic data loss for every other book. | `TriggerIndexEndpoint.cs:70-78` → `QdrantVectorStore.cs:150-156` |
| P0-2 | Security | **Hardcoded, self-describing JWT signing secret actually in use** (`"medassist-change-me-in-production-must-be-32-chars!!"`). Nothing overrides it. Anyone with the string forges an Admin JWT → full `/api/admin/*` compromise, no creds. | `config/appsettings.shared.json` (`Auth:Jwt:SecretKey`); `ServiceCollectionExtensions.cs:216`; `LoginEndpoint.cs:45` |
| P0-3 | Security | **Weak default admin credential** (`admin`/`medassist123`) seeded on first boot, documented in README, and reused by the admin UI to self-authenticate — so it can't be rotated without breaking the UI. Bypasses the app's own 8-char minimum. | `UserSeeder.cs:20-32`; `config/appsettings.shared.json`; `AdminApiClient.cs:32-41` |
| P0-4 | Correctness + Quality **[cross-confirmed]** | **`GetByIcdAsync` uses an EF-untranslatable `string.Equals(…, StringComparison)`** inside `FirstOrDefaultAsync` → `InvalidOperationException` at runtime. `GET /api/dictionary/{icd}` is dead for every request. InMemory tests hide it. | `MedicalDictionaryService.cs:67-69` |

## P1 — High

### Security
| # | Finding | Location |
|---|---|---|
| P1-1 | **Path traversal / arbitrary file write** via unsanitized `BookId` (`Path.Combine(pdfPath, $"{BookId}.pdf")`, only validated `NotEmpty`). Same value builds `.md` read paths and is forwarded to Marker as a raw FS path. `BookId="../../.."` escapes the books dir. | `UploadBookEndpoint.cs:552-558`; `UploadBookValidator.cs`; `TriggerIndexEndpoint.cs:51,93`; `MarkerClient.cs:31` |
| P1-2 | **No transport security**: `http` only, no `UseHttpsRedirection()`, auth cookie has no `SecurePolicy`/`SameSite` (ships without `Secure`), `UseHsts()` is a no-op without TLS. Cookie/JWT sniffable/replayable. | `docker-compose.yml`; `ServiceCollectionExtensions.cs:187-198`; `Program.cs` |
| P1-3 | **Secrets baked into the Docker image** (`COPY config/ /config/` copies the decrypted working copy) → JWT secret, admin/doctor passwords, Postgres password recoverable via `docker history`. Defeats git-crypt. | `MedAssist.Web/Dockerfile` |
| P1-4 | **SSRF**: web-search `AllowedDomains` is applied only as a `site:` search hint; the returned URL is fetched with **no validation** → can hit `169.254.169.254`, `qdrant:6334`, `ollama:11434`, other `pcc-net` services; fetched body is fed to the LLM and shown to the user (exfil). | `WebSearchPlugin.cs:864-866`; `QueryService.cs` (`FetchPageSnippetAsync`) |

### Correctness
| # | Finding | Location |
|---|---|---|
| P1-5 | **First book indexed gets empty sparse vectors** — vocab snapshot is built/persisted (`VocabularyBuilder.FlushAsync`) only at the END of `IndexAsync`, after chunks are already upserted; `DocumentFrequency >= 2` load filter compounds it. Hybrid RRF silently degrades to dense-only for early content. | `BookIndexer.cs:97-115`; `SparseVectorizer.cs:27-33,52-69` |
| P1-6 | **Re-index duplicates all points** — point ids are random `Guid.NewGuid()`, and non-force re-index clears the checkpoint but not the vectors, so every upsert is a new point → duplicate content, skewed reranking/citations, doubled storage per run. | `QdrantVectorStore.cs:42`; `BookIndexer.cs:99,207` |
| P1-7 | **Concurrent index triggers on the same book both proceed** — the 409 guard checks `Status == InProgress`, but status is written to `InProgress` only deep inside the background task, leaving a TOCTOU window. | `TriggerIndexEndpoint.cs:62-66`; `BookIndexer.cs:61-70` |

### Architecture / Performance / Observability **[several cross-confirmed]**
| # | Finding | Location |
|---|---|---|
| P1-8 | **Ingestion runs as fire-and-forget `Task.Run` inside HTTP endpoints** (not a hosted service): untracked by the host, dropped on restart mid-write, ignores shutdown, unbounded concurrency. `ExtractionTracker` state is in-memory only. **[arch + quality + correctness]** | `TriggerIndexEndpoint.cs:86-139`; `ExtractBookEndpoint.cs`; `BulkExtractEndpoint.cs`; `ExtractionTracker.cs:16` |
| P1-9 | **BM25 vocab reloaded from Postgres on every query** — `SparseVectorizer` caches the snapshot in an instance field but is registered **Scoped** → full `bm25_vocab` table load + dictionary build per request. **[perf + arch]** | `ServiceCollectionExtensions.cs:78`; `SparseVectorizer.cs:52-69` |
| P1-10 | **`ChunkEnricher` reloads the whole illnesses table + O(illnesses×aliases) substring scan per chunk** — thousands of identical full-table queries per book; the dominant ingestion cost. Also unbounded `Contains` → false-positive ICD codes. **[perf + quality + correctness]** | `ChunkEnricher.cs:11-31`; `BookIndexer.cs:80` |
| P1-11 | **Cross-encoder re-encodes the query and the entire growing candidate pool from scratch every retry iteration**, with batch-size-1 ONNX runs and per-candidate query re-tokenization. | `RagPluginBase.cs:82,112-117`; `CrossEncoderReranker.cs:26-79` |
| P1-12 | **Blazor admin path self-calls the app's own REST API over loopback**, logging in with config creds to get a JWT — two architectures for one concern, plaintext admin password at runtime, extra auth+serialization hop. | `AdminApiClient.cs:18-55`; `AdminBookService.cs:11-51` |
| P1-13 | **Primary RAG path has no error handling** (`QueryService` has no `catch`, no injected logger) → Ollama/Qdrant/embedder failures surface as raw 500s. Ollama chat client also has **no timeout/retry** (unlike Marker/QueryService clients). | `QueryService.cs:45-119`; `RagPluginBase.cs:290,343`; `KernelFactory.cs:39` |
| P1-14 | **Observability gaps**: `indexer_chunks_total` metric never emitted; the `MedAssist.AI` meter name isn't registered in `AddMeter`; OTel `AddSource("MedAssist.Web")` registered but no `ActivitySource`/spans exist → zero custom traces of the RAG pipeline. | `ServiceCollectionExtensions.cs:265`; `BookIndexer.cs`; `QueryService.cs:28-32` |

## P2 — Medium

| # | Area | Finding | Location |
|---|---|---|---|
| P2-1 | Correctness | BM25 DF/total-doc counts corrupted on resume/re-index (skipped chunks miss `AddChunk`; per-term DF is added to existing rows each run → IDF collapse). | `BookIndexer.cs:74-115`; `VocabularyBuilder.cs:17-46` |
| P2-2 | Correctness | Read-modify-write in `UpsertTermsAsync` loses concurrent DF increments (reads no-tracking, writes absolute). | `BM25VocabService.cs:77-114` |
| P2-3 | Correctness | Web snippet↔source misalignment — filtered `snippets` consumed by absolute index → snippets attributed to the wrong article/URL (bad medical citations). | `QueryService.cs:149-167,201-219` |
| P2-4 | Correctness + Quality | `MarkerClient.PollStatusAsync` has no max timeout and callers pass `CancellationToken.None` → a stuck job polls forever, holding a background scope; transient errors retry indefinitely, book never marked `Failed`. | `MarkerClient.cs:44-85` |
| P2-5 | Correctness | Reranker can build a >512-token sequence (`queryIds + passageIds - 1`, `passageBudget` floor of 3) → ONNX position-embedding overflow crashes the whole query. | `CrossEncoderReranker.cs:55-79` |
| P2-6 | Security | No rate limiting / lockout on authentication → online brute force against the weak default creds. | `LoginEndpoint.cs:20-24`; `Program.cs` |
| P2-7 | Security | Swagger + Scalar API docs served unconditionally in all environments (Prod API surface disclosure). | `Program.cs:53-57` |
| P2-8 | Security | Indirect prompt injection — raw web snippets (tag-stripped only) concatenated into the LLM prompt with no isolation. | `QueryService.cs:328-347,380-400` |
| P2-9 | Security | PHI logging — full clinical query text logged at Information → console. | `RagPluginBase.cs:580-581,599`; `Program.cs:45` |
| P2-10 | Security | Upload has no content-type/magic-byte check and a 764 MB body limit; file written before validation → disk-fill DoS + feeds path traversal. | `UploadBookValidator.cs`; `ServiceCollectionExtensions.cs:142` |
| P2-11 | Security | JWT algorithm not pinned (`ValidAlgorithms` unset), 5-min `ClockSkew` (defense-in-depth; signature validation is on). | `ServiceCollectionExtensions.cs:209-217` |
| P2-12 | Architecture | Repository pattern inconsistent: endpoints/services hit `DbContext` directly, no repo interfaces, `UserRepository` split into Web, `BookCatalogService` duplicates `BookRepository.MapToInfo`. | `UploadBookEndpoint.cs:57-85`; `BookCatalogService.cs`; `Web/Data/UserRepository.cs` |
| P2-13 | Architecture | AI layer coupled to concrete `MedAssistDbContext` (forces `MedAssist.AI` → Data + EF reference; can't build/test AI without EF). | `MedicalDictionaryService.cs:9-45`; `BM25VocabService.cs` |
| P2-14 | Architecture | Duplicate DI registrations silently override lifetime (`AddScoped<T>()` then `AddHttpClient<T>()` — the scoped reg is dead). | `ServiceCollectionExtensions.cs:124-125,134-135` |
| P2-15 | Architecture | Business/data-access logic embedded in Blazor components (`Query.razor` injects `ChatHistoryRepository`, maps entities in-view, uses `JS.InvokeVoidAsync("eval", …)`). | `Query.razor:12,172-177,186-192,251-258` |
| P2-16 | Architecture | `RagPluginBase` (~386 lines) concentrates retrieval, section expansion, rerank orchestration, retry, query rewriting, prompt assembly, markdown stripping. | `RagPluginBase.cs` |
| P2-17 | Performance | `ExpandQueryAsync` runs one DB query per keyword (N round-trips), un-memoized. | `MedicalDictionaryService.cs:25-45` |
| P2-18 | Performance | Missing indexes on illness `lower(name_en/bg)` used for equality + `Contains` (only `IcdCode` unique index exists). | `IllnessEntityConfiguration.cs:9-24` |
| P2-19 | Performance | Two separate `SaveChanges` per query to persist chat history. | `QueryService.cs:90-91`; `ChatHistoryRepository.cs:19-23` |
| P2-20 | Performance | ONNX default `SessionOptions` while gated at `ProcessorCount` concurrency → intra-op thread oversubscription. | `MultilingualE5Embedder.cs`; `CrossEncoderReranker.cs` |
| P2-21 | Quality | `qdrant_results_total` counts final cited sources, not Qdrant hits — misleading metric. | `QueryService.cs:85` |
| P2-22 | Quality | Stale/contradictory spec `ingestion-status-constants` (mandates a string-const class + method the code doesn't have; code uses a `BookStatus` enum). | `openspec/specs/ingestion-status-constants` |
| P2-23 | Quality | Stale spec `private-field-naming` contradicts `.editorconfig` + code (spec wrong, code is consistent `_camelCase`). | `openspec/specs/private-field-naming` |
| P2-24 | Quality | `CLAUDE.md` grossly out of date ("project is in its initial state"). | `CLAUDE.md` |

## P3 — Low

| # | Area | Finding | Location |
|---|---|---|---|
| P3-1 | Correctness | `MergeSmallChunks` merges across heading boundaries and keeps the wrong heading path → wrong citation metadata. | `MarkdownChunker.cs:186-217` |
| P3-2 | Correctness | `ApplyOverlap` can push a chunk back over 512 tokens → embedder silently truncates the tail. | `MarkdownChunker.cs:161-184` |
| P3-3 | Security | `javascript:`-scheme web-source links not scheme-restricted before render. | `Query.razor:197` |
| P3-4 | Security | Open `/metrics` and `/health`; wildcard `AllowedHosts:"*"`. | `Program.cs:42-43`; `appsettings.shared.json` |
| P3-5 | Security | Logout is a GET + AllowAnonymous (forced-logout CSRF); git-crypt key `aiassitantkey` unencrypted on disk (correctly gitignored/untracked). | `LogoutEndpoint.cs:11` |
| P3-6 | Performance | `StripMarkdown` runs 5 non-compiled `Regex.Replace` passes per answer → use `[GeneratedRegex]`. | `RagPluginBase.cs:350-362` |
| P3-7 | Performance | Marker polling fixed at 30 s with no backoff. | `MarkerClient.cs:19,44-48` |
| P3-8 | Quality | Chat-history ordering uses a `now.AddMicroseconds(1)` hack instead of a sequence column. | `QueryService.cs:89-91` |
| P3-9 | Quality | Duplicated `\p{L}+` tokenizer regex in `SparseVectorizer` + `VocabularyBuilder` (index/query symmetry risk); duplicated web-context building in `EnrichWithWebAsync`/`AnswerFromWebAsync`. | — |
| P3-10 | Quality | README drift — missing the PCC-stack Postgres topology now assumed by the seed/reindex scripts. | `README.md` |

---

## Verified NON-issues (checked, not problems)

- **Passwords**: ASP.NET Core Identity `PasswordHasher<T>` (PBKDF2, per-user salt, constant-time verify) — `UserRepository.cs:30,49`.
- **SQL injection**: none — all data access is parameterized EF Core LINQ; no `FromSqlRaw`/`ExecuteSqlRaw`/interpolated SQL anywhere.
- **Endpoint authorization**: every FastEndpoints endpoint and Blazor page is explicitly role-gated (`Roles(...)`/`[Authorize(Roles=...)]`); no missing authz.
- **CSRF**: Blazor login uses `<AntiforgeryToken/>` + `UseAntiforgery()`; `/api` uses JWT bearer (no ambient cookie).
- **XSS**: LLM output renders through Razor (`@msg.Content`, HTML-encoded).
- **E5 embedding**: mean-pooling (batch=1, all-ones mask) and query/passage prefixes are correct.
- **ONNX**: `Run` is thread-safe; embedder/reranker gate concurrency and use `ConfigureAwait(false)`.
- **Async**: no `.Result`/`.Wait()`/`async void` on request paths; background work uses its own `CreateAsyncScope()` (correctly avoiding the disposed request scope).
- **Iterative RAG loop**: `maxIter = Min(MaxIterations, 5)` with exactly 5 strategies; `MinRetryScore ≤ MinAnswerScore` invariant holds.
- **DI lifetimes**: no captive-dependency bug (Kernel correctly Scoped; ONNX singletons capture only singletons; HttpClient via factory/typed clients).
- **Project layering**: acyclic, top-down; `Shared` never references `Data`.
- **Root scripts** (`seed_books.sql`, `reindex_all.sh`, `start.sh`) and `aiassitantkey` are **current, not stale**.

## Test coverage (biggest quality gap)

**6 test classes for ~114 source files.** Covered: `MarkdownChunker`, `SparseVectorizer`,
`ChunkEnricher`, `MarkerClient`, `MedicalDictionaryExpand`, `RagIterativeLoop` (control-flow
only, fully stubbed). `MedAssist.Tests` does not reference `MedAssist.Web`.

**Highest-risk untested (recommended order):** 1) auth/JWT/password hashing; 2) `GetByIcdAsync`/
`SearchAsync` against **real Postgres** (InMemory hides P0-4); 3) `QdrantVectorStore` filter/RRF/
payload mapping; 4) `BookIndexer` resumable checkpoint arithmetic; 5) `CrossEncoderReranker`
token budgeting; 6) `MultilingualE5Embedder`/tokenizer; 7) repositories; 8) `QueryService`
web-fallback branching; 9) `VocabularyBuilder` DF merge; 10) `ExtractionTracker` state machine;
11) endpoints; 12) `WebSearchPlugin`/`ModelInitializer`.
