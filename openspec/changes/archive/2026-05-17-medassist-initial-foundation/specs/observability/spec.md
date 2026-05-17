## ADDED Requirements

### Requirement: All services emit structured JSON logs via Serilog
`MedAssist.Web` and `MedAssist.Indexer` SHALL use `Serilog.AspNetCore` configured to write structured JSON logs to stdout. Log entries SHALL include: timestamp, level, message, service name, trace ID (when available). Minimum log level SHALL be configurable via `appsettings.json`.

#### Scenario: Log output is valid JSON
- **WHEN** a request is processed by MedAssist.Web
- **THEN** a JSON log entry is written to stdout with all required fields

### Requirement: OpenTelemetry traces cover HTTP and outbound calls
Both `MedAssist.Web` and `MedAssist.Indexer` SHALL configure OpenTelemetry with `AspNetCore`, `Http`, and `Runtime` instrumentation. Traces SHALL be exported to the configured OTLP endpoint (default: Prometheus-compatible).

#### Scenario: Incoming HTTP request generates a trace
- **WHEN** a query is submitted via the Web UI
- **THEN** a trace spanning the full request lifecycle (UI → AI plugin → Qdrant) is recorded

### Requirement: Prometheus scrape endpoint is exposed
`MedAssist.Web` SHALL expose `/metrics` using `OpenTelemetry.Exporter.Prometheus.AspNetCore`. The endpoint SHALL include standard ASP.NET Core metrics plus custom metrics: `query_duration_seconds` (histogram by plugin type), `qdrant_results_total` (counter), `indexer_chunks_total` (counter, Indexer only).

#### Scenario: Prometheus scrapes the metrics endpoint
- **WHEN** Prometheus scrapes `http://web:8080/metrics`
- **THEN** response contains valid Prometheus text format with at least `query_duration_seconds` metric

### Requirement: Grafana is pre-configured with a dashboard
The `docker-compose.yml` SHALL mount a Grafana provisioning configuration that loads at least one dashboard covering: request rate, query duration by plugin type, Qdrant query latency, indexer chunk throughput.

#### Scenario: Grafana dashboard loads on startup
- **WHEN** `docker compose up` completes and Grafana is accessed
- **THEN** the provisioned dashboard is present and displays data after at least one query is made

### Requirement: Ollama and Qdrant call durations are traced
Outbound HTTP calls from `MedAssist.AI` to Ollama and Qdrant SHALL appear as child spans in the OpenTelemetry trace for each query.

#### Scenario: Qdrant call appears as child span
- **WHEN** a plugin executes a Qdrant search
- **THEN** the trace includes a child span with operation name containing "qdrant"
