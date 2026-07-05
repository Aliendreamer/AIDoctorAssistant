# pipeline-observability Specification

## Purpose
TBD - created by archiving change audit-remediation. Update Purpose after archive.
## Requirements
### Requirement: Ingestion emits a chunk-throughput metric

The indexer SHALL emit the `indexer_chunks_total` counter as chunks are processed, and the meter
that owns it SHALL be registered with the OpenTelemetry metrics provider so it is exported.

#### Scenario: Indexing increments the counter

- **WHEN** a book is indexed
- **THEN** `indexer_chunks_total` increases and is visible to the metrics exporter

#### Scenario: Owning meter is registered

- **WHEN** the OpenTelemetry metrics pipeline is configured
- **THEN** the meter that emits AI/indexer counters is included in `AddMeter(...)`

### Requirement: Custom tracing source is either used or removed

If an `ActivitySource` is registered via `AddSource`, the RAG pipeline stages SHALL create spans on
it; otherwise the unused registration SHALL be removed so tracing configuration reflects reality.

#### Scenario: Registered source produces spans

- **WHEN** a query is processed and a custom `ActivitySource` is registered for tracing
- **THEN** spans for the retrieval/rerank/answer stages are emitted on that source

