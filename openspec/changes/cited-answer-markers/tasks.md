## 1. Excerpt numbering

- [ ] 1.1 In `RagPluginBase.BuildResultAsync`, number the context excerpts `[1]`, `[2]`, … in
      `chunks` order (the same order `sources` is projected), e.g. `[{i+1}] (Book — Chapter › Section)`.
- [ ] 1.2 Confirm by reading the code that `sources` and `context` iterate the same `chunks` list in
      the same order (the mapping invariant); add a code comment stating the contract.

## 2. Prompt citation rule

- [ ] 2.1 Add the citation instruction to `RagPluginBase.GetSystemPrompt` (base — Disease /
      Symptoms / Treatment) and update its few-shot EXAMPLE ANSWER to show `[n]` markers appended
      mid-prose.
- [ ] 2.2 Add the citation instruction to `GlobalSearchPlugin.GetSystemPrompt`.
- [ ] 2.3 Add the citation instruction + update the EXAMPLE ANSWER in
      `DifferentialDiagnosisPlugin.GetSystemPrompt`.
- [ ] 2.4 Keep every existing prose rule (continuous sentences, no lists/markdown, same language,
      insufficient-excerpts fallback).

## 3. Web path

- [ ] 3.1 In `QueryService.EnrichWithWebAsync`, add the "preserve existing `[n]` markers unchanged"
      instruction to the synthesis system prompt.
- [ ] 3.2 Leave `AnswerFromWebAsync` unchanged (pure-web answers carry no markers).

## 4. Markdown stripping

- [ ] 4.1 Add a `MarkdownStripper` test asserting `[1]`, `[2][3]`, and `[1, 2]` survive `Strip()`
      unchanged. Only change stripper code if the test fails (not expected).

## 5. Rendering

- [ ] 5.1 Add a pure segmenter (answer text + source count → ordered plain-text / marker segments)
      using regex `\[\s*\d+(?:\s*,\s*\d+)*\s*\]`, with the range guard (`1..sourceCount`) and the
      `sourceCount == 0` → all-plain-text rule. Put it where it can be unit-tested.
- [ ] 5.2 In `Query.razor`, render the answer body from segments: plain text as text (preserving
      `white-space: pre-wrap`), markers as `<sup class="cite" title="@Label(n)">@n</sup>` where
      `Label(n)` names `sources[n-1]` (book title + page, or web title).
- [ ] 5.3 Add `.cite` styling to `Query.razor.css` (mono, azure, superscript, subtle background,
      accessible contrast) consistent with the approved design.

## 6. Tests & verification

- [ ] 6.1 Unit-test the segmenter: in-range marker → marker; out-of-range → plain text; comma group
      → multiple markers; no markers → single text segment; `sourceCount == 0` → all text.
- [ ] 6.2 `dotnet build MedAssist.slnx` — 0 warnings.
- [ ] 6.3 `dotnet test MedAssist.Tests` — green.
- [ ] 6.4 Live check (needs the PCC stack): run a book query of each type and confirm markers appear
      in-prose, map to the right source on hover, and that over-citation degrades to plain text.
      Tune prompt/example wording if marker placement is unreliable.
