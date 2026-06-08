## Capability: outline-prompt-injection

Inject book outlines into the RAG prompt so the LLM can map query terms to section vocabulary before retrieving chunks.

### BookInfo / BookCatalogService

**`MedAssist.Web/Services/BookCatalogService.cs`** (or the `BookInfo` model it uses)
- Add `public string? Outline { get; init; }` to `BookInfo`
- Populate it from `BookEntity.Outline` when building the cached list

### RagPluginBase prompt change

**`MedAssist.AI/Plugins/RagPluginBase.cs`**

After resolving which books to search (already done via `bookIds` parameter), build an outline block:

```csharp
private static string BuildOutlineBlock(IEnumerable<BookInfo> books)
{
    var entries = books
        .Where(b => !string.IsNullOrWhiteSpace(b.Outline))
        .Select(b => $"[{b.Title}]\n{b.Outline}")
        .ToList();

    if (entries.Count == 0) return string.Empty;

    return "=== Book Structure ===\n" + string.Join("\n\n", entries) + "\n======================";
}
```

Inject this block at the top of the SK prompt template, before the retrieved chunks section. If the block is empty (all outlines null), inject nothing — prompt stays unchanged.

### Prompt template change

The outline block is prepended as a system-level hint:

```
{outlineBlock}

Use the book structure above to identify which chapter or section vocabulary is most relevant to the query, then answer based on the retrieved passages below.

=== Retrieved Passages ===
{chunks}
```

### Behaviour

- If a query targets specific books (`bookIds` set), only those books' outlines are injected.
- If no books are specified (search all), all indexed books with non-null outlines are included.
- Outlines are read from the `BookCatalogService` cache — no extra DB query per request.
- The LLM sees bilingual chapter names (e.g. "Болест на Грейвс / Graves Disease") and can bridge the terminology gap before embedding retrieval.
