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

### Requirement: Word-part zip upload is validated before anything is stored

A zip upload of Word parts SHALL be accepted only if it starts with the zip signature and every file entry
ends in `.docx`; directory entries are ignored. Each entry SHALL be reduced to its file name, and names that
are empty, contain path separators or `..`, or contain control characters SHALL be rejected. The number of
entries, the size of each entry and the total uncompressed size SHALL be bounded, with the uncompressed size
measured while extracting rather than trusted from the zip headers. A rejected zip SHALL store no parts.

#### Scenario: Zip containing a non-docx entry is rejected

- **WHEN** an uploaded zip contains `notes.txt` beside `.docx` parts
- **THEN** the request is rejected and no part is stored

#### Scenario: Traversal in an entry name is rejected

- **WHEN** an entry is named `../../etc/x.docx`
- **THEN** the request is rejected and nothing is written outside the book's parts directory

#### Scenario: Oversized expansion is rejected

- **WHEN** an entry expands beyond the per-entry or total uncompressed limit
- **THEN** extraction stops, the request is rejected and the partially written parts are removed

#### Scenario: Valid Nelson zip is accepted

- **WHEN** the 79-part `Храносмилателна система` zip is uploaded for `nelson-20-bg`
- **THEN** its 79 `.docx` parts are stored in the book's parts directory

