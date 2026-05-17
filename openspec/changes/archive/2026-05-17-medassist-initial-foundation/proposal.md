## Why

Medical professionals need fast, reliable access to clinical knowledge from their trusted textbooks — in both Bulgarian and English — without manually searching through hundreds of scanned pages. This project establishes the complete foundation: a RAG-based AI assistant that indexes bilingual medical books and answers clinical queries with precise citations.

## What Changes

- **New solution**: 4-project .NET 10 solution scaffold (`MedAssist.Shared`, `MedAssist.AI`, `MedAssist.Web`, `MedAssist.Indexer`)
- **New capability**: Resumable PDF ingestion pipeline — Docling preprocessing → structural chunking → multilingual embeddings → Qdrant upsert with checkpoint/resume every 50 chunks
- **New capability**: Bilingual RAG query engine with 3 distinct Semantic Kernel plugins (Symptoms, Disease, Treatment), supporting language filter (`both` | `en` | `bg`) and per-book scoping
- **New capability**: Medical terminology dictionary (ICD-10 codes + BG/EN names + aliases) enabling cross-book, cross-language illness referencing
- **New capability**: Optional web search fallback via PubMed API, explicitly opt-in per query
- **New capability**: Blazor Server web UI for medical professionals to submit queries, filter by book/language, and view cited results
- **New infrastructure**: SQLite books registry tracking indexed books and ingestion state; Qdrant collection with structured payload per chunk
- **New infrastructure**: Full Docker Compose stack (Web, Indexer, Qdrant, Ollama/Qwen, Prometheus, Grafana)
- **New infrastructure**: Serilog structured logging + OpenTelemetry traces/metrics across all services

## Capabilities

### New Capabilities

- `solution-scaffold`: .NET 10 solution structure — 2 class libraries, 1 Blazor Server app, 1 Worker/CLI app with shared tooling, editorconfig, docker-compose
- `book-ingestion`: Resumable ingestion pipeline — Docling PDF→markdown, structural chunking by heading hierarchy, multilingual embedding (intfloat/multilingual-e5), Qdrant upsert with checkpoint state in SQLite
- `rag-query`: Semantic Kernel RAG engine — 3 plugins (SymptomsPlugin, DiseasePlugin, TreatmentPlugin), multilingual query expansion via ICD-10 dictionary, per-book and per-language Qdrant filters
- `medical-dictionary`: SQLite illness dictionary with ICD-10 codes, BG/EN names and aliases; used at index time (payload enrichment) and query time (query expansion)
- `web-search`: Optional PubMed web search plugin, explicitly enabled per query; results tagged separately from book results
- `web-ui`: Blazor Server UI — query input, book/language filter selectors, result display with citations (book+page or web source)
- `observability`: Serilog JSON logging + OpenTelemetry (HTTP, outbound, runtime) + Prometheus scrape endpoint + Grafana dashboards across all services

### Modified Capabilities

None — greenfield project.

## Impact

- **New NuGet packages**: `Microsoft.SemanticKernel`, `Qdrant.Client`, `Microsoft.ML.OnnxRuntime`, `Microsoft.ML.Tokenizers`, `Microsoft.Data.Sqlite`, `Serilog.AspNetCore`, OpenTelemetry suite, `OllamaSharp`
- **New Docker services**: qdrant, ollama (Qwen model), prometheus, grafana
- **External dependency**: Docling (offline preprocessing only, not in compose runtime)
- **Storage**: Qdrant volume (vectors), SQLite file (books registry + dictionary + checkpoints), markdown files (processed book output)
- **Target users**: Medical professionals (doctors, residents, students) reading Bulgarian and English medical textbooks
