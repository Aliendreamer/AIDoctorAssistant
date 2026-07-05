# Design — Audit Remediation

Design notes for the fixes where the "how" is non-obvious. Straightforward fixes (log wording,
config flags, regex generation) are covered by `tasks.md` alone.

## 1. Scoped re-index (P0-1)

The current `force=true` path is destructive because it treats "clean rebuild of one book" as
"clean rebuild of everything." Two shared stores must be scoped:

- **Qdrant** — points carry a `BookId` payload. Add `DeleteByBookAsync(bookId)` that issues a
  `Delete` with a `Filter` on `payload.BookId == bookId`. Never call `DeleteCollectionAsync` from
  a per-book path.
- **BM25 vocab/stats** — these are corpus-global aggregates (document frequency across all books).
  Truncating them for one book is wrong in both directions: it destroys other books' contribution
  *and* re-adding inflates counts (see P2-1). The correct model is a **per-book DF contribution**
  so a book can be subtracted and re-added idempotently. Minimum viable fix: record each book's
  per-term contribution (e.g. a `bm25_book_terms` table or a stored per-book snapshot) so
  re-index subtracts the old contribution before adding the new one. This also fixes P2-1/P2-2.

Decision: implement the per-book DF contribution now (it is the root cause of P0-1, P2-1, and
P2-2). If that is too large for the P0 slice, the interim P0 fix is to scope the Qdrant delete and
**disable** the vocab truncate on the per-book path, accepting slightly stale DF until the
two-phase/idempotent work in task 9/13 lands — but never the global wipe.

## 2. Deterministic point ids + two-phase index (P1-5, P1-6)

Root cause of both empty-first-book sparse vectors and duplicate points is ordering + id strategy:

- **Ids**: UUIDv5 (namespace + name) from `"{bookId}:{chunkIndex}"` for chunks and
  `"summary:{bookId}:{section}"` for section summaries. Deterministic → upsert overwrites, so
  re-index is idempotent and force-delete-then-reindex is unnecessary for correctness.
- **Ordering**: index in two passes over the same chunk stream:
  1. **Pass A** — chunk + tokenize + `VocabularyBuilder.AddChunk`; `FlushAsync` to persist DF;
     invalidate + reload the `SparseVectorizer` snapshot.
  2. **Pass B** — embed (dense) + vectorize (sparse, now non-empty) + upsert with deterministic
     ids.

  This keeps a single ONNX embed pass (Pass A does not embed) and guarantees the vocab exists
  before any sparse vector is produced.

## 3. Durable background ingestion (P1-8)

Replace `_ = Task.Run(work, CancellationToken.None)` in the index/extract/bulk endpoints with:

- A singleton `Channel<IngestionJob>` (bounded, `SingleReader`) as the queue.
- A `BackgroundService` consumer that creates a DI scope per job, runs the indexer with a linked
  token from `ApplicationStopping`, and updates status/metrics.
- Endpoints validate + enqueue + return `202`. The existing atomic `InProgress` write (task 11)
  becomes the enqueue guard.

`ExtractionTracker` moves from in-memory to a persisted table (or is folded into `BookStatus` +
a progress column) so state survives restart.

Rationale over Hangfire/queue libs: the workload is single-node, GPU-bound, and already serialized
by the ONNX gate; a `Channel` + `BackgroundService` is the smallest correct primitive and adds no
new infra to the PCC stack.

## 4. Secret & transport model (P0-2, P0-3, P1-3)

- **Signing key**: read from environment/secret at startup. Add an `IValidateOptions<JwtOptions>`
  (or explicit startup check) that fails fast in Production when the key equals the known
  placeholder or is `< 32` bytes. Dev keeps a clearly-marked dev key.
- **Admin bootstrap**: `UserSeeder` creates the admin with a cryptographically-random password if
  none is supplied via secret, and only when the user does not already exist (idempotent). It logs
  that an admin was created and where to set the password — never the password itself.
- **Admin UI**: the loopback self-call (`AdminApiClient`) exists only because Blazor components
  were calling the HTTP API instead of services. Remove it — inject `AdminBookService`/repositories
  in-process (task 15). This also deletes the runtime need for admin creds in config.
- **Image**: config is mounted at runtime (compose already mounts `config/…` is *not* currently —
  it bakes it). Switch `Dockerfile` to expect a mounted `/config` and add `.dockerignore`.

## 5. SSRF guard (P1-4)

The allowlist must be enforced on the **URL actually dereferenced**, not the search query. Add a
`SafeFetch` guard used by `FetchPageSnippetAsync`:

1. Parse URL; require `https`.
2. Host must match an `AllowedDomains` entry by exact or suffix (`.`-boundary) match.
3. Resolve the host; reject if any resolved address is private/loopback/link-local/ULA
   (`10/8`, `172.16/12`, `192.168/16`, `127/8`, `169.254/16`, `::1`, `fc00::/7`, `fe80::/10`).
4. Disable auto-redirects (or re-run the guard on each hop; reject cross-host redirects).

Note the resolve-then-connect TOCTOU is acceptable here (defense-in-depth alongside the allowlist);
a pinned-IP `SocketsHttpHandler.ConnectCallback` is a possible hardening but out of scope for P1.

## 6. Reranker correctness + cost (P2-5, P1-11)

- **Budget**: `passageBudget = max(0, MaxSeq - queryIds.Count)` and hard-truncate `combined` to
  `MaxSeq` (512). Removes the `-1`/floor-of-3 overflow.
- **Cost**: encode the query once per rerank pass; keep a `Dictionary<(string BookId, int
  ChunkIndex), float>` of scores so retry iterations only score newly-added candidates; batch the
  remaining candidates into a real padded ONNX batch instead of `Run` per candidate.

## 7. What we deliberately do NOT change

The audit cleared these; touching them adds risk without benefit: PBKDF2 password hashing,
parameterized EF LINQ (no raw SQL), endpoint/page authorization, antiforgery on the cookie flow,
Razor HTML-encoding of model output, E5 mean-pooling + prefixes, ONNX thread-safety, the iterative
RAG loop bounds/invariants, DI lifetimes (Kernel correctly Scoped), and the acyclic project
layering. See "Verified NON-issues" in `audit-report.md`.

## Open questions

- **Per-book DF store** (design §1): new table vs. per-book JSON snapshot in `bm25_stats`? Table is
  cleaner for SQL subtraction; snapshot avoids a migration. Leaning table.
- **Secret source**: env vars via the PCC stack vs. a dedicated secret store — depends on how the
  PCC stack injects secrets to sibling apps (the compose file loads `config/appsettings.shared.json`
  after env, so today env is *overridden* by config; that precedence must flip for secrets).
- **TLS termination**: at the PCC Traefik proxy (then `ForwardedHeaders` + no app cert) vs. in the
  app. Proxy is consistent with the rest of the PCC stack.
