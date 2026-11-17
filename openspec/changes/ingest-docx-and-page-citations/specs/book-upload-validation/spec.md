## ADDED Requirements

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
