## Capability: outline-extraction

Extract chapter/section headings from a book's Markdown and persist them as a structured outline on `BookEntity`.

### Data model

**`MedAssist.Data/Entities/BookEntity.cs`**
```csharp
public string? Outline { get; set; }
```

**EF migration** — add nullable `outline` text column to `books` table.

**`MedAssist.Data/Repositories/BookRepository.cs`** — add method:
```csharp
public async Task UpdateOutlineAsync(string bookId, string outline, CancellationToken ct = default)
```
Does an `ExecuteUpdateAsync` on the matching row. If no row found, silently returns.

### Extraction logic

**`MedAssist.AI/Ingestion/BookIndexer.cs`** — add a private static helper:
```csharp
private static string ExtractOutline(string markdown)
```

Implementation:
- Match lines starting with `#`, `##`, or `###` using `Regex.Matches(markdown, @"^(#{1,3})\s+(.+)$", RegexOptions.Multiline)`
- For each match, format as `{depth_indent}{text}` (H1 = no indent, H2 = two spaces, H3 = four spaces)
- Take at most 200 headings
- Join with newline
- Return empty string if no headings found

Call site in `IndexAsync`, after markdown is obtained:
```csharp
var outline = ExtractOutline(markdown);
if (outline.Length > 0)
    await bookRepo.UpdateOutlineAsync(bookId, outline, ct);
```

### Behaviour

- Outline is extracted from the same Markdown used for chunking — no additional Docling call.
- Books indexed before this feature have `Outline = null`; re-indexing populates it.
- Extraction failure does not abort indexing.
