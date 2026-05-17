# MedAssist — AI Doctor Assistant

A bilingual (EN/BG) RAG-based medical knowledge assistant for physicians. Queries indexed medical books using hybrid dense + sparse (BM25) vector search, powered by a local LLM via Ollama.

---

## Architecture

```
MedAssist.Shared   — models, interfaces, constants (referenced by all projects)
MedAssist.Data     — EF Core 9 + PostgreSQL, entities, migrations, repositories
MedAssist.AI       — embedder, reranker, sparse vectorizer, Qdrant store,
                     ingestion pipeline, SK plugins, kernel factory
MedAssist.Web      — FastEndpoints REST API + Blazor Server UI
MedAssist.Tests    — xUnit unit tests
```

### Key technologies

| Concern | Technology |
|---|---|
| Framework | .NET 10 |
| UI | Blazor Server |
| API | FastEndpoints 8 |
| AI orchestration | Semantic Kernel |
| LLM inference | Ollama (`qwen2.5:7b` by default) |
| Dense embeddings | `multilingual-e5-large` (ONNX, auto-downloaded) |
| Reranker | `ms-marco-MiniLM-L-6-v2` cross-encoder (ONNX, auto-downloaded) |
| Sparse embeddings | BM25 (in-process) |
| Vector store | Qdrant — hybrid named-vector collection |
| Metadata store | PostgreSQL via EF Core 9 |
| PDF → Markdown | Docling (runs as HTTP service) |
| Observability | OpenTelemetry → Prometheus → Grafana + Tempo |
| Logging | Serilog (compact JSON) |
| Container | Docker Compose |

### Query flow

```
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

## Quick start (Docker)

```bash
# 1. Clone and unlock
git clone <repo-url> && cd AIDoctorAssistant
git-crypt unlock /path/to/medassist.key

# 2. Start all services
docker compose up -d

# 3. Pull the LLM into Ollama
docker exec -it $(docker compose ps -q ollama) ollama pull qwen2.5:7b

# 4. Open the web app
open http://localhost:8080
```

### Service URLs

| Service | URL | Credentials |
|---|---|---|
| Web app | http://localhost:8080 | see UI section below |
| Scalar API docs | http://localhost:8080/scalar/v1 | — |
| pgAdmin | http://localhost:5050 | `admin@medassist.com` / `medassist` |
| Grafana | http://localhost:3000 | `admin` / `medassist` |
| Prometheus | http://localhost:9090 | — |
| Qdrant REST | http://localhost:6333 | — |
| Docling | http://localhost:5001/docs | — |

---

## UI

The web UI is at **http://localhost:8080**. All pages require login — navigate there and you will be redirected to the login screen automatically.

### Pages

| Path | Role | Description |
|---|---|---|
| `/login` | Public | Sign in with username + password |
| `/query` | Doctor, Admin | Ask medical questions — select language, query type, and optionally filter by book |
| `/admin/books` | Admin | List all books, trigger re-indexing per book |
| `/admin/books/upload` | Admin | Upload a new PDF book |
| `/admin/users` | Admin | List user accounts, delete users |
| `/admin/users/create` | Admin | Create a new Doctor or Admin account |

### Default credentials

On first start the app seeds a single Admin account from `config/appsettings.shared.json`:

| Username | Password | Role |
|---|---|---|
| `admin` | `medassist123` | Admin |

> The `doctor` user from the old config is **not** auto-seeded. Create doctor accounts through the UI at `/admin/users/create` after logging in as admin.

After the first run, credentials live in the PostgreSQL `users` table (PBKDF2-hashed). Manage them entirely from the admin UI — the config list is only used for the first-run seed.

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

```
Admin uploads PDF
      ↓
POST /api/admin/books/upload   — saves PDF to /books/raw/, registers in DB (status: pending)
      ↓
POST /api/admin/books/{bookId}/index   — triggers indexing in background
      ↓
Docling (OCR)        — PDF → Markdown
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
|---|---|---|
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

```bash
# Start infrastructure only
docker compose up postgres qdrant ollama docling -d

# Run the web app
dotnet run --project MedAssist.Web
```

The app auto-downloads ONNX models on first start (~1.2 GB total for embedder + reranker).

---

## Configuration reference

Settings priority (highest wins):

1. Environment variables (`__` as separator, e.g. `Database__ConnectionString`)
2. `config/appsettings.shared.json`

| Key | Default | Description |
|---|---|---|
| `Database:ConnectionString` | — | PostgreSQL connection string |
| `Models:Path` | `models` | Directory for ONNX model files |
| `Models:RerankerPath` | `models/ms-marco-MiniLM-L-6-v2` | Reranker model directory |
| `Books:RawPath` | `/books/raw` | Directory where uploaded PDFs are stored |
| `Docling:Endpoint` | `http://localhost:5001` | Docling HTTP service URL |
| `VectorStore:Qdrant:Endpoint` | `http://localhost:6334` | Qdrant gRPC endpoint |
| `AI:ModelProvider` | `ollama` | LLM provider |
| `AI:Ollama:Endpoint` | `http://localhost:11434` | Ollama base URL |
| `AI:Ollama:ModelName` | `qwen2.5:7b` | Model tag |

---

## Project structure

```
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
│   │                       VocabularyBuilder, DoclingClient
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
│   ├── otel-collector/
│   ├── prometheus/
│   ├── grafana/
│   ├── tempo/
│   └── searxng/
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
