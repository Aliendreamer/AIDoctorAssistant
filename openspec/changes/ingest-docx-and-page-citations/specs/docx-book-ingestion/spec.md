## ADDED Requirements

### Requirement: Word parts are stored per book and accumulate across uploads

An admin SHALL be able to upload a zip of `.docx` parts for a book. The parts SHALL be stored in a directory
belonging to that book, contained within the configured books directory. Uploading another zip for the same
book SHALL add its parts to the stored set; a part whose file name already exists SHALL be replaced. The
book SHALL be marked `Pending` after the upload, as a PDF upload is.

#### Scenario: Second Part is added to the same book

- **WHEN** a zip with Part XVIII is uploaded for book `nelson-20-bg`, then a zip with Parts VI–VII
- **THEN** the book's parts directory holds the files of both zips
- **AND** the book status is `Pending`

#### Scenario: Re-uploaded part replaces the earlier copy

- **WHEN** a zip contains a part whose file name is already stored for the book
- **THEN** the stored part is replaced by the new one and no duplicate is kept

### Requirement: Parts are ordered by Part number and prefix

Parts SHALL be concatenated in reading order. A file name of the form `<prefix>-Nelson_<Part>_…` SHALL sort by
the Part's Roman numeral, then by its numeric prefix (`41.2` sorts between `41` and `42`); prefixes restart in
each Part, so the Part comes first. Names without that form SHALL sort after them by the first PDF page in the
name (`…_PDF<start>-<end>`), then by name.

#### Scenario: Parts from two zips are ordered by Part

- **WHEN** the stored parts are Part XVIII (PDF 2546–2925) and Parts VI–VII (PDF 426–587)
- **THEN** the markdown contains Part VI, then Part VII, then Part XVIII, in increasing PDF page order

#### Scenario: Part without a range keeps its place

- **WHEN** a zip holds `1-Nelson_XVIII_305_BG.docx` and `3-Nelson_XVIII_BG_PDF2551-2555.docx`
- **THEN** the chapter opener `1-…` appears before the `3-…` part
- **AND** `41.2-…` appears after `41-…` and before `42-…`

### Requirement: Word structure is converted to markdown

The converter SHALL map Word heading styles to markdown headings. It SHALL recognise both the English style
ids (`Heading1`–`Heading4`, `Title`) and the Bulgarian-locale numeric ids (`1`, `21`, `31`, `4`) that Word
writes for the same built-in headings, resolved through the document's style definitions. Tables SHALL be
converted to markdown tables, and figure and table captions SHALL be kept as text. Images SHALL be omitted.

#### Scenario: Bulgarian-locale heading becomes a markdown heading

- **WHEN** a paragraph uses style id `21`, defined as the built-in Heading 2
- **THEN** it is written as a `##` markdown heading

#### Scenario: Table is preserved

- **WHEN** a part contains a Word table
- **THEN** the markdown contains the table's rows and cells as a markdown table

### Requirement: Translator headers and meta-notes are removed

The converter SHALL drop the translator's per-file header lines (the Part/Section banner and the "translation
of Nelson Textbook of Pediatrics … PDF pages …" line, in all their wording variants) and translator meta-notes
such as progress boundaries (`Контролна граница: …`) and continuation notes (`Бележка за продължението: …`).
Source attributions (`Адаптирано от …`, `Източник: …`) and medical content SHALL be kept.

#### Scenario: Header banner does not reach a chunk

- **WHEN** a part begins with `ЧАСТ XVIII — ХРАНОСМИЛАТЕЛНА СИСТЕМА` and a translation-source line
- **THEN** neither line appears in the markdown

#### Scenario: Source attribution is kept

- **WHEN** a table is followed by `Източник: Black RE, Victora CG, …`
- **THEN** that line remains in the markdown

### Requirement: Page markers come from translator page lines

The converter SHALL recognise the translator's page lines in their wording variants, for example
`PDF стр. 2635 · печатна стр. 1804`, `Оригинал: PDF 2635 · отпечатана стр. 1804`,
`PDF страница 2635 · Отпечатана страница 1804`, including online-only printed pages such as `1804.e1` and lines
stating that no printed number is visible. Each recognised line SHALL be replaced by a page marker carrying the
PDF page and, when present, the printed page. A part with no recognised page line SHALL get a marker for the
first PDF page in its file name. A part with neither SHALL inherit the page in effect from the preceding part.

#### Scenario: Printed and PDF page recognised

- **WHEN** a part contains the line `Оригинал: PDF 2635 · отпечатана стр. 1804`
- **THEN** the markdown holds a page marker with PDF page 2635 and printed page `1804`
- **AND** the original line is not in the markdown

#### Scenario: No visible printed number

- **WHEN** a line reads `PDF стр. 530 · без видим собствен печатен номер · …`
- **THEN** the page marker carries PDF page 530 and no printed page

#### Scenario: Filename fallback

- **WHEN** part `23-Nelson_XVIII_BG_PDF2646-2650.docx` has no page line
- **THEN** a page marker for PDF page 2646 is placed at the start of that part

### Requirement: Indexing a Word book regenerates its markdown from all parts

When a book stored as Word parts is indexed, the worker SHALL regenerate the book's markdown from all stored
parts, overwriting any cached markdown, and SHALL index that markdown. No OCR request SHALL be made. Adding a
Part and indexing with `force=true` SHALL replace the book's points through the single-book force re-index.

#### Scenario: Index after adding a Part covers both Parts

- **WHEN** Parts VI–VII are added to an indexed `nelson-20-bg` and the book is indexed with `force=true`
- **THEN** the book's Qdrant points cover both Parts and no stale points from the earlier index remain
- **AND** MinerU is not called
