## Context

`ExpandBySectionAsync` currently collects all unique `(chapterTitle, sectionTitle, bookId)` tuples from the full candidate pool and scrolls up to 50 chunks per section. With an initial `topK=5` search and multiple expanded query terms, the candidate pool can already contain 10–20 chunks spanning 5–10 different sections. Expanding all of them adds up to 500 extra chunks — almost all from wrong sections — before the cross-encoder reranker ever runs.

Section summary chunks (`IsSummary = true`) were indexed precisely to act as high-quality entry points: each summary's text is `"{HeadingPath}\n\n{first 800 chars}"`, so its dense vector directly represents the section topic. If the dense search retrieves a summary chunk for section X, it is strong evidence that section X is relevant. Non-summary chunks from wrong sections landing in the pool is much weaker signal and should not trigger expansion.

## Goals / Non-Goals

**Goals:**
- Restrict scroll expansion to sections confirmed by a summary chunk hit
- Eliminate pool contamination from wrong-section expansion
- No re-index required; no schema changes

**Non-Goals:**
- Changing how summary chunks are created or indexed
- Fixing the Cyrillic/Latin script mismatch for Graves disease (separate problem)
- Tuning `topK`, confidence thresholds, or retry strategies

## Decisions

### Filter candidates to summary chunks before deriving expansion sections

**Change:**
```csharp
// Before (expands from all candidates)
var sections = candidates
    .Where(c => !string.IsNullOrEmpty(c.SectionTitle))
    .Select(c => (c.ChapterTitle, c.SectionTitle, c.BookId))
    .Distinct().ToList();

// After (expands only from summary hits)
var sections = candidates
    .Where(c => c.IsSummary && !string.IsNullOrEmpty(c.SectionTitle))
    .Select(c => (c.ChapterTitle, c.SectionTitle, c.BookId))
    .Distinct().ToList();
```

**Why:** Summary chunks are the designed trigger for expansion. Restricting to them preserves the intended architecture and eliminates wrong-section amplification.

**Alternative considered:** Score-gate expansion by reranking first, then expanding only from sections with score > threshold. Rejected for now — adds complexity and a full reranker pass before expansion; summary-gating is simpler and sufficient.

**Alternative considered:** Remove expansion entirely. Rejected — expansion correctly recovers multi-chunk sections when the summary is found; removing it loses that benefit.

## Risks / Trade-offs

- **If no summary is retrieved for the right section**: expansion doesn't happen and we rely on whatever regular chunks the search returned. This is strictly no worse than the current wrong-expansion behaviour and matches the pre-expansion baseline.
- **Summary retrieval quality**: depends on the dense embedding model finding the summary. For Bulgarian medical queries this already works for several sections (жълтеница, астма, нефротичен синдром are all in the summary list).

## Migration Plan

1. Change one `Where` filter in `RagPluginBase.cs`
2. Build → container rebuild → no re-index needed
3. Re-run 13-query test to confirm improvement
