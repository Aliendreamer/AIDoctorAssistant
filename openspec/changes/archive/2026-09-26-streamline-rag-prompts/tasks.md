## 1. Baseline measurement

- [x] 1.1 Verify whether the SK Ollama connector exposes the prompt-eval count on non-streaming
      responses (`InnerContent` / `Metadata`); note the finding in design.md Open Questions
- [x] 1.2 Add the `rag_prompt_tokens` histogram (tag `plugin_type`, plus `estimated=true|false`) to
      the `MedAssist.AI` meter and record it for every answer-generation call
- [x] 1.3 Write the golden question set (~6 EN + ~6 BG across Disease, Symptoms, Treatment, Global,
      DDx, including one BG follow-up) as `golden-questions.md` in this change folder
- [x] 1.4 ~~Run the golden set on the old prompts (`baseline.md`)~~ — replaced: a second image from
      `main` was declined; run 1 (`run1-4k-context.md`) is the reference instead, see `comparison.md`

## 2. Prompt builder (TDD)

- [x] 2.1 Write failing tests for a pure `AnswerPromptBuilder`: citation instruction once and after
      the excerpts; language line once; prose rule once; persona once; no "Sources searched" list;
      excerpt headers carry book/chapter/section
- [x] 2.2 Write failing tests for history handling: `[n]`/`[n][m]` stripped from assistant turns,
      user turns untouched, character budget drops oldest whole pairs, latest pair always kept
- [x] 2.3 Add the history character-budget constant under `MedAssist.Shared/Constants`
- [x] 2.4 Implement `AnswerPromptBuilder` with shared core + `PromptProfile` delta until 2.1–2.2 pass

## 3. Migrate call sites

- [x] 3.1 Replace `GetSystemPrompt()` overrides with `PromptProfile` deltas in `RagPluginBase`,
      `GlobalSearchPlugin`, `DifferentialDiagnosisPlugin` (Disease/Symptoms/Treatment use the
      default profile)
- [x] 3.2 Switch `RagPluginBase.BuildResultAsync` to the builder; remove the book-list block
- [x] 3.3 Compose `EnrichWithWebAsync` and `AnswerFromWebAsync` prompts from the shared core plus
      their deltas; keep the `<web_source>` guard verbatim; add a test that it is present once
- [x] 3.4 `dotnet build MedAssist.slnx` with 0 warnings and `dotnet test MedAssist.Tests` green

## 3b. Context window (added after golden run 1)

- [x] 3b.1 Test + add `PromptConstants.ContextWindowTokens` (8,192) and a shared
      `AnswerPromptBuilder.ExecutionSettings` carrying `num_ctx`; pass it at all four chat call sites
- [x] 3b.2 Test + move the language line after the excerpts (book prompt) and to the end of both
      web prompts
- [x] 3b.3 Confirm live: Ollama log shows `n_ctx_slot = 8192`

## 4. Verify and gate

- [x] 4.1 Re-run the golden set on the new build; results in `run2-8k-context.md`, compared in
      `comparison.md`
- [x] 4.2 Gate (12/12 markers, in range, language; 0 leakage; token drop covered by unit tests):
      prompt tokens down; marker rate and markers-in-range not lower than baseline; no BG
      answer in the wrong language; no new list/markdown leakage. If citations regress, restore a
      one-line citation mention in the core and re-measure (design.md Risks)
- [ ] 4.3 Live-verify in the UI: one EN and one BG query plus a follow-up render with superscript
      markers and correct sources
- [x] 4.4 Update `ROADMAP.md` citation-reliability section with the measured outcome
