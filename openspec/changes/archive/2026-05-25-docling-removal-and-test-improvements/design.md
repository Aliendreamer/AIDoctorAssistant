## Overview

Cleanup of dead Docling code and targeted improvements to the Marker extraction pipeline and test suite. All changes are already implemented.

## Docling Removal

Docling was the original PDF-to-markdown backend. After Marker replaced it, three artefacts remained:

| Artefact | Action |
|---|---|
| `MedAssist.AI/Ingestion/DoclingClient.cs` | Deleted |
| `MedAssist.Web/Options/DoclingOptions.cs` | Deleted |
| `docker/docling/Dockerfile` | Deleted |
| `MarkdownChunker.cs:16` comment | Updated: "Docling PDF artifact" → "scanned PDF artifact" |
| `cspell.json` | Removed two Docling word entries |

No registration, DI wiring, or docker-compose reference existed — all files were pure dead code.

## MarkdownChunker: Marker Image Ref Stripping

Marker emits image references as `![](_page_N_Picture.jpeg)` lines. These pass through the existing `InlineImageRegex` (which only strips `data:` URIs). A new `MarkerImageRefRegex` strips file-based image refs on their own line before chunking:

```
^\s*!\[[^\]]*\]\([^)]+\.(jpe?g|png|gif|webp|svg)\)\s*$   (Multiline)
```

Both stripping steps happen in `StripInlineImages`, called before any chunking logic.

## MarkerClient: Injectable Poll Interval

`PollInterval` was a `private static readonly` constant (30 s). Changed to an instance field set via an optional constructor parameter:

```csharp
public MarkerClient(HttpClient httpClient, bool useLlm, ILogger<MarkerClient> logger,
    TimeSpan? pollInterval = null)
```

Defaults to 30 s in production (no change to DI registration). Tests pass `TimeSpan.Zero` to eliminate delays without subclassing.

## BulkExtractEndpoint: Smart Skip

Added a filter to the eligibility check: skip a book if its `.md` file exists **and** its last-write time is strictly newer than the `.pdf`:

```csharp
.Where(b => {
    var mdPath = Path.ChangeExtension(b.FilePath, ".md");
    return !File.Exists(mdPath) || File.GetLastWriteTimeUtc(mdPath) <= File.GetLastWriteTimeUtc(b.FilePath);
})
```

Logic: a `.md` written by the old extraction pipeline has the same timestamp as the `.pdf` (both uploaded at the same time). A freshly Marker-extracted `.md` is written today — strictly newer than the original PDF. This distinguishes "needs re-extraction" from "already done".

## Test Fixes and Additions

### Pre-existing RAG Loop Failures

`LowConfidenceScore_RunsFallbackIterations` and `MaxIterations_CappedAtFive` were broken after `MinRetryScore` was added to the RAG pipeline. The test `RagOptions` didn't set the new field, so its default (`1.0f`) caused the CRAG web-fallback branch to fire on every call with a low stub score, exiting before any retry iteration.

Fix: `DefaultOptions` and all affected per-test options now set `MinRetryScore = float.NegativeInfinity` and `MinAnswerScore = float.NegativeInfinity`, isolating iteration behaviour from threshold gating.

### New MarkdownChunker Tests (Marker patterns)

| Test | What it covers |
|---|---|
| `MarkerImageRefs_AreNotProducedAsChunks` | `![](_page_N.jpeg)` lines don't appear in chunk text |
| `Base64InlineImages_AreStripped` | Old Docling `data:image/png;base64,...` blobs are removed |
| `SpacedLetterOcrArtifact_DoesNotCrashChunker` | Spaced-letter OCR lines (`т е ж е с т т а`) don't crash |
| `SoftHyphenArtifacts_AreNormalized` | U+00AD soft hyphens are stripped |
| `MarkerH4Subsections_ProduceCorrectHeadingPath` | `#` + `####` hierarchy produces correct `>` path |

### New MarkerClient Tests

| Test | What it covers |
|---|---|
| `StartConversionAsync_ReturnsJobId` | POST returns job_id correctly |
| `PollStatusAsync_ReturnsMarkdown_WhenStateDone` | Polls until `done`, returns markdown |
| `PollStatusAsync_ThrowsOnFailedState` | `failed` state throws with error message |
| `PollStatusAsync_RetriesOnTransientHttpError` | `HttpRequestException` is swallowed, polling continues |
| `PollStatusAsync_StopsOnCancellation` | `CancellationToken` stops the loop |
| `StartConversionAsync_AppendsUseLlmQueryParam_WhenEnabled` | `use_llm=true` appended when configured |
