## Why

Section-aware candidate expansion was designed to recover fragmented content by pulling all chunks from a relevant section once a summary chunk is found. However the current implementation expands from **every candidate** — not just summary hits. When the initial dense search returns one chunk from a wrong section, expansion floods the reranking pool with 50 more wrong chunks from that section, burying the correct content. Testing 13 specific pediatric queries shows only 3 clear passes vs the expected 8+.

## What Changes

- `ExpandBySectionAsync` in `RagPluginBase` is restricted to only expand from sections where a **summary chunk** (`IsSummary = true`) was found in the candidate set
- Regular (non-summary) candidate chunks no longer trigger section expansion
- Summary chunks continue to be filtered out of the final answer (existing behaviour preserved)
- If no summary chunks are found among candidates, no scroll expansion happens — the pool stays as-is from the initial search

## Capabilities

### New Capabilities

- None

### Modified Capabilities

- `section-aware-retrieval`: The expansion trigger condition changes from "any candidate in section X" to "a summary chunk from section X was found" — the spec requirement for summary-triggered expansion is now correctly enforced

## Impact

- `MedAssist.AI/Plugins/RagPluginBase.cs` — `ExpandBySectionAsync` logic change (3-5 lines)
- No schema changes, no migration, no container rebuild required if hot-reload is available; otherwise one container rebuild needed
- No test changes required (existing stubs already return empty from `ScrollSectionAsync`)
