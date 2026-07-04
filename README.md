# MedAssist — AI Doctor Assistant

A bilingual (EN/BG) RAG-based medical knowledge assistant for physicians. Queries indexed medical books
using hybrid dense + sparse (BM25) vector search, powered by a local LLM via Ollama.

---

## Monitoring GPU utilization

```bash
watch -n2 nvidia-smi
```

---

## Architecture

```text
MedAssist.Shared   — models, interfaces, constants (referenced by all projects)
MedAssist.Data     — EF Core 9 + PostgreSQL, entities, migrations, repositories
MedAssist.AI       — embedder, reranker, sparse vectorizer, Qdrant store,
                     ingestion pipeline, SK plugins, kernel factory
MedAssist.Web      — FastEndpoints REST API + Blazor Server UI
MedAssist.Tests    — xUnit unit tests
```

### Key technologies

| Concern | Technology |
| --- | --- |
| Framework | .NET 10 |
| UI | Blazor Server |
| API | FastEndpoints 8 |
| AI orchestration | Semantic Kernel |
| LLM inference | Ollama (`gemma2:9b` by default) |
| Dense embeddings | `multilingual-e5-large` (ONNX, auto-downloaded) |
| Reranker | `ms-marco-MiniLM-L-6-v2` cross-encoder (ONNX, auto-downloaded) |
| Sparse embeddings | BM25 (in-process) |
| Vector store | Qdrant — hybrid named-vector collection |
| Metadata store | PostgreSQL via EF Core 9 |
| PDF → Markdown | Marker (runs as HTTP service, GPU-accelerated) |
| Observability | OpenTelemetry → Prometheus → Grafana + Tempo |
| Logging | Serilog (compact JSON) |
| Container | Docker Compose |

### Query flow

```text
Browser → Blazor → QueryService
                       ↓
                   RagPluginBase
                   ├─ MedicalDictionary.ExpandQuery()   (ICD-10 synonym expansion)
                   ├─ Embedder.EmbedQueryAsync()         (dense vector, 1024-dim)
                   └─ SparseVectorizer.VectorizeQuery()  (BM25 sparse vector)
                           ↓
                   QdrantVectorStore.SearchAsync()
                   ├─ dense prefetch  → "dense" named vector (cosine)
                   ├─ sparse prefetch → "sparse" named vector (BM25 index)
                   └─ RRF fusion (Reciprocal Rank Fusion)
                           ↓
                   CrossEncoderReranker (ms-marco-MiniLM)
                           ↓
                   Ollama LLM  →  answer + citations
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- [git-crypt](https://github.com/AGWA/git-crypt) — for decrypting `config/` and `books/`

---

## Repository encryption (git-crypt)

The `config/` and `books/` directories are encrypted at rest. After cloning, unlock them:

```bash
# Symmetric key (shared secret)
git-crypt unlock /path/to/medassist.key

# Or GPG-based (if your key was added by a team member)
git-crypt unlock
```

> **CI/CD**: store the base64-encoded key as a secret and run
> `echo "$GIT_CRYPT_KEY" | base64 -d | git-crypt unlock -` before building images.

---

## Shared infrastructure (PersonalCommandCenter)

MedAssist does **not** run its own database, vector store, LLM, metasearch, or observability
stack. Those are provided by the sibling **PersonalCommandCenter (PCC)** stack, and this repo's
`docker-compose.yml` only builds two services: **`web`** (the app) and **`marker`** (GPU OCR).

The app reaches the shared services by container name over the external
`personalcommandcenter_default` Docker network (declared here as `pcc-net`):

| Shared service | Reached as | Provided by |
| --- | --- | --- |
| PostgreSQL | `postgres:5432` | PCC |
| Qdrant (gRPC) | `qdrant:6334` | PCC |
| Ollama | `ollama:11434` | PCC |
| SearXNG | `searxng:8080` | PCC |
| OTEL collector | `otel-collector:4317` | PCC |

> **Ordering matters.** `pcc-net` is an *external* network, so the PCC stack must be started
> **before** MedAssist — otherwise `docker compose up` fails because the network doesn't exist yet.
> The app also can't `depends_on` cross-stack services, so PCC must be healthy before the app runs.
> The `medassist` database is created automatically on first run by EF Core migrations.

## Quick start (Docker)

```bash
# 1. Clone and unlock
git clone <repo-url> && cd AIDoctorAssistant
git-crypt unlock /path/to/medassist.key

# 2. Start the shared PCC stack FIRST (separate repo)
cd ../PersonalCommandCenter && docker compose up -d && cd -

