## ADDED Requirements

### Requirement: Page markers in book markdown

Book markdown SHALL carry page changes as a single neutral marker format that both converters write and the
chunker reads. A marker SHALL carry a PDF page number and MAY carry a printed page label. Printed page labels
SHALL be text, because they include online-only pages such as `1804.e1`.

#### Scenario: Marker round-trips

- **WHEN** a converter writes a marker for PDF page 2635 with printed page `1804.e1`
- **THEN** the chunker reads PDF page 2635 and printed page `1804.e1` from it

### Requirement: Each chunk gets the page range it spans

The chunker SHALL give every chunk the range of pages its text spans: the page in effect where the chunk
starts, through the last page marker inside it. Overlap text copied from the previous chunk SHALL NOT widen the
range. Page markers SHALL be removed from chunk text before embedding. Markdown with no markers SHALL produce
chunks with unknown pages.

#### Scenario: Chunk crossing a page boundary

- **WHEN** a chunk starts on printed page `1804` and contains the marker for printed page `1805`
- **THEN** the chunk's printed range is `1804`–`1805` and its text contains no marker

#### Scenario: Markdown without markers

- **WHEN** a book's markdown contains no page markers
- **THEN** every chunk is stored with unknown pages

### Requirement: Skipped pages are cited as a PDF range

Where the source does not note every page, a marker SHALL carry the last PDF page its text may run to (`until`):
the page before the next noted page, or the last PDF page of the part. A chunk spanning such a stretch SHALL
have no printed pages and a PDF range running to that page, so its citation is less exact but never wrong.

#### Scenario: Chapter after the last page note

- **WHEN** a part covering PDF 2636–2640 notes PDF 2637 (printed `1805.e1`) and no later page, and chapter 332
  follows that note
- **THEN** chapter 332's chunks cite `PDF pp. 2637–2640`, not `p. 1805.e1`

### Requirement: Pages are stored with each point

Each Qdrant point SHALL store the chunk's PDF page range, and its printed page range when known. Reading a
point that lacks page fields or stores `0` SHALL yield unknown pages and SHALL NOT fail.

#### Scenario: Existing point without printed pages

- **WHEN** a point indexed before this change is retrieved, with `page_start = 0` and no printed fields
- **THEN** the retrieved chunk has unknown pages and the query completes

### Requirement: Citation shows the best known page

A citation SHALL show the printed page range when known (`p. 1804` or `pp. 1804–1805`), otherwise the PDF page
range labelled as such (`PDF p. 2635`), otherwise no page text at all. The same rule SHALL apply to the query
page's source list, the book citation component and the evaluation trace. `0` SHALL never be shown as a page.

#### Scenario: Printed page known

- **WHEN** a cited chunk has printed range `1804`–`1805`
- **THEN** the citation reads `pp. 1804–1805`

#### Scenario: Only PDF page known

- **WHEN** a cited chunk has PDF page 2635 and no printed page
- **THEN** the citation reads `PDF p. 2635`

#### Scenario: Pages unknown

- **WHEN** a cited chunk has unknown pages
- **THEN** the citation shows the book and section with no page text
