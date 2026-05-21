# Query UI Redesign & New RAG Plugins

**Date:** 2026-05-21  
**Status:** Implemented

---

## Overview

Two parallel improvements implemented in the same session:

1. **Query page UI redesign** — replace the form-above-results layout with a proper chat interface matching modern AI assistant conventions.
2. **Two new RAG plugins** — `GlobalSearchPlugin` (unrestricted search) and `DifferentialDiagnosisPlugin` (symptom-cluster to ranked DDx).

---

## 1. Query UI Redesign

### Problem

The previous layout placed all form controls (query type, language, book list, web search toggle, submit button) at the top, with chat history appended below after each query. This meant:

- The input moved further from the user's focus with each exchange.
- The book list was a vertical checkbox stack, taking excessive vertical space.
- The sidebar active-route indicator was barely visible (`rgba(255,255,255,0.37)` on a dark gradient).

### Design

**Layout — three zones stacked vertically:**

```
┌─────────────────────────────────────────┐
│ Controls (compact, flex row)            │
│  [Query type ▾] [Language ▾] [PubMed ☐] [Clear history] │
│  [chip] [chip] [chip] …  (book pills)   │
├─────────────────────────────────────────┤
│ Chat container (flex: 1, overflow-y:    │
│ auto, fills remaining viewport height) │
│                                         │
│          Ask a medical question…        │  ← empty state
│                                         │
│  ┌──────────────────────────────────┐   │
│  │ You                              │   │  ← right-aligned accent bubble
│  └──────────────────────────────────┘   │
│  ┌──────────────────────────────────┐   │
│  │ MedAssist                        │   │  ← left-aligned elevated bubble
│  │   ▸ Sources (3)                  │   │
│  └──────────────────────────────────┘   │
├─────────────────────────────────────────┤
│ [textarea — Enter to send…]  [Send]     │
└─────────────────────────────────────────┘
```

**Book selector:** vertical checkbox list → horizontal flex-wrap pill chips. Each chip shows book title + language badge. Selected state uses `--accent` colour with a tinted background. Clicking anywhere on the chip toggles selection (hidden checkbox inside `<label>`).

**Chat bubbles:**
- User messages: right-aligned, `--accent` background, bottom-right radius collapsed.
- Assistant messages: left-aligned, `--bg-elevated` with `--border`, bottom-left radius collapsed.
- Sources rendered as a `<details>` toggle beneath the latest assistant bubble only.

**Input area:** `<textarea>` that grows up to 130px; Enter submits, Shift+Enter inserts newline. Send button is 48px tall to align with single-line textarea height.

**Auto-scroll:** `OnAfterRenderAsync` calls a JS eval to set `scrollTop = scrollHeight` on the chat container after each render cycle that has history.

**Clear history button:** always visible, disabled when `_history.Count == 0`.

### Nav sidebar

- Active route: `--accent` left-border (2px) + `rgba(79, 126, 247, 0.18)` background, white text, `font-weight: 500`.
- Inactive links: `rgba(255,255,255,0.65)`, full opacity on hover.
- Icon `<span>` opacity 0.75, goes to 1 on hover/active.
- Admin section label: 0.65rem uppercase, `rgba(255,255,255,0.3)`.

### Files changed

| File | Change |
|------|--------|
| `MedAssist.Web/Components/Pages/Query.razor` | Full restructure to three-zone layout |
| `MedAssist.Web/Components/Pages/Query.razor.css` | Complete rewrite for chat UI |
| `MedAssist.Web/Components/Layout/NavMenu.razor.css` | Active-route accent styling |

---

## 2. New RAG Plugins

### Problem

The three existing plugins (Disease, Symptoms, Treatment) force the user to categorise their question before asking. Two common use cases have no good fit:

- **Open clinical question** — e.g. "tell me about Wilson's disease" (covers disease + presentation + treatment).
- **Differential diagnosis** — e.g. "child with fever, rash, and joint pain" — the model should reason about possible diagnoses, not just retrieve symptom facts.

### Design

Both plugins extend `RagPluginBase` and use the same hybrid RAG pipeline. The only difference is the system prompt passed to the LLM.

**`RagPluginBase` change:** system prompt extracted from `BuildResultAsync` into a `protected virtual string GetSystemPrompt()` method. Default implementation returns the existing prose-only prompt. Subclasses override to specialise.

#### GlobalSearchPlugin

- **KernelFunction description:** "Search all indexed medical books for any clinical question without category restriction."
- **System prompt additions:** instructs the model to cover aetiology, presentation, pathophysiology, diagnosis, and management as the sources allow — rather than focusing on one dimension.
- **Default query type:** replaces Disease as the default selection in the UI.

#### DifferentialDiagnosisPlugin

- **KernelFunction description:** "Generate a differential diagnosis from a clinical presentation or symptom cluster."
- **System prompt:** directs the model to present diagnoses from most to least likely, explaining supporting clinical features for each. Prose style is "reasoning aloud during a ward round."
- **Input expectation:** a symptom cluster or clinical presentation string (e.g. "6-year-old, fever 5 days, strawberry tongue, rash, conjunctival injection").

### QueryType enum additions

```csharp
public enum QueryType
{
    Symptoms,
    Disease,
    Treatment,
    GlobalSearch,           // new — default
    DifferentialDiagnosis   // new
}
```

Chat history is keyed by `QueryType.ToString().ToLowerInvariant()`, so each plugin maintains its own independent conversation thread.

### Files changed

| File | Change |
|------|--------|
| `MedAssist.AI/Plugins/RagPluginBase.cs` | Extract `GetSystemPrompt()` virtual method |
| `MedAssist.AI/Plugins/GlobalSearchPlugin.cs` | New plugin |
| `MedAssist.AI/Plugins/DifferentialDiagnosisPlugin.cs` | New plugin |
| `MedAssist.AI/Kernel/KernelFactory.cs` | Register both new plugins |
| `MedAssist.Shared/Models/QueryRequest.cs` | Add two enum values |
| `MedAssist.Web/Services/QueryService.cs` | Add switch cases |
| `MedAssist.Web/Components/Pages/Query.razor` | Add dropdown options, set GlobalSearch as default |

---

## Trade-offs & Notes

- **GlobalSearch vs running all three plugins in parallel:** single RAG call is chosen for speed and simplicity. Running all three would be 3× slower with diminishing returns since all plugins search the same book corpus.
- **DifferentialDiagnosis prompt compliance:** like the other plugins, it depends on `qwen2.5:7b` following the prose instruction. Larger models (14b/32b) would give more reliable formatting.
- **Re-index needed for OCR cleanup:** the Portuguese-diacritic OCR artifact regex added to `MarkdownChunker` applies only to newly indexed books. The existing 22k Qdrant vectors still contain garbled Cyrillic and need a full re-index pass to benefit.
