## Tasks

Grouped by severity. **P0 must ship before P1.** Each fix should land with a regression test where
the audit noted the area is untested. Per project rules, `dotnet build`/`docker` steps are stated
and run only with explicit approval.

### P0 — Critical

#### 1. Scope destructive re-index to a single book (P0-1) ✅ DONE
- [x] `IVectorStore`/`QdrantVectorStore`: added `DeleteByBookAsync(bookId)` using a `Payload.BookId` filter (no `DeleteCollectionAsync`)
- [x] Extracted `BookReindexCleaner` (MedAssist.AI) as the single, testable owner of reindex-clearing; `TriggerIndexEndpoint` force path now calls it and **no longer** truncates the shared `bm25_vocab`/`bm25_stats` tables; misleading log fixed
- [x] Per-book BM25 DF subtraction now implemented in task 9 (delta-based `ApplyBookContributionAsync`) — a force re-index self-corrects that book's DF idempotently; the cleaner still never wipes the shared tables. (Interim "slightly stale DF" note superseded.)
- [x] Test (`BookReindexCleanerTests`): force re-index deletes only that book's vectors and never `DeleteCollectionAsync`; TDD RED→GREEN; 65 tests pass, solution builds 0/0
> Note: end-to-end verification against a live Qdrant (point counts for other books) needs the running stack — tracked in the integration-test uplift.

#### 2. JWT signing key (P0-2) — ✅ DONE (guard) + accepted deployment model
> **Decision (2026-07-04):** the committed-key approach is accepted (local build/deploy, internal
> docker network behind an HTTPS-terminating proxy). Added a startup guard so the *self-describing
> placeholder* can never be the effective Production key silently. Since `docker-compose.yml`
> defaults `ASPNETCORE_ENVIRONMENT=Production`, the placeholder was also swapped for a real random
> key so `docker compose up` still boots.
- [x] `JwtKeyPolicy.Validate` (MedAssist.Shared): rejects placeholder / `<32`-byte key in Production; no-op elsewhere
- [x] `WebApplicationExtensions.ValidateJwtSigningKey` called in `Program.cs` right after `Build()` (fail-fast)
- [x] Replaced the `"…change-me…"` placeholder in `config/appsettings.shared.json` with a real 64-char key
- [x] Tests (`JwtKeyPolicyTests`, 6 cases). 129 tests pass, build 0/0
> Full secret-manager sourcing (env/secret store instead of committed config) intentionally NOT done — committing the key is accepted for this deployment.

#### 3. Strong admin bootstrap; decouple admin UI from seed creds (P0-3) — ✅ ACCEPTED / WON'T FIX (deployment model)
> **Decision (2026-07-04):** accepted. Same trust boundary as P0-2/P1-2/P1-3 — local build/deploy,
> internal docker network behind the HTTPS proxy. The committed default admin credential is
> accepted. (The admin-UI/loopback decoupling in task 15 still has independent architectural merit
> and stays on the backlog.)
- [x] No change — accepted risk given deployment model

#### 4. Fix `GetByIcdAsync` (P0-4) — cross-confirmed ✅ DONE
- [x] Replace `IcdCode.Equals(icdUpper, StringComparison…)` with translatable `IcdCode.ToUpper() == icdUpper` (`MedicalDictionaryService.cs:69`)
- [x] Test against a **real relational** provider — added in-memory **SQLite** test harness (`MedicalDictionaryLookupTests.cs`) that reproduced the translation failure (RED) then passed (GREEN); InMemory hides it. Advisory for the transitive native SQLite lib suppressed **test-project-only** via `NuGetAuditSuppress`.
- [x] Full suite green: 63 passed

### P1 — High