# 3. Pull the LLM into the shared Ollama (runs in the PCC stack)
docker exec -it $(docker ps -qf name=ollama) ollama pull gemma2:9b

# 4. Start MedAssist (web + marker)
docker compose up -d

# 5. Open the web app
open http://localhost:8080
```

### Service URLs

MedAssist publishes only the web app and the Marker OCR service. Everything else (Grafana,
Prometheus, pgAdmin, Qdrant REST, SearXNG UI) is owned by PCC and reached via its Traefik
router at `*.pcc.localhost`.

| Service | URL | Owned by |
| --- | --- | --- |
| Web app | <http://localhost:8080> | MedAssist |
| Scalar API docs | <http://localhost:8080/scalar/v1> | MedAssist |
| Marker | <http://localhost:5002/docs> | MedAssist |
| Grafana | <http://grafana.pcc.localhost> | PCC |
| Prometheus | <http://prometheus.pcc.localhost> | PCC |
| pgAdmin | <http://pgadmin.pcc.localhost> | PCC |
| Qdrant REST | <http://qdrant.pcc.localhost> | PCC |
| SearXNG | <http://searxng.pcc.localhost> | PCC |

---

## UI

The web UI is at **<http://localhost:8080>**. All pages require login — navigate there
and you will be redirected to the login screen automatically.

### Pages

| Path | Role | Description |
| --- | --- | --- |
| `/login` | Public | Sign in with username + password |
| `/query` | Doctor, Admin | Ask medical questions — select language, query type, and optionally filter by book |
| `/admin/books` | Admin | List all books, trigger re-indexing per book |
| `/admin/books/upload` | Admin | Upload a new PDF book |
| `/admin/users` | Admin | List user accounts, delete users |
| `/admin/users/create` | Admin | Create a new Doctor or Admin account |

### Default credentials

On first start the app seeds a single Admin account from `config/appsettings.shared.json`:

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `medassist123` | Admin |

> The `doctor` user from the old config is **not** auto-seeded. Create doctor accounts through
> the UI at `/admin/users/create` after logging in as admin.

After the first run, credentials live in the PostgreSQL `users` table (PBKDF2-hashed). Manage them
entirely from the admin UI — the config list is only used for the first-run seed.

---

## Authentication

The REST API uses JWT bearer tokens. Obtain one via:

```bash
curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"medassist123"}' | jq .token
```

---

## Book ingestion

Books are scanned PDFs (not digital-born). The ingestion pipeline is:

```text
Admin uploads PDF
      ↓
POST /api/admin/books/upload   — saves PDF to /books/raw/, registers in DB (status: pending)
      ↓
POST /api/admin/books/{bookId}/index   — triggers indexing in background
      ↓
Marker (OCR)         — PDF → Markdown
      ↓
MarkdownChunker      — splits into semantic chunks (≤ 512 tokens)
      ↓
ChunkEnricher        — tags each chunk with ICD-10 codes from the medical dictionary
      ↓
MultilingualE5Embedder  — dense vector per chunk (1024-dim)
SparseVectorizer        — BM25 sparse vector per chunk
      ↓
Qdrant               — upserts named vectors (dense + sparse)
PostgreSQL           — updates book status → indexed, saves checkpoints
```

### Step 1 — Upload

`POST /api/admin/books/upload` (Admin role required)

Multipart form fields:

| Field | Required | Description |
| --- | --- | --- |
| `File` | yes | The scanned PDF |
| `BookId` | yes | Unique identifier, e.g. `harrison-21` |
| `Title` | yes | Display title |
| `Author` | yes | Author(s) |
| `Language` | yes | `en` or `bg` |
| `Edition` | no | Edition string |

### Step 2 — Trigger indexing

`POST /api/admin/books/{bookId}/index` (Admin role required)

Returns `202 Accepted` immediately. Indexing runs in the background — check book status via `GET /api/books`.

Indexing is **resumable**: if interrupted, re-triggering picks up from the last checkpoint.

### Check status

`GET /api/books` (Admin or Doctor role required) — returns all indexed books.

---

## Local development

The default dev flow is fully containerized — the app joins the shared network and reaches
the PCC services by container name:

```bash
# 1. Start the shared infra (PCC stack: postgres, qdrant, ollama, searxng, otel-collector)
cd ../PersonalCommandCenter && docker compose up -d && cd -

