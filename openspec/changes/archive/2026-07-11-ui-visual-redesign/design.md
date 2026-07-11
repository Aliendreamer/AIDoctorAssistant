# Design — "Clinical instrument"

An approved interactive mockup of the redesigned Query page (azure accent) drove these
decisions. It is the reference for the target look.

## Design thesis

A physician's tool should feel like precise clinical equipment, not a chat toy. Two moves carry
the identity:

1. **Monospace as the "instrument" voice.** Every label, the wordmark, and all data (citations,
   page numbers, ICD codes, language tags, timestamps) are set in a monospace stack, the way a
   lab readout or diagnostic instrument presents information. Answer *prose* stays in a humanist
   sans so long medical text is comfortable to read. This is the one distinctive move; everything
   else stays quiet.
2. **The cited answer is the hero.** The most valuable, most characteristic artifact in a medical
   RAG assistant is a source-grounded answer. Citations are promoted from a hidden `<details>`
   toggle into a visible **reference apparatus** — a structured, mono-set source list beneath each
   answer. (Inline numbered markers *inside* the prose are a natural extension but need a RAG
   prompt change; they are deferred to the `cited-answer-markers` follow-up. This change styles the
   apparatus only.)

The theme is a **deliberate single dark theme** — not an omission. The product lives in a focused
dark clinical environment; there is no light variant.

## Constraints that shaped the choices

- **Offline / local deployment.** No CDN, no downloaded web fonts. Both type stacks are system
  fonts, so the identity is fully self-contained and survives an air-gapped deployment.
- **Bilingual EN/BG.** The mono and sans stacks must render Cyrillic; system stacks do. No glyph
  is delivered by a subsetted Latin-only web font.
- **Look-only.** Component logic, bindings, routes, and services are untouched. Every edit is to
  markup structure and CSS.
- **`TreatWarningsAsErrors` is on**, but CSS/markup changes don't affect the C# build; the app
  must still build with 0 warnings.

## Token system

Defined once on `:root` in `app.css`; every surface derives from these. Names are stable so
scoped component CSS can reference them.

### Color

| Token | Value | Role |
| --- | --- | --- |
| `--bg` | `#0A0E14` | App background (deep cool near-black) |
| `--surface` | `#10151E` | Panels, cards, composer, answer card |
| `--elevated` | `#171E2A` | Hover fills, segmented "on", inputs |
| `--raised` | `#1D2634` | Avatars, toggle track |
| `--border` | `#232C3A` | Standard hairline border |
| `--border-soft` | `#1B2230` | Quiet dividers |
| `--text` | `#E7EDF4` | Primary text |
| `--muted` | `#7E8CA0` | Secondary text, labels |
| `--faint` | `#55627A` | Tertiary / placeholder / eyebrow |
| `--accent` | `#4C9EF5` | **Single accent — azure** |
| `--accent-ink` | `#061826` | Text/icon on an accent fill |
| `--accent-glow` | `rgba(76,158,245,.16)` | Header glow, active-nav fill, focus ring |
| `--accent-line` | `rgba(76,158,245,.35)` | Accent borders |
| `--web` | `#A78BFA` | Web/PubMed source affordance — **violet, deliberately not azure** so external provenance never reads as the primary accent |
| `--web-bg` | `rgba(167,139,250,.12)` | Web tag fill |
| `--web-line` | `rgba(167,139,250,.35)` | Web tag border |
| `--danger` | `#F26D82` | Errors (existing, retuned) |
| `--success` | `#37D399` | Success (existing, retuned) |

The neutrals are biased slightly cool/blue toward the azure accent — a chosen neutral, not a flat
grey. Semantic danger/success are separate from the accent and are never used decoratively.

### Type

| Role | Stack | Used for |
| --- | --- | --- |
| Body / prose | `ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif` | Answer text, user messages, buttons |
| Instrument / data | `ui-monospace, "SF Mono", "JetBrains Mono", "Cascadia Mono", Menlo, Consolas, monospace` | Wordmark, all labels/eyebrows, citations, page numbers, ICD codes, language tags, counts |