#### 5. BookId allowlist + path containment (P1-1) ✅ DONE
- [x] New pure `BookIdRules` (MedAssist.Shared): `IsValid` (`^[a-z0-9][a-z0-9-]{0,63}$`, `[GeneratedRegex]`) + `ResolveWithin(baseDir, id, ext)` that validates and asserts `GetFullPath` stays under baseDir
- [x] `UploadBookValidator`: `.Must(BookIdRules.IsValid)` → rejects traversal at the upload boundary (400 before any file write)
- [x] Derived-path containment via `ResolveWithin` in `UploadBookEndpoint` (write), `TriggerIndexEndpoint` (both `.md` paths, + early 400 guard), `BulkExtractEndpoint` (filter invalid ids + `.md` path)
- [x] Tests (`BookIdRulesTests`): accepts allowlisted ids; rejects `../../etc/passwd`, slashes, dots, uppercase, symbols, over-length; `ResolveWithin` throws on escape. 87 tests pass, solution 0/0
- [~] `docker/marker/app.py` mirror check deferred (Python side; belt — .NET side already refuses invalid ids before Marker is called)

#### 6. Transport security (P1-2) — ✅ ACCEPTED / WON'T FIX (deployment model)
> **Decision (2026-07-04):** accepted. TLS terminates at the internal HTTPS proxy and traffic rides
> the docker network, so cleartext is never exposed to untrusted parties. If cookie flags are ever
> tightened, remember `ForwardedHeaders` so the app sees the original request as HTTPS behind the proxy.
- [x] No change — accepted risk given deployment model

#### 7. Secrets out of the Docker image (P1-3) — ✅ ACCEPTED / WON'T FIX (deployment model)
> **Decision (2026-07-04):** accepted. The image is built and run only on the owner's host and is
> never pushed to any registry, so image-layer secret extraction requires local host access —
> already inside the trust boundary. No change.
- [x] No change — accepted risk given deployment model

#### 8. SSRF guard on web fetch (P1-4) ✅ DONE
- [x] New pure `WebFetchPolicy` (MedAssist.Shared): `IsAllowedUrl` (https + host-suffix allowlist) + `IsBlockedAddress` (loopback/RFC1918/link-local/CGNAT/IPv6 ULA/metadata)
- [x] `QueryService.FetchPageSnippetAsync` now validates the URL, DNS-resolves the host, and refuses blocked addresses before `GetStringAsync`
- [x] Tests (`WebFetchPolicyTests`, 25 cases): allows https allowlisted hosts; rejects http/wrong-scheme/suffix-spoof/non-allowlisted; blocks `169.254.169.254`, `10/8`, `172.16-31`, `192.168`, `100.64/10`, `::1`, `fe80::`, `fc00::`. 112 tests pass, 0/0
- [~] Cross-host **redirect** re-validation deferred (design note: pinned-IP `ConnectCallback` is the stronger follow-up; initial-URL guard covers the primary SSRF vector)

#### 9. Two-phase vocab-before-vectors (P1-5 + P2-1) — ✅ DONE (code + migration + unit tests) — one integration check stack-pending
> **Why it was held & now unblocked:** fixing first-book-empty-sparse-vectors requires a vocab-first
> pass; but a vocab-first pass that re-runs on **resume or re-index** double-counts document
> frequencies unless the book's DF contribution is idempotent. That idempotency is now provided by
> **per-book DF storage** + delta accounting, and the risky DF math is pinned by real-relational
> (SQLite) unit tests, so it no longer needs the stack to land safely. Design: `design.md §1/§2`.
- [x] Migration `AddBm25BookContributions`: `bm25_book_terms(book_id, term, document_frequency)` (PK `(book_id, term)`, `book_id` index) + per-book chunk count in `bm25_book_stats(book_id, chunk_count)`
- [x] `IBM25VocabStore.ApplyBookContributionAsync(bookId, termDfs, chunkCount)` (replaces the additive `UpsertTermsAsync`/`GetTotalDocumentsAsync`): in one transaction, folds each term's `(new − old)` delta into global `bm25_vocab`, applies the `chunkCount` delta to `bm25_stats`, drops global rows that reach ≤0, and replaces the book's stored contribution in place. `BM25VocabService` (DB) + `Bm25VocabCache` (invalidates) + `VocabularyBuilder.FlushAsync(bookId)`
- [x] `BookIndexer` Pass A: `AddChunk` over ALL chunks → `FlushAsync(bookId)` (delta apply) → `Invalidate` the snapshot, BEFORE any embed/vectorize. Idempotent on resume/re-index by construction (zero-delta re-apply)
- [x] `BookIndexer` Pass B: embed + vectorize (sparse now non-empty) + upsert, resumable from checkpoint; section summaries also vectorized after Pass A
- [x] `BookReindexCleaner` doc updated — per-book DF subtraction is now the indexer's job (force re-index self-corrects DF), not deferred
- [x] Tests: `Bm25BookContributionTests` (SQLite — first book populates globals; re-apply is idempotent; second book adds then re-index applies delta leaving the first intact; dropped term removes its global row) + `VocabularyBuilderTests` (per-chunk DF dedup, chunk count, state reset, empty-flush clears). 143 tests pass, build 0/0
- [~] Integration test on the live stack (first book on a fresh DB has non-empty sparse vectors; re-index/resume leaves global DF unchanged) — **stack-pending**; the unit tests exercise the same delta logic against real SQL

