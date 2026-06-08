## Context

The current PDF extraction pipeline calls a Docling HTTP service (async task pattern: submit → poll → fetch). Docling's OCR model mis-encodes Cyrillic characters, producing Portuguese diacritics inside Bulgarian words. The extracted markdown is cached on disk as `<bookId>.md` and fed into `MarkdownChunker`. Everything downstream of that file is unaffected by the extractor swap.

## Goals / Non-Goals

**Goals:**
- Replace Docling with Marker as the PDF-to-markdown extractor
- Support optional LLM-assisted correction (Qwen2.5-VL via Ollama) controlled by a config flag
- Keep the contract unchanged: PDF in → markdown string out

**Non-Goals:**
- Changing `MarkdownChunker`, `BookIndexer`, or any RAG component
- Removing the 5 OCR normalization regexes during this change (remove only after corpus is validated)
- Re-indexing existing books (separate manual step after validation)

## Decisions

**Single synchronous POST vs async poll**
Marker processes PDFs faster than Docling for most inputs. A synchronous `/convert` endpoint avoids the polling loop, simplifying `MarkerClient` to a single HTTP call. Timeout is handled via `HttpClient.Timeout` (configurable, default 30 min).

**LLM correction via Ollama (not Gemini Flash)**
Marker defaults to Gemini Flash API for `--use_llm`. We configure it to use Ollama's OpenAI-compatible endpoint (`http://ollama:11434/v1`) with Qwen2.5-VL instead — no external API key, no data leaving the server. Disabled by default (`UseLlm: false`); enable in config to activate.

**GPU base image: `pytorch/pytorch:2.3.1-cuda12.1-cudnn8-runtime`**
Marker uses PyTorch under the hood. Using the official PyTorch CUDA image avoids manual CUDA setup. Image is large (~4GB) but only built once.

**Models loaded at startup via FastAPI lifespan**
`load_all_models()` is called once on startup and held in memory. Avoids re-loading per request, which is expensive (~30s). Service is not healthy until models are loaded.

## Risks / Trade-offs

- [Marker API changes between versions] → Pin `marker-pdf>=1.6.0` in requirements.txt; test after dependency updates
- [Large Docker image build time] → First build is slow; subsequent builds use layer cache
- [Qwen2.5-VL not pulled in Ollama] → `UseLlm` stays false by default; pull `qwen2.5vl:7b` manually before enabling
- [Cached `.md` files from Docling] → Existing cached markdown is reused as-is; delete cache files to force re-extraction with Marker

## Migration Plan

1. Build and start the `marker` container (`docker-compose up --build marker`)
2. Test conversion on 2–3 books with known Cyrillic artifacts
3. Compare markdown quality vs existing cached Docling output
4. If quality is good: delete cached `.md` files for affected books and re-trigger indexing
5. Rollback: switch back to `main` branch; Docling service and client are fully intact there
