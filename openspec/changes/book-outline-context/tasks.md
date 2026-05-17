## Tasks

### 1. Data model — BookEntity
- [ ] Add `public string? Outline { get; set; }` to `BookEntity`

### 2. EF migration
- [ ] Run `dotnet ef migrations add AddBookOutline --project MedAssist.Data --startup-project MedAssist.Web`

### 3. BookRepository — UpdateOutlineAsync
- [ ] Add `UpdateOutlineAsync(string bookId, string outline, CancellationToken ct)` method

### 4. BookIndexer — ExtractOutline helper
- [ ] Add static `ExtractOutline(string markdown)` — regex H1/H2/H3, max 200 headings, indented by depth
- [ ] Call it in `IndexAsync` after markdown is obtained; persist via `UpdateOutlineAsync`

### 5. BookInfo — add Outline field
- [ ] Add `public string? Outline { get; init; }` to `BookInfo` (or whatever model BookCatalogService returns)
- [ ] Populate from `BookEntity.Outline` in `BookCatalogService`

### 6. RagPluginBase — BuildOutlineBlock
- [ ] Add `BuildOutlineBlock(IEnumerable<BookInfo> books)` helper
- [ ] Inject outline block at top of SK prompt template, before retrieved chunks

### 7. Verify build
- [ ] `dotnet build MedAssist.slnx` — zero errors