#### 10. Deterministic Qdrant point ids (P1-6) ✅ DONE
- [x] New pure `DeterministicGuid.Create(key)` (MedAssist.Shared); `QdrantVectorStore.UpsertAsync` derives id from `"{bookId}:{chunkIndex}"` (or `"summary:…"`) so re-index overwrites
- [x] Tests (`DeterministicGuidTests`): stable per key, differs per key, summary≠chunk at same index, non-empty
- [~] "same point count after re-index" needs a live Qdrant — integration-pending

#### 11. Close concurrent-index race (P1-7) ✅ DONE (unit-pending live verify)
- [x] `BookRepository.TryMarkInProgressAsync` — atomic `ExecuteUpdateAsync` (`WHERE Status != InProgress`); `TriggerIndexEndpoint` claims before any clear/Task.Run, returns 409 if not claimed
- [~] Two-simultaneous-triggers test needs real Postgres (`ExecuteUpdate` + enum) — integration-pending; logic atomic by construction

#### 12. Durable background ingestion (P1-8) — ✅ DONE
- [x] `IngestionQueue` (`Channel`, single-reader) + `IngestionWorker : BackgroundService`; index/extract logic moved out of the endpoints' `Task.Run` into the worker on a per-job DI scope
- [x] Honors `ApplicationStopping` (`stoppingToken`): a shutdown mid-index leaves the book `InProgress` + checkpoint → resumes next start; Marker calls now receive the token
- [x] All three endpoints (`TriggerIndex`, `ExtractBook`, `BulkExtract`) validate + enqueue + 202; registered `AddHostedService<IngestionWorker>()`
- [x] `MedAssist.Tests` now references `MedAssist.Web` (unblocks Web-layer tests — the audit's #1 coverage gap); `IngestionQueueTests` guards FIFO order. 130 tests pass, 0/0
- [x] **Persisted `ExtractionTracker`:** new `extraction_status` table + `ExtractionStatusRepository` (migration `AddExtractionStatus`); `ExtractionTracker` is now a DB-backed singleton (scope-per-call, like `Bm25VocabCache`) with an async API. Extract status survives restart. On worker startup, any row left `Running` is reconciled to `Failed` ("Interrupted by shutdown") since the in-memory queue is also lost on restart — so a book is never reported in-flight forever and can be re-triggered. `MarkDone/MarkFailed` use `CancellationToken.None` so the outcome persists even during shutdown. Tests: `ExtractionStatusRepositoryTests` (SQLite — start/idempotent-running/restart-after-failure/done/failed/throw-if-unstarted/ordering/interrupted-reconcile). 151 tests pass, build 0/0
- [~] Integration verification of shutdown-mid-job on the running host — **stack-pending** (behavior covered by unit tests against real SQL + startup reconciliation)

#### 13. Hot-path performance (P1-9, P1-10, P1-11)
- [x] **P1-10** `ChunkEnricher`: loads the dictionary once per instance (lazy + lock) instead of per chunk; test `DictionaryIsLoadedOnce_AcrossManyChunks` guards it (matching semantics unchanged; word-boundary false-positive fix left to P2)
- [x] **P1-9** BM25 vocab singleton cache: new `Bm25VocabCache` singleton (loads once via a short-lived scope — no captive `DbContext`; `Invalidate()` on index); `SparseVectorizer` is now a stateless singleton over it. `BookIndexer` invalidates after `FlushAsync`. Existing `SparseVectorizerTests` still green (ctor preserved).
- [x] **P1-11 (P2-5)** `CrossEncoderReranker`: extracted pure `CombineInputIds` with a hard 512-token cap and fixed the budget (`512 - queryLen`, was `Max(3, 512-queryLen+1)` → could hit 514 → ONNX crash). Tests `CrossEncoderRerankerTests` (6 cases). **Deferred:** the cross-retry score memoization + batched inference (perf-only) — the crash fix is the critical part.

#### 14. Resilience & observability (P1-13, P1-14) ✅ DONE
- [x] `QueryService`: injected `ILogger<QueryService>`, wrapped the RAG path in try/catch → structured log + user-safe `QueryResult`; added a real `ActivitySource("MedAssist.Web")` span (registration is no longer dead)
- [x] Ollama chat client: DI-registered named `"ollama"` `HttpClient` with a bounded 5-min timeout (was a default client with no timeout); `KernelFactory` uses the `HttpClient` overload
- [x] `BookIndexer` emits `indexer_chunks_total`; `AddMeter("MedAssist.AI")` registered so it exports
- [~] Verified by build + code review; metric/span emission needs the running collector to observe end-to-end

#### 15. Admin UI in-process boundary (P1-12) — ✅ DONE (code + tests) — final browser click-through pending
> `AdminBookService`/`AdminUserService` call the app's own REST API over loopback via
> `AdminApiClient` (config-cred JWT login). Going in-process means routing each call to the
> underlying repository/application service — and for upload/index it means first **extracting** the
> endpoint bodies into shared services (they currently own file-write + the indexing trigger). That
> extraction + rewiring is a real refactor whose payoff (admin pages still work) can only be
> confirmed in a browser. Deferring until the endpoint→service extraction is done. Removing the
> loopback also retires the runtime need for `Auth:Users` config creds (ties to deferred P0-3).
- [x] Extracted endpoint logic into shared in-process application services: `BookApplicationService` (`UploadAsync` — PDF-header validation + path-contained file write + upsert; `TriggerIndexAsync` — validate → atomic `TryMarkInProgress` → optional per-book force-clear → enqueue) and `UserApplicationService` (list/create/delete with password/role validation + the last-admin guard, all centralized). The `UploadBookEndpoint`/`TriggerIndexEndpoint`/`CreateUser`/`DeleteUser`/`ListUsers` endpoints are now thin: parse request → call service → map result to status codes
- [x] Blazor admin pages now run fully in-process: `AdminBookService`/`AdminUserService` are thin adapters over the application services (page code + method signatures unchanged), and **`AdminApiClient` is deleted** — no more loopback REST call with a config-cred JWT login. DI drops `AddHttpClient<AdminApiClient>()`; `WebApp:SelfBaseUrl` is no longer read at runtime (`Auth:Users` remains, used only by `UserSeeder`)
- [x] Tests: `UserApplicationServiceTests` (8, TDD RED→GREEN — create validation, duplicate, last-admin guard) + `BookApplicationServiceTests` (6, characterization — upload invalid-id/non-pdf/valid, trigger not-found/started-enqueues-and-claims/already-in-progress). Fixed `UserRepository.ListAsync` to order client-side (tiny admin table; SQLite can't `ORDER BY DateTimeOffset`). 165 tests pass, build 0/0
- [~] Final browser click-through (list/upload/reindex/users pages work end-to-end) — **browser-gated**; logic is covered by the unit/characterization tests and the endpoints now share the exact same code paths
- [ ] Browser check: list/upload/reindex/users pages work

### P2 — Medium (after P0/P1)
- [x] Idempotent BM25 DF accounting on resume/re-index (P2-1) — **DONE** in task 9: per-book `bm25_book_terms`/`bm25_book_stats` + delta-based `ApplyBookContributionAsync`; re-apply is a no-op (unit-tested on SQLite)
- [x] Atomic SQL DF increment / serialize vocab flushes (P2-2) — **DONE** in task 9: the whole read-old → apply-delta → replace-contribution runs in a single `BM25VocabService` transaction per book (one flush per book, at end of Pass A)
- [x] Zip web snippets↔sources in lockstep — `QueryService.BuildWebContextAsync` pairs each snippet with its own source (was mis-indexed) (P2-3)
- [x] `MarkerClient` max elapsed/poll deadline (`_maxPollDuration`, default 2h) → `TimeoutException`; consecutive-failure cap (default 10) → give up; worker now passes the shutdown token (P2-4)
- [x] Reranker budget fixed + hard 512 cap via `CombineInputIds` (done with P1-11) (P2-5)
- [ ] Login rate limiting — **deferred** (low value on the trusted internal network; cheap to add later) (P2-6)
- [x] Gate Swagger/Scalar — **ACCEPTED as-is** (deployment model): API docs (`/scalar/v1`) are intentionally available on the internal/trusted network; documented in README (P2-7)
- [x] Fence untrusted web text in `<web_source>` tags + system-prompt instruction to treat it as data, not instructions (P2-8)
- [x] Clinical query logging downgraded to Debug in `RagPluginBase` (3 sites) so PHI stays out of default logs (P2-9)
- [x] Validate PDF `%PDF-` header before persisting the upload (P2-10). (Size cap unchanged — accepted given trusted single-user deployment.)
- [x] Pin JWT `ValidAlgorithms=[HmacSha256]`, `ValidateIssuerSigningKey`, `ClockSkew=Zero` (P2-11)
- [x] Repository interfaces for the ingestion repos (P2-12) — added `IBookRepository` + `ICheckpointRepository` (Shared/Interfaces) and moved the `IngestionCheckpoint` record to Shared/Models; `BookRepository`/`CheckpointRepository` implement them; DI registers interface→concrete forwards. [~] Full consolidation of every repo (User/ChatHistory/ExtractionStatus) behind interfaces left optional — only the AI-facing ones were needed for the decoupling
- [x] Decouple AI from `MedAssistDbContext`; drop the AI→EF reference (P2-13) — **DONE**: moved the two EF-backed services (`MedicalDictionaryService`, `BM25VocabService`) from `MedAssist.AI/Dictionary` → `MedAssist.Data/Services`; `BookIndexer` now depends on `IBookRepository`/`ICheckpointRepository` (Shared); `Bm25VocabCache` stays in AI over the interface. Removed the `MedAssist.AI → MedAssist.Data` project reference and the EF package refs — **`MedAssist.AI` now references only `MedAssist.Shared`**. Build 0/0, 163 tests pass. [~] DI/startup wiring verifiable against the running app
- [x] Removed dead `AddScoped<QueryService>` / `AddScoped<AdminApiClient>` (the `AddHttpClient<T>` typed-client registration already covers them) (P2-14)
- [ ] Move data logic out of `Query.razor`; replace `JS eval` (P2-15) — **remaining (browser-gated)**
- [x] Extract `RagPluginBase` collaborators (P2-16) — pulled two focused collaborators out of the 394-line base: `MarkdownStripper` (pure answer post-processing) and `CandidateRetriever` (hybrid dense+sparse gather + section expansion, built in the base ctor from its existing deps so subclass constructors are unchanged). Base now 317 lines; behavior held — all 9 `RagIterativeLoopTests` + full suite (163) green, build 0/0. [~] Further splits (`AnswerComposer` for `BuildResultAsync`, `QueryRewriter` for `MaybeRewriteQueryAsync`) left optional — they couple to the SK kernel + the virtual `GetSystemPrompt`
- [x] Batch `ExpandQueryAsync` into a single `IN (...)` query instead of one round-trip per term (P2-17). (Cross-request memoization deferred — the vocab cache already removed the bigger per-query load.)
- [x] `lower(name_en/bg)` expression indexes + `pg_trgm` GIN (P2-18) — migration `AddDictionaryTrigramIndexes` (raw SQL, kept out of the model so the snapshot/SQLite tests are unaffected): `CREATE EXTENSION pg_trgm`; b-tree expression indexes on `lower(name_en)`/`lower(name_bg)`/`lower(alias)` for `ExpandQueryAsync`'s equality `IN`; GIN trigram indexes on `lower(name_en)`/`lower(name_bg)` for `SearchAsync`'s `LIKE '%q%'`. [~] EXPLAIN-verification of plan usage still needs live Postgres
- [x] Single `SaveChanges` for chat-history pair (`AddMessagesAsync`) (P2-19)
- [ ] Tune ONNX `IntraOpNumThreads` — **deferred (needs profiling)**; setting it blind risks regressing single-query latency (P2-20)
- [ ] Rename/relocate `qdrant_results_total` — **deferred**; the name is referenced by the ported Grafana dashboard, so renaming without updating it there would break the panel (P2-21)
- [x] Correct stale spec `ingestion-status-constants` (P2-22) — rewritten to document the actual `BookStatus` enum (`Pending/InProgress/Indexed/Failed`, mapped to Postgres `book_status`); removed the obsolete `IngestionStatus` string-constants requirements. Passes `openspec validate --specs --strict`
- [x] Correct stale spec `private-field-naming` (P2-23) — rewritten to match `.editorconfig`: **all** private fields incl. `const` use `_camelCase` (dropped the wrong "private const = PascalCase" rule and the reference to the deleted `IllnessDictionaryRepository.cs`); kept the accurate `LanguageCodes` display-name requirement. Passes strict validation
- [x] Rewrote `CLAUDE.md` with real overview/architecture/build-test-run + split-stack infra + deployment-model note (P2-24)

### P3 — Low
- [x] `MergeSmallChunks` only merges same-heading chunks (was mislabeling merged content) (P3-1)
- [x] Cap overlap so `ApplyOverlap` can't push a chunk past the 512-token budget (P3-2)
- [x] Restrict web-source link scheme to http/https before render (`WebFetchPolicy.IsHttpUrl`, gated in `Query.razor` + `WebSourceCitation.razor`) (P3-3)
- [x] `/metrics` + `/health` and `AllowedHosts` — **ACCEPTED** (internal/trusted deployment) (P3-4)
- [ ] Logout → POST — **skipped**: would need a UI form change I can't browser-verify; forced-logout CSRF is negligible on the trusted network (P3-5)
- [x] `[GeneratedRegex]` for `StripMarkdown` (class made `partial`, 5 source-gen regexes) (P3-6)
- [x] Marker polling backoff (P3-7) — **obsolete**: the `migrate-marker-to-mineru` change removed Marker entirely; MinerU's `/file_parse` is a single synchronous call with no polling loop, so there is nothing to back off
- [x] Chat-history deterministic ordering (P3-8) — resolved **without** a new column: the identity `Id` is already the monotonic insertion sequence, so `GetRecentAsync` now tie-breaks equal `CreatedAt` by `Id` (`ThenBy(m => m.Id)`), and `QueryService` no longer fudges the assistant row's timestamp with `AddMicroseconds(1)` (both rows share the true `now`). Code-only, no migration. 165 tests pass, build 0/0
- [x] Shared `\p{L}+` tokenizer pattern via `TokenizationConstants.WordPattern` (index/query symmetry); web-context building already deduped in P2-3 (P3-9)
- [x] README PCC split-stack topology updated earlier this effort; seed/reindex scripts already reference the PCC Postgres (P3-10)

### Test coverage uplift (parallel to the above)
- [x] Reference `MedAssist.Web` from `MedAssist.Tests` (done with task 12; Web-layer service/repository tests now compile and run)
- [~] Add tests in the audit's priority order: auth/JWT/password ✅ → `GetByIcdAsync` (real SQLite) ✅ → BM25 DF accounting ✅ → extraction-status ✅ → user/book application services ✅ → reranker budgeting ✅ → **still open**: `QdrantVectorStore`, `QueryService` branching (both stack/mock-heavy)

### Verification
- [x] `dotnet build MedAssist.slnx` — 0 errors, 0 warnings ✅ (run each iteration)
- [x] `dotnet test MedAssist.Tests` — all green ✅ (165 passed)
- [x] `openspec validate audit-remediation --strict` ✅ (change is valid)