Type scale (rem-ish, in px for reference): 10 labels · 11 data · 12.5 controls · 14.5 body ·
15 wordmark. Uppercase mono labels get `letter-spacing: .12–.16em`. Body line-height ~1.55.
Headings and answer text get `text-wrap: balance` / comfortable measure.

### Radius & spacing

`--r-sm: 8px` · `--r-md: 12px` · `--r-lg: 16px`. Bubbles use asymmetric radii (speaker-side
corner tightened to 4px). Consistent `gap`-based layout — no per-element margin stacking.

## Layout & components

The app shell (sidebar + main) is unchanged structurally.

- **Sidebar** — brand mark (rounded azure tile with a `+`) + mono `Med`**`Assist`** wordmark and a
  `CLINICAL RAG` sub-label; token-driven nav items with 17px stroke icons; a `LIBRARY` mono
  section label; a user chip (mono initials avatar + name + role) pinned to the bottom.
- **Query header** — query-type **segmented control** (5 modes always visible; active pill shows a
  glowing accent dot) replacing the `<select>`; a mono language `<select>` pill; a **PubMed toggle
  switch**; book-filter **chips** (selected = accent glow + accent dot, with a mono language tag).
- **Transcript** — max-width ~820px, centered. User turn: an azure bubble aligned right with a
  mono `YOU` eyebrow. Assistant turn: a `MedAssist` eyebrow over an **answer card** (`--surface`,
  a soft accent rule down the left edge).
- **Reference apparatus** (signature) — a dashed divider; a `SOURCES` mono header with an accent
  count badge; each source is a 3-column row: mono index · path (`Book › Chapter › Section` with
  book title emphasised) · mono page/pmid meta. Web sources use the `--web` color and a `PubMed`
  tag and are linkable. (The mockup's inline `[1]` markers in the prose are deferred to
  `cited-answer-markers`; the `.cite` styling ships with that change.)
- **Composer** — a `--surface` dock: auto-growing textarea + azure **Send** button with an arrow
  glyph; a mono hint row (`Enter` send · `Shift+Enter` new line · provenance note). Focus lifts an
  accent ring.
- **States** —
  - *Empty*: a branded panel inviting the first question (not the bare "Ask a medical question…").
  - *Thinking*: an inline assistant-side indicator while a query is in flight (respects
    `prefers-reduced-motion`).
  - *Error*: token-driven `--danger` inline alert; interface voice, says what failed.
  - *Reconnect / not-found / error page*: same identity.
- **Login** — centered card on `--bg` with the brand mark, mono field labels, azure sign-in button.
- **Admin** — token-driven tables (mono column headers, `tabular-nums` for counts/dates), status
  **pills** (indexed / indexing / failed mapped to success / accent / danger), and forms matching
  the composer/input treatment.

## Signature vs. quiet

Boldness is spent in exactly two related places: the **mono instrument voice** and the **reference
apparatus**. Everything else — spacing, borders, buttons — stays disciplined and low-contrast so
the citations and the answer are what the eye lands on.

## Accessibility floor

- All text/background pairs meet WCAG AA; azure-on-`--bg` and mono `--muted`/`--faint` labels
  checked at their sizes.
- Visible `:focus-visible` ring (accent glow) on every interactive control, including the
  segmented control, chips, toggle, and Send.
- `prefers-reduced-motion: reduce` disables the thinking animation, glow pulses, and transitions.
- The segmented control, chips, and toggle remain real form controls (keyboard-operable,
  labelled); the redesign restyles them rather than replacing them with non-semantic elements.

## Application approach

1. Land the token system + global overrides in `app.css` first; verify existing pages still render
   (tokens are a superset of today's variables).
2. Restyle surface-by-surface via each component's scoped `.razor.css`, keeping markup edits
   minimal and logic untouched.
3. Verify against the mockup and the accessibility floor after each surface.
