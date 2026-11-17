## MODIFIED Requirements

### Requirement: Synchronous PDF-to-markdown conversion via MinerU

The ingestion pipeline SHALL convert a source PDF to markdown with a single synchronous request to
the shared MinerU service. The client SHALL `POST {ServiceUrl}/file_parse` as `multipart/form-data`
with the PDF under the `files` field and the fields `backend`, `parse_method`, `return_md=true` and
`return_content_list=true`. The client SHALL build the markdown from the content list in
`results.<firstKey>.content_list`, writing a page marker for the 1-based PDF page wherever a block's
`page_idx` changes. If the response has no usable content list, the client SHALL fall back to the
markdown in `results.<firstKey>.md`, without page markers.

#### Scenario: Successful conversion returns markdown

- **WHEN** a valid PDF path is passed to `MinerUClient.ConvertToMarkdownAsync`
- **THEN** the client POSTs the PDF to `/file_parse` with `return_md=true` and `return_content_list=true`
- **AND** returns markdown built from `results.<firstKey>.content_list`

#### Scenario: Configured backend and method are sent

- **WHEN** the client submits a conversion with `Backend=pipeline` and `Method=ocr`
- **THEN** the multipart form includes `backend=pipeline` and `parse_method=ocr`

#### Scenario: Page markers follow page_idx

- **WHEN** the content list has blocks with `page_idx` 0, 0 and 1
- **THEN** the markdown holds a marker for PDF page 1 before the first block and for PDF page 2 before the third

#### Scenario: Content list missing

- **WHEN** the response has `md` but no `content_list`
- **THEN** the client returns the `md` markdown with no page markers
