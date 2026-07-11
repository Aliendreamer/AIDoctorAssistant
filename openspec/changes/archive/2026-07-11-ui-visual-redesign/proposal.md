## Why

The Blazor UI works and is coherent, but it reads as a generic Bootstrap-dark app: muted
indigo accent, Helvetica everywhere, a query-type `<select>`, and citations hidden behind a
`<details>` toggle. For a tool physicians rely on, the interface should feel like precise
clinical equipment and should make its most valuable output — cited, source-grounded answers —
the visual centrepiece. This change gives MedAssist a distinctive visual identity without
touching any flow, route, service, or data path.

## What Changes

A **look-only** redesign — no behavioural, routing, or data changes. Every existing component
keeps its logic, bindings, and services; only its presentation changes.

**Visual identity: "clinical instrument"** (see `design.md` for the full token system)
- A committed dark theme on a deeper, cooler near-black (`#0A0E14`) with a single confident
  **azure** accent (`#4C9EF5`) and a soft accent glow behind the query header.
- **Monospace as the "instrument" voice**: the wordmark, all labels, citations, page numbers,
  ICD codes, and language tags are set in a `ui-monospace` stack; answer prose stays in a clean
  humanist-sans stack. No web fonts are downloaded — both stacks are system fonts, which keeps
  the offline/local deployment self-contained and renders both Latin and Cyrillic (EN/BG).
- Reusable design tokens (color, type, radius, spacing) defined once in `app.css` so every
  surface pulls from the same system.

**Surfaces restyled to the new identity**
- **Query page** — query-type `<select>` becomes an always-visible **segmented control**;
  refined language select, PubMed toggle switch, and book-filter chips; restyled chat transcript.
- **Cited answer as a reference apparatus** (the signature) — the existing `Sources` block is
  promoted from a hidden `<details>` toggle into a visible, structured source list: book sources
  show chapter/section path + page range in mono; web (PubMed) sources are tagged and linkable.
  This restyles the existing block; it does not change what sources are returned. *Inline `[n]`
  markers inside the answer prose are intentionally out of scope here* — they require a RAG
  prompt/pipeline change (the current prompt emits marker-free prose) and are deferred to a
  separate follow-up change, `cited-answer-markers`.
- **Sidebar / nav** — new brand mark + mono wordmark, token-driven nav items, a user chip.
- **Login** — restyled card in the new identity.
- **Admin** (Books, Upload book, Users, Create user) — token-driven tables, forms, and status
  pills.
- **States** — branded empty state, a "thinking" indicator for in-flight queries, and
  error / not-found / reconnect surfaces consistent with the identity.

**Explicitly out of scope**
- No changes to query flow, RAG pipeline, endpoints, auth, or persisted data.
- No new features, no new routes, no light theme (the dark theme is a deliberate commitment).
- Component C# logic and bindings are preserved; edits are to markup structure and CSS only.
- **Inline `[n]` citation markers in answer prose** — deferred to a follow-up change
  (`cited-answer-markers`) because they need a system-prompt + `MarkdownStripper` + parsing change,
  not just CSS.

## Capabilities

### New Capabilities

- `ui-visual-identity`: The MedAssist design system (tokens, typography voice, component
  treatments) and its consistent application across every Blazor surface.

### Modified Capabilities

- None. `blazor-query-ui`, `blazor-admin-ui`, and `blazor-auth` keep their behavioural
  requirements unchanged; this change only restyles their rendered output.

## Impact

- `MedAssist.Web/wwwroot/app.css` — replace the ad-hoc variables with the full token system;
  add shared component styles and Bootstrap overrides driven by tokens.
- `MedAssist.Web/Components/Layout/NavMenu.razor(.css)` — brand mark, mono wordmark, user chip.
- `MedAssist.Web/Components/Layout/MainLayout.razor(.css)` — shell polish.
- `MedAssist.Web/Components/Pages/Query.razor(.css)` — segmented control, chips, transcript,
  reference-apparatus citations, composer, empty/thinking states. Markup + CSS only.
- `MedAssist.Web/Components/Shared/BookSourceCitation.razor`,
  `WebSourceCitation.razor` — restyle to the reference-apparatus treatment.
- `MedAssist.Web/Components/Pages/Login.razor` + `Layout/LoginLayout.razor` — restyled card.
- `MedAssist.Web/Components/Pages/Admin/*.razor` — token-driven tables, forms, status pills.
- `MedAssist.Web/Components/Pages/Error.razor`, `NotFound.razor`,
  `Layout/ReconnectModal.razor(.css)` — identity-consistent states.
- `MedAssist.Web/Components/Layout/AdminLayout.razor` — token alignment if needed.

No changes outside `MedAssist.Web`. No new NuGet or JS dependencies.
