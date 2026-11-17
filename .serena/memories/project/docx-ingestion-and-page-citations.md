# Word-part ingestion and page citations

**Type:** project (change `ingest-docx-and-page-citations`, 2026-11-17)

The Bulgarian Nelson Textbook of Pediatrics (20th ed.) arrives as zips of 5-page `.docx` parts, one book
`nelson-20-bg`. `DocxBookConverter` reads them in process (OpenXml, no OCR); upload accepts a zip of parts, stored in
`Books:DocxPath/<bookId>/`, and every index regenerates the markdown from all parts.

## Page markers

- Both converters write `<!-- page pdf=N printed=X until=M -->` into the markdown; only `MarkdownChunker` reads it.
- `printed` is text (`1805.e1` online-only pages). `until` marks pages the translator skipped: such chunks cite
  `PDF pp. N–M` instead of a possibly wrong printed page (89% of Nelson chunks keep a printed page).
- MinerU PDFs get PDF pages from `content_list` `page_idx`; the 8 older books have no pages until re-extracted.

## Translator conventions (`DocxConventions`)

- Page lines vary in wording (`PDF стр. N · печатна стр. N`, `Оригинал: PDF N · отпечатана стр. N`, …); match by
  text, not style. Heading styles use locale ids `1`/`21`/`31`, resolved through `styles.xml` names.
- Strong banners (`Част XVIII —`, lines opening with the book name) are dropped anywhere; opening banners only
  before content. Meta-notes (`Контролна граница`, `Бележка за продължение…`) are dropped; `Източник:` is kept.
- Order is Part numeral, then file prefix (`41.2` between 41 and 42): prefixes restart in each zip.
- `DocxRealPartsFixtureTests` runs the converter over the real zips; extend its patterns when a new Part arrives.

## Gotcha found on the way

The OCR clean-up joined `макро- и микро-` into `макрои`; suspended hyphens before `и/или/and/or` are now kept
(632 cases in the old books, fixed on their next re-index).

Related: `mem:project/evaluation-baseline-findings`.
