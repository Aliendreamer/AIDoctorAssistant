# marker-ocr-service Specification

## Purpose
RETIRED. The self-hosted Marker OCR service was removed in favor of the shared MinerU service (see
the archived `migrate-marker-to-mineru` change and the `mineru-ocr-service` capability). This
capability no longer exists in the system.

## Requirements

### Requirement: Marker OCR service is retired
The application SHALL NOT build or run a self-hosted Marker OCR service; PDF→markdown conversion is
provided by `mineru-ocr-service` (the shared MinerU service).

#### Scenario: No Marker service in the stack
- **WHEN** the compose stack is built
- **THEN** there is no Marker container, and OCR is served by the shared MinerU service
