# Design — inline citation markers

## The mapping guarantee (why this is safe)

The whole design rests on one invariant already present in the code:

- In `RagPluginBase.BuildResultAsync`, `sources` is built by projecting the final `chunks` list in
  order, and the `context` string is built by iterating **the same `chunks` in the same order**.
- In `QueryService.ExecuteAsync`, when web results are merged the code does
  `[.. bookResult.Sources, .. webSources]` — book sources keep their positions; web sources are
  appended.

Therefore, if the excerpts are numbered `[1..N]` in `chunks` order and the model cites with those
numbers, **marker `n` maps to `sources[n-1]`** in every path. No sentence-to-source inference, no
post-hoc alignment. The numbering is the contract.

## Excerpt numbering

In `BuildResultAsync`, change the context builder from:

```
[Book — Chapter › Section]
text…
```

to a 1-based numbered form, e.g.:

```
[1] (Book — Chapter › Section)
text…
```

The number is the excerpt's position in `chunks`, which equals its position (+1) in `sources`.

## Prompt citation rule

Three prompts generate book-grounded answers: the shared `RagPluginBase.GetSystemPrompt` (used by
Disease, Symptoms, Treatment) and the `GlobalSearchPlugin` / `DifferentialDiagnosisPlugin`
overrides. Each gains a rule of the form:

> Support factual clinical claims by appending the number(s) of the excerpt(s) that back them in
> square brackets, e.g. `[1]` or `[2][4]`, placed after the claim. Cite only excerpts that actually
> support the specific statement, and never invent a number that isn't in the provided excerpts. Do
> not add a marker to general or connective sentences.

The existing prose rules (continuous sentences, no lists, no markdown, same language as the
question) stay. The few-shot EXAMPLE ANSWERs in the base and Differential prompts are updated to
show `[n]` markers appended mid-prose, so the model mimics cited prose rather than marker-free
prose. The "weave source references naturally" line is reconciled — natural mention is still fine,
but the bracketed number is the machine-readable anchor.

**Conservative-vs-full example rewrite** is a knob the implementer can dial: minimal example edits
drift the style less but make emission less reliable; fuller example rewrites make markers more
reliable. Default: update the examples enough to demonstrate the pattern clearly.

## Web paths

- `EnrichWithWebAsync` synthesises a new answer from the book answer (which now contains `[n]`) plus
  web excerpts. Add one instruction: *"Preserve any `[n]` citation markers from the book answer
  exactly as they appear; do not renumber or remove them."* Because book sources keep indices
  `1..K` in the merged list, preserved markers remain correct. Web sources are cited by title as
  today.
- `AnswerFromWebAsync` (pure web) is unchanged — no markers, cite by title.

## MarkdownStripper

`Strip()` must not eat `[n]`. It currently strips `<think>`, headings, bold/italic, and line-start
`-`/`*`/`N.` list markers. A bracketed `[1]` matches none of these (`\[1\]` is not `1. `). Confirm
with a test; no code change expected. If the model ever emits a *line* that starts with `[1]` as a
faux list, that's still fine — the bullet/numbered regexes require `-`, `*`, or `N.`, not `[N]`.

## Rendering

A pure helper segments the answer for a given message:

```
IEnumerable<AnswerSegment> Segment(string content, int sourceCount)
```

- Regex `\[\s*\d+(?:\s*,\s*\d+)*\s*\]` finds a bracket group of one or more comma-separated numbers.
- Each number in the group becomes its own marker segment **iff** `1 ≤ n ≤ sourceCount`; otherwise
  the original bracket text is emitted as a plain-text segment (range guard — never mislead).
- Text between matches is emitted as plain-text segments.
- If `sourceCount == 0` (e.g. history-loaded answer with no in-session sources), the whole content
  is one plain-text segment — markers show as literal text.

In `Query.razor`, the answer body iterates segments: plain text renders as text (preserving the
`white-space: pre-wrap` layout); a marker renders as
`<sup class="cite" title="@sourceLabel(n)">@n</sup>`. `sourceLabel(n)` is the book title (+ page)
or web title from `sources[n-1]`, so hovering a marker names its source.

### Rendering notes

- Markers are visual + tooltip only in v1 (no scroll-to-source). This avoids anchor-jump quirks
  inside the scrolling chat container and keeps the change small; the numeric tie to the numbered
  source list carries the meaning.
- Consecutive `[1][2]` (two bracket groups) yields two adjacent markers; `[1, 2]` (one group)
  yields two markers from one match — both render the same way.

## Reliability & failure modes

- Local model under-cites → fewer markers; answer still reads as prose. Acceptable.
- Local model over-cites / invents numbers → out-of-range guard renders them as plain text. Safe.
- Model emits markers in the wrong place → a citation may point at a less-relevant-but-real source;
  bounded by the range guard, and the source list itself is unchanged. This is the main
  quality risk and is why marker *placement* accuracy is a prompt-tuning concern, tracked in tasks.

## Accessibility

- `.cite` markers are non-interactive `<sup>` with a `title`; they don't add tab stops or trap
  focus. Contrast of the marker color on the answer surface meets AA.