# 2. Start MedAssist (web + marker) on the shared network
docker compose up -d --build
```

> **Running the app on the host** (`dotnet run --project MedAssist.Web`) is not wired up out of
> the box: the PCC stack doesn't publish `postgres`/`qdrant`/`ollama` on host ports, and Docker's
> container-name DNS only resolves *inside* the network. To do host-based dev you'd need to
> override the endpoints in `config/appsettings.shared.json` (or via `__`-separated env vars) to
> host-reachable addresses. The container flow above is the supported path.

The app auto-downloads ONNX models on first start (~1.2 GB total for embedder + reranker).

---

## Configuration reference

Settings priority (highest wins):

1. Environment variables (`__` as separator, e.g. `Database__ConnectionString`)
2. `config/appsettings.shared.json`

| Key | Default | Description |
| --- | --- | --- |
| `Database:ConnectionString` | — | PostgreSQL connection string |
| `Models:Path` | `models` | Directory for ONNX model files |
| `Models:RerankerPath` | `models/ms-marco-MiniLM-L-6-v2` | Reranker model directory |
| `Books:RawPath` | `/books/raw` | Directory where uploaded PDFs are stored |
| `Marker:Endpoint` | `http://localhost:5002` | Marker HTTP service URL |
| `VectorStore:Qdrant:Endpoint` | `http://localhost:6334` | Qdrant gRPC endpoint |
| `AI:ModelProvider` | `ollama` | LLM provider |
| `AI:Ollama:Endpoint` | `http://localhost:11434` | Ollama base URL |
| `AI:Ollama:ModelName` | `gemma2:9b` | Model tag |

---

## Project structure

```text
AIDoctorAssistant/
├── MedAssist.Shared/
│   ├── Constants/          OnnxConstants, IngestionStatus, LanguageCodes, VectorStoreConstants
│   ├── Interfaces/         IVectorStore, IEmbedder, ISparseVectorizer,
│   │                       IBM25VocabStore, IMedicalDictionary, ICrossEncoderReranker
│   └── Models/             MedicalChunk, BookInfo, SparseVector, BM25VocabSnapshot, …
├── MedAssist.Data/
│   ├── Entities/           BookEntity, IngestionCheckpointEntity, Bm25VocabEntity, …
│   ├── Migrations/
│   ├── Repositories/       BookRepository, CheckpointRepository
│   └── MedAssistDbContext.cs
├── MedAssist.AI/
│   ├── Dictionary/         MedicalDictionaryService, BM25VocabService
│   ├── Embedding/          MultilingualE5Embedder, SparseVectorizer, ModelInitializer
│   ├── Ingestion/          BookIndexer, MarkdownChunker, ChunkEnricher,
│   │                       VocabularyBuilder, MarkerClient
│   ├── Kernel/             KernelFactory
│   ├── Plugins/            RagPluginBase, SymptomsPlugin, DiseasePlugin,
│   │                       TreatmentPlugin, WebSearchPlugin
│   ├── Reranker/           CrossEncoderReranker
│   └── VectorStore/        QdrantVectorStore
├── MedAssist.Web/
│   ├── Components/
│   │   ├── Layout/         MainLayout, AdminLayout, NavMenu
│   │   ├── Pages/          Login, Home (redirect), Query
│   │   │   └── Admin/      Books, UploadBook, Users, CreateUser
│   │   └── Shared/         BookSourceCitation, WebSourceCitation
│   ├── Data/               UserRepository
│   ├── Endpoints/
│   │   ├── Auth/           LoginEndpoint, LogoutEndpoint
│   │   ├── Books/          ListBooksEndpoint, UploadBookEndpoint, TriggerIndexEndpoint
│   │   ├── Dictionary/     GetByIcdEndpoint, SearchDictionaryEndpoint
│   │   ├── Query/          QueryEndpoint
│   │   └── Users/          ListUsersEndpoint, CreateUserEndpoint, DeleteUserEndpoint
│   ├── Extensions/         ServiceCollectionExtensions, WebApplicationExtensions
│   ├── Services/           BookCatalogService, QueryService,
│   │                       AdminApiClient, AdminBookService, AdminUserService
│   ├── Startup/            UserSeeder
│   └── Program.cs
├── MedAssist.Tests/
├── config/
│   └── appsettings.shared.json
├── books/
│   └── raw/                Uploaded PDFs (git-crypt encrypted)
├── docker/
│   └── marker/             Marker OCR service (Dockerfile + app.py) — the only infra MedAssist builds
├── requests/
│   └── medassist.yaak.json  Yaak/Insomnia v4 collection
├── docker-compose.yml
└── MedAssist.slnx
```

---

## Build and test

```bash
dotnet build MedAssist.slnx
dotnet test MedAssist.Tests
```
