## Tasks

### 1. Data model — BookEntity
- [x] Add `public string? Outline { get; set; }` to `BookEntity`

### 2. EF migration
- [x] Run `dotnet ef migrations add AddBookOutline --project MedAssist.Data --startup-project MedAssist.Web`

### 3. BookRepository — UpdateOutlineAsync
- [x] Add `UpdateOutlineAsync(string bookId, string outline, CancellationToken ct)` method

### 4. BookIndexer — ExtractOutline helper
- [x] Add static `ExtractOutline(string markdown)` — regex H1/H2/H3, max 200 headings, indented by depth
- [x] Call it in `IndexAsync` after markdown is obtained; persist via `UpdateOutlineAsync`

### 5. BookInfo — add Outline field
- [x] Add `public string? Outline { get; init; }` to `BookInfo` (or whatever model BookCatalogService returns)
- [x] Populate from `BookEntity.Outline` in `BookCatalogService`

### 6. RagPluginBase — BuildOutlineBlock
- [x] Add `BuildOutlineBlock(IEnumerable<BookInfo> books)` helper
- [x] Inject outline block at top of SK prompt template, before retrieved chunks

### 7. Verify build
- [x] `dotnet build MedAssist.slnx` — zero errors
