# book-upload-validation Specification

## Purpose
TBD - created by archiving change audit-remediation. Update Purpose after archive.
## Requirements
### Requirement: BookId is allowlisted and all derived paths are contained

`BookId` SHALL be validated against `^[a-z0-9][a-z0-9-]{0,63}$` before any filesystem use. Every
path derived from `BookId` (uploaded `.pdf`, cached `.md`, and the path forwarded to the Marker
service) SHALL be resolved with `Path.GetFullPath` and asserted to remain within its configured
base directory. A value that escapes the base directory SHALL be rejected before any file I/O.

#### Scenario: Traversal attempt is rejected

- **WHEN** an upload or index request supplies `BookId = "../../etc/passwd"`
- **THEN** the request is rejected with a validation error and no file is read or written

#### Scenario: Valid id is accepted

- **WHEN** `BookId = "harrison-21"`
- **THEN** the derived paths resolve inside the books base directory and processing proceeds

### Requirement: Uploaded file is validated as a PDF within a bounded size

An uploaded book file SHALL be validated to begin with the `%PDF-` magic bytes and to be within a
realistic maximum size before it is persisted to the books directory.

#### Scenario: Non-PDF upload is rejected

- **WHEN** a file whose contents do not start with `%PDF-` is uploaded
- **THEN** the request is rejected and the file is not stored in the books directory

