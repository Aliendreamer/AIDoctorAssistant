## 1. Solution Scaffold

- [x] 1.1 Create `.NET 10` solution file `MedAssist.slnx` at repo root
- [x] 1.2 Create `MedAssist.Shared` class library project targeting `net10.0`
- [x] 1.3 Create `MedAssist.AI` class library project targeting `net10.0`
- [x] 1.4 Create `MedAssist.Web` Blazor Server app project targeting `net10.0`
- [x] 1.5 Create `MedAssist.Indexer` Worker Service project targeting `net10.0`
- [x] 1.6 Add all projects to solution and wire project references (Shared←AI←Web; Shared←Indexer)
- [x] 1.7 Add `.editorconfig` at solution root with C# style rules (nullable, braces, naming)
- [x] 1.8 Configure `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `EnforceCodeStyleInBuild true` in all `.csproj` files
- [x] 1.9 Add `.gitignore` covering `bin/`, `obj/`, `*.user`, `*.db`, `books/raw/`, `books/processed/`
- [x] 1.10 Verify `dotnet build` passes with zero errors and zero warnings

## 2. Docker Compose Infrastructure

- [x] 2.1 Write `docker-compose.yml` with services: `qdrant` (qdrant/qdrant:latest), `ollama` (ollama/ollama:latest), `prometheus`, `grafana`
- [x] 2.2 Add volume mounts for Qdrant data, Ollama models, Prometheus config, Grafana provisioning
- [x] 2.3 Write `Dockerfile` for `MedAssist.Web` (multi-stage, `mcr.microsoft.com/dotnet/aspnet:10.0`)
- [x] 2.4 Write `Dockerfile` for `MedAssist.Indexer` (multi-stage, `mcr.microsoft.com/dotnet/aspnet:10.0`)
- [x] 2.5 Add `web` and `indexer` services to `docker-compose.yml` with correct build contexts
- [x] 2.6 Write `prometheus.yml` scrape config targeting `web:8080/metrics`
- [x] 2.7 Write Grafana provisioning config (`datasources/prometheus.yaml`, `dashboards/medassist.json`)
- [x] 2.8 Verify `docker compose up` starts all services and Qdrant health check passes

## 3. MedAssist.Shared — Domain Models

- [x] 3.1 Create `Models/BookInfo.cs` (id, title, author, language, edition, totalChunks, status, indexedAt)
- [x] 3.2 Create `Models/MedicalChunk.cs` (bookId, bookTitle, author, language, chapterTitle, sectionTitle, pageStart, pageEnd, chunkIndex, contentType, text, icdCodes)
- [x] 3.3 Create `Models/QueryRequest.cs` (query, queryType, language, bookIds, webSearchEnabled)
- [x] 3.4 Create `Models/QueryResult.cs` (answer, sources: list of `SourceCitation`)
- [x] 3.5 Create `Models/SourceCitation.cs` (sourceType: Book|Web, bookTitle, author, chapterTitle, sectionTitle, pageStart, pageEnd, url, sourceName)
- [x] 3.6 Create `Models/IllnessEntry.cs` (id, icdCode, nameEn, nameBg, aliases)
- [x] 3.7 Create `Interfaces/IEmbedder.cs` with `EmbedAsync(string text): Task<float[]>`
- [x] 3.8 Create `Interfaces/IVectorStore.cs` with `UpsertAsync`, `SearchAsync` signatures
- [x] 3.9 Create `Interfaces/IMedicalDictionary.cs` with `ExpandQueryAsync(string query): Task<IReadOnlyList<string>>`

## 4. SQLite Database (MedAssist.Indexer)

- [x] 4.1 Add `Microsoft.Data.Sqlite` NuGet to `MedAssist.Indexer`
- [x] 4.2 Create `Database/DbInitializer.cs` that creates `medassist.db` and runs schema migrations on startup
- [x] 4.3 Write SQL schema for `books` table (id, title, author, language, edition, total_chunks, status, indexed_at)
- [x] 4.4 Write SQL schema for `illnesses` table (id, icd_code UNIQUE, name_en, name_bg, created_at)
- [x] 4.5 Write SQL schema for `illness_aliases` table (id, illness_id FK, alias, language)
- [x] 4.6 Write SQL schema for `ingestion_checkpoints` table (book_id PK, total_chunks, indexed_chunks, last_chunk_index, status, updated_at)
- [x] 4.7 Enable WAL mode on SQLite connection to support concurrent reads from Web service
- [x] 4.8 Create `Repositories/BookRepository.cs` (upsert, getAll, getById)
- [x] 4.9 Create `Repositories/CheckpointRepository.cs` (upsert, getByBookId)
- [x] 4.10 Create `Repositories/IllnessDictionaryRepository.cs` (add, findByName, expandQuery)

## 5. Multilingual Embedder (MedAssist.AI)

- [x] 5.1 Add `Microsoft.ML.OnnxRuntime`, `Microsoft.ML.Tokenizers` NuGet to `MedAssist.AI`
- [x] 5.2 Download `intfloat/multilingual-e5-large` ONNX model and tokenizer files to `models/multilingual-e5-large/`
- [x] 5.3 Create `Embedding/MultilingualE5Embedder.cs` implementing `IEmbedder` using ONNX Runtime
- [x] 5.4 Add E5 query prefix logic (`"query: "` prefix for queries, `"passage: "` prefix for passages)
- [x] 5.5 Register `IEmbedder` as singleton in DI
- [x] 5.6 Write unit test verifying embedding output is 1024-dimensional float array

## 6. Qdrant Vector Store (MedAssist.AI)

- [x] 6.1 Add `Qdrant.Client` NuGet to `MedAssist.AI`
- [x] 6.2 Create `VectorStore/QdrantVectorStore.cs` implementing `IVectorStore`
- [x] 6.3 Implement `UpsertAsync` — create collection `medical_books` if not exists (cosine distance, 1024 dims), upsert vector with full payload
- [x] 6.4 Implement `SearchAsync` — accept query vector, language filter, bookIds filter, top-k (default 5); return `List<MedicalChunk>`
- [x] 6.5 Add Qdrant connection string to `appsettings.json` (`VectorStore:Qdrant:Endpoint`)
- [x] 6.6 Register `IVectorStore` as singleton in DI

## 7. Semantic Kernel Setup (MedAssist.AI)

- [x] 7.1 Add `Microsoft.SemanticKernel` NuGet to `MedAssist.AI`
- [x] 7.2 Add `OllamaSharp` NuGet to `MedAssist.AI`
- [x] 7.3 Create `KernelFactory.cs` that builds `IKernel` from `appsettings.json` `AI:ModelProvider` config
- [x] 7.4 Implement Ollama provider path in `KernelFactory` (model name, endpoint from config)
- [x] 7.5 Add `AI:ModelProvider`, `AI:Ollama:Endpoint`, `AI:Ollama:ModelName` to `appsettings.json`
- [x] 7.6 Register kernel and plugins in DI

## 8. SK Plugins (MedAssist.AI)

- [x] 8.1 Create `Plugins/SymptomsPlugin.cs` with `[KernelFunction] SearchAsync(query, language, bookIds)` — finds likely diagnoses from symptoms
- [x] 8.2 Create `Plugins/DiseasePlugin.cs` with `[KernelFunction] SearchAsync(query, language, bookIds)` — returns clinical disease information
- [x] 8.3 Create `Plugins/TreatmentPlugin.cs` with `[KernelFunction] SearchAsync(query, language, bookIds)` — returns treatment options
- [x] 8.4 Implement shared RAG logic in `Plugins/RagPluginBase.cs`: expand query via `IMedicalDictionary`, embed, search Qdrant, build `QueryResult`
- [x] 8.5 Create `Plugins/WebSearchPlugin.cs` with `[KernelFunction] SearchAsync(query, language)` — queries PubMed E-utilities API
- [x] 8.6 Write PubMed query builder: construct `esearch.fcgi` + `efetch.fcgi` calls, parse XML response into `SourceCitation` list
- [x] 8.7 Implement plugin registration logic: always register Symptoms/Disease/Treatment; register WebSearch only when `QueryRequest.WebSearchEnabled = true`

## 9. Medical Dictionary Service (MedAssist.AI)

- [x] 9.1 Create `Dictionary/MedicalDictionaryService.cs` implementing `IMedicalDictionary`
- [x] 9.2 Implement `ExpandQueryAsync` — query `illnesses` + `illness_aliases` tables, return all matching BG/EN names and aliases
- [x] 9.3 Register `IMedicalDictionary` in DI pointing to shared `medassist.db` path
- [x] 9.4 Add CLI command to `MedAssist.Indexer`: `dictionary add --icd <code> --en <name> --bg <name>`

## 10. Book Ingestion Pipeline (MedAssist.Indexer)

- [x] 10.1 Create `books/raw/`, `books/processed/` directories with `.gitkeep`
- [x] 10.2 Create `Ingestion/MarkdownChunker.cs` — split markdown by heading hierarchy, respect 512 token limit, merge chunks < 50 tokens
- [x] 10.3 Create `Ingestion/ChunkEnricher.cs` — match chunk text against `IllnessDictionaryRepository` and populate `icd_codes`
- [x] 10.4 Create `Ingestion/BookIndexer.cs` — orchestrates: load checkpoint → read markdown → chunk → enrich → embed → upsert → save checkpoint every 50 chunks → update books registry
- [x] 10.5 Add CLI command `index --book <markdown-file> --book-id <id> --title <title> --author <author> --language <bg|en>` to trigger indexing
- [x] 10.6 Implement checkpoint resume logic: on startup load `ingestion_checkpoints`, skip chunks with index ≤ `last_chunk_index`
- [x] 10.7 Test resumable ingestion: index 60 chunks, kill process, restart, verify resumes from chunk 51

## 11. Blazor Web UI (MedAssist.Web)

- [x] 11.1 Configure Blazor Server with DI wiring for `MedAssist.AI` services and `IVectorStore`
- [x] 11.2 Create `Pages/Query.razor` — main query page with text area, query type selector (Symptoms/Disease/Treatment), language filter, book filter, web search toggle, submit button
- [x] 11.3 Implement book filter population — call `BookRepository.GetAllAsync()` on page load, bind to multi-select
- [x] 11.4 Implement query submission — build `QueryRequest`, invoke correct SK plugin, display `QueryResult`
- [x] 11.5 Create `Components/ResultCard.razor` — displays answer text + citation block (book or web badge)
- [x] 11.6 Create `Components/BookSourceCitation.razor` — shows book title, author, chapter, section, page range
- [x] 11.7 Create `Components/WebSourceCitation.razor` — shows "PubMed" badge, article title, URL
- [x] 11.8 Add loading spinner during query execution
- [x] 11.9 Add error display for failed queries (Qdrant unreachable, model timeout, etc.)

## 12. Observability Wiring

- [x] 12.1 Add Serilog packages to `MedAssist.Web` and `MedAssist.Indexer` (`Serilog.AspNetCore`, `Serilog.Enrichers.Environment`, `Serilog.Formatting.Compact`)
- [x] 12.2 Configure Serilog JSON stdout output in `Program.cs` for both services
- [x] 12.3 Add OpenTelemetry packages to both services (AspNetCore, Http, Runtime instrumentation + Prometheus exporter)
- [x] 12.4 Configure OTel in `Program.cs` for `MedAssist.Web`: register `query_duration_seconds` histogram by plugin type, `qdrant_results_total` counter
- [x] 12.5 Configure OTel in `MedAssist.Indexer`: register `indexer_chunks_total` counter
- [x] 12.6 Expose `/metrics` Prometheus endpoint in `MedAssist.Web`
- [x] 12.7 Write Grafana dashboard JSON with panels: request rate, query duration by plugin, Qdrant latency, indexer chunk throughput
- [x] 12.8 Verify end-to-end: submit a query, confirm trace spans appear for Qdrant + Ollama calls
