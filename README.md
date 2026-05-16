# MedAssist — AI Doctor Assistant

A bilingual (EN/BG) RAG-based medical knowledge assistant for physicians. Queries indexed medical books using hybrid dense + sparse (BM25) vector search, powered by a local LLM via Ollama.

---

## Architecture

```
MedAssist.Shared      — models, interfaces, constants (referenced by all projects)
MedAssist.AI          — embedder, sparse vectorizer, Qdrant store, SK plugins, kernel factory
MedAssist.Web         — Blazor Server UI + QueryService
MedAssist.Indexer     — CLI tool: ingest books, build vocabulary, manage dictionary
MedAssist.Tests       — xUnit unit tests
```

### Key technologies

| Concern | Technology |
|---|---|
| Framework | .NET 10 |
| UI | Blazor Server |
| AI orchestration | Semantic Kernel |
| LLM inference | Ollama (`qwen2.5:7b` by default) |
| Dense embeddings | `multilingual-e5-large` (ONNX, auto-downloaded) |
| Sparse embeddings | BM25 (in-process, b=0) |
| Vector store | Qdrant — hybrid named-vector collection |
| Metadata store | SQLite — book catalog, medical dictionary, BM25 vocab |
| Observability | OpenTelemetry → Prometheus → Grafana |
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
                   Ollama LLM  →  answer + citations
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- [git-crypt](https://github.com/AGWA/git-crypt) — for decrypting `config/` and `books/`
- [Ollama](https://ollama.com/) — only needed for local development outside Docker

---

## Repository encryption (git-crypt)

The `config/` and `books/` directories are encrypted at rest using git-crypt. After cloning, unlock them with your key:

```bash
# Symmetric key (shared secret)
git-crypt unlock /path/to/medassist.key

# Or GPG-based (if your key was added by a team member)
git-crypt unlock
```

All files under `config/` and `books/` will then be transparently decrypted. Docker builds happen from your unlocked working tree, so the build context always contains the decrypted files.

> **CI/CD**: store the base64-encoded key as a secret and run  
> `echo "$GIT_CRYPT_KEY" | base64 -d | git-crypt unlock -` before building images.

---

## Quick start (Docker)

```bash
# 1. Clone
git clone <repo-url>
cd AIDoctorAssistant

# 2. Start all infrastructure (Qdrant, Ollama, Prometheus, Grafana)
docker compose up -d

# 3. Pull the LLM into Ollama
docker exec -it $(docker compose ps -q ollama) ollama pull qwen2.5:7b

# 4. Index a book (place the preprocessed markdown in books/raw/)
docker compose run --rm --profile indexer indexer \
  index \
  --book /books/raw/harrison.md \
  --book-id harrison-21 \
  --title "Harrison's Principles of Internal Medicine, 21e" \
  --author "Kasper et al." \
  --language en

# 5. Open the web app
open http://localhost:8080
```

Grafana is available at **http://localhost:3000** (admin / `medassist`).  
Prometheus at **http://localhost:9090**.

---

## Local development (without Docker)

### 1. Start infrastructure only

```bash
docker compose up qdrant ollama prometheus grafana -d
docker exec -it $(docker compose ps -q ollama) ollama pull qwen2.5:7b
```

If you already have Qdrant running separately, make sure **both ports** are exposed — port 6333 (REST/health) and port 6334 (gRPC, used by the .NET client):

```bash
docker run -d -p 6333:6333 -p 6334:6334 -v qdrant_data:/qdrant/storage qdrant/qdrant
```

### 2. Edit the shared config

After unlocking git-crypt, edit `config/appsettings.shared.json` with your local paths. This file is the single source of defaults for all projects:

```json
{
  "Database": { "Path": "medassist.db" },
  "Models":   { "Path": "models" },
  "VectorStore": {
    "Qdrant": { "Endpoint": "http://localhost:6333" }
  },
  "AI": {
    "ModelProvider": "ollama",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "ModelName": "qwen2.5:7b"
    }
  }
}
```

Both the Web app and Indexer load this file automatically. It is inserted at the lowest priority, so environment variables always override it.

Set `MEDASSIST_CONFIG_PATH=/path/to/config` to point to a different directory (used automatically in Docker at `/config`).

### 3. Run the web app

```bash
dotnet run --project MedAssist.Web
```

The app auto-downloads the embedding model on first start (~1.2 GB).  
Visit **http://localhost:5000**.

---

## Indexer CLI

The indexer is a standalone console app. Run it with `dotnet run --project MedAssist.Indexer -- <command>`.

### Index a book

```bash
dotnet run --project MedAssist.Indexer -- index \
  --book       books/raw/mybook.md \
  --book-id    mybook-v1 \
  --title      "My Medical Book" \
  --author     "Author Name" \
  --language   en \
  [--edition   "3rd edition"] \
  [--recreate-collection]
```

| Flag | Required | Description |
|---|---|---|
| `--book` | yes | Path to the preprocessed markdown file |
| `--book-id` | yes | Unique identifier for the book (used for filtering) |
| `--title` | yes | Display title |
| `--author` | yes | Author(s) |
| `--language` | yes | `en` or `bg` |
| `--edition` | no | Edition string |
| `--recreate-collection` | no | Delete and recreate the Qdrant collection (required after schema changes) |

Indexing is resumable — progress is checkpointed per book in SQLite.

### Add a medical dictionary entry

```bash
dotnet run --project MedAssist.Indexer -- dictionary add \
  --icd  J18.9 \
  --en   "Pneumonia, unspecified" \
  --bg   "Пневмония, неуточнена"
```

### Rebuild BM25 vocabulary

Run after bulk re-indexing or if the vocabulary is out of sync:

```bash
dotnet run --project MedAssist.Indexer -- rebuild-vocab
```

---

## Book preparation

The indexer expects markdown, not raw PDF. Use [Docling](https://github.com/DS4SD/docling) to convert scanned or digital PDFs:

```bash
pip install docling
docling convert --input books/raw/mybook.pdf --output books/processed/mybook.md
```

Place the resulting `.md` file in `books/raw/` before running the indexer.

---

## Configuration reference

Settings are loaded in this priority order (highest wins):

1. Environment variables / Docker Compose `environment:` (double-underscore `__` as separator)
2. Per-project `appsettings.{Environment}.json`
3. Per-project `appsettings.json` (logging only)
4. `config/appsettings.shared.json` (infrastructure defaults — **edit this for local dev**)

| Key | Env var | Default | Description |
|---|---|---|---|
| `Database:Path` | `Database__Path` | `medassist.db` | SQLite database file path |
| `Models:Path` | `Models__Path` | `models` | Directory for ONNX model files |
| `VectorStore:Qdrant:Endpoint` | `VectorStore__Qdrant__Endpoint` | `http://localhost:6333` | Qdrant gRPC/HTTP endpoint |
| `AI:ModelProvider` | `AI__ModelProvider` | — | `ollama` (only supported provider) |
| `AI:Ollama:Endpoint` | `AI__Ollama__Endpoint` | — | Ollama base URL |
| `AI:Ollama:ModelName` | `AI__Ollama__ModelName` | — | Model tag, e.g. `qwen2.5:7b` |

---

## Project structure

```
AIDoctorAssistant/
├── MedAssist.Shared/
│   ├── Constants/          VectorStoreConstants, LanguageCodes
│   ├── Interfaces/         IVectorStore, IEmbedder, ISparseVectorizer,
│   │                       IBM25VocabStore, IMedicalDictionary
│   └── Models/             MedicalChunk, SparseVector, BM25VocabSnapshot, …
├── MedAssist.AI/
│   ├── Dictionary/         MedicalDictionaryService, BM25VocabService
│   ├── Embedding/          MultilingualE5Embedder, SparseVectorizer, ModelInitializer
│   ├── Kernel/             KernelFactory
│   ├── Plugins/            RagPluginBase, SymptomsPlugin, DiseasePlugin, TreatmentPlugin
│   │                       WebSearchPlugin
│   └── VectorStore/        QdrantVectorStore
├── MedAssist.Web/
│   ├── Components/         Blazor pages and components
│   ├── Extensions/         ServiceCollectionExtensions, WebApplicationExtensions
│   ├── Services/           QueryService
│   └── Program.cs
├── MedAssist.Indexer/
│   ├── Commands/           CliCommands
│   ├── Database/           DbInitializer
│   ├── Ingestion/          BookIndexer, MarkdownChunker, ChunkEnricher, VocabularyBuilder
│   ├── Repositories/       BookRepository, CheckpointRepository,
│   │                       IllnessDictionaryRepository, BM25VocabRepository
│   └── Program.cs
├── MedAssist.Tests/
│   └── SparseVectorizerTests.cs
├── books/
│   ├── raw/                Input markdown files (git-ignored content, .gitkeep tracked)
│   └── processed/          Docling output (git-ignored content, .gitkeep tracked)
├── docker/
│   ├── prometheus/
│   └── grafana/
├── docker-compose.yml
├── MedAssist.slnx
└── .gitattributes
```

---

## Build and test

```bash
# Build entire solution
dotnet build MedAssist.slnx

# Run tests
dotnet test MedAssist.Tests
```
