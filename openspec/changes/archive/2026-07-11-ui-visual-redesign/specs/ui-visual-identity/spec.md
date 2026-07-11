## ADDED Requirements

### Requirement: Centralized design token system
The web app SHALL define a single set of CSS custom properties on `:root` in `app.css` for color,
typography stacks, radius, and spacing, and all surfaces SHALL derive their styling from these
tokens rather than from hard-coded values. The palette SHALL be a committed dark theme with a
single accent color, azure `#4C9EF5`.

#### Scenario: Tokens defined once
- **WHEN** `app.css` is inspected
- **THEN** it defines the `--bg`, `--surface`, `--elevated`, `--border`, `--text`, `--muted`,
  `--accent` (and related) custom properties on `:root`
- **AND** the accent token resolves to azure `#4C9EF5`

#### Scenario: Surfaces consume tokens
- **WHEN** any component's styling references a background, border, text, or accent color
- **THEN** it uses a design token (e.g. `var(--surface)`) rather than a literal hex value

#### Scenario: Single committed theme
- **WHEN** the app renders in any browser
- **THEN** it presents the dark identity with no light-theme variant

### Requirement: Dual typographic voice
Interface labels and data SHALL be set in a monospace system-font stack (the "instrument" voice),
while answer prose and message body text SHALL be set in a humanist sans system-font stack. No web
font SHALL be downloaded from a network origin, and the stacks SHALL render both Latin and Cyrillic
glyphs.

#### Scenario: Labels and data in mono
- **WHEN** the wordmark, a section label, a citation marker, a page-number, or a language tag
  renders
- **THEN** it is set in the monospace stack

#### Scenario: Answer prose in sans
- **WHEN** an assistant answer or a user message renders
- **THEN** its body text is set in the sans stack

#### Scenario: No network font dependency
- **WHEN** the rendered pages and stylesheets are inspected
- **THEN** no `@font-face` or `<link>` requests a font from an external/CDN origin
- **AND** both type stacks are composed of system fonts

#### Scenario: Bilingual rendering
- **WHEN** Bulgarian (Cyrillic) text appears in a label, chip, or answer
- **THEN** it renders in the intended stack without missing-glyph fallback boxes

### Requirement: Query-type segmented control
The query-type selector on the Query page SHALL be presented as an always-visible segmented
control exposing all five query modes, replacing the previous dropdown. Selecting a mode SHALL
have the same effect on the query as the previous dropdown, and the selector SHALL remain a
keyboard-operable, labelled control.

#### Scenario: All modes visible
- **WHEN** the Query page renders
- **THEN** Global search, Disease, Symptoms, Treatment, and Differential are all visible as
  segments without opening a menu

#### Scenario: Selecting a mode
- **WHEN** the user selects a segment
- **THEN** that mode becomes the active query type and the next query uses it, matching prior
  behaviour

#### Scenario: Keyboard operable
- **WHEN** the user focuses the segmented control and navigates with the keyboard
- **THEN** the mode can be changed without a pointer, and the focused segment shows a visible focus
  indicator

### Requirement: Cited answer reference apparatus
An assistant answer that has source citations SHALL present them as a visible reference apparatus
rather than a hidden-by-default toggle: a structured source list SHALL be shown with, for each
source, an index, the source path, and mono-set locator metadata. Book sources SHALL show the
book title with chapter/section path and page range; web sources SHALL be visually tagged and,
when the URL is an HTTP(S) link, rendered as an external link. The set of sources shown SHALL be
exactly what the query returned (no behavioural change).

#### Scenario: Book source row
- **WHEN** an answer cites a book source
- **THEN** the source list shows an index, the book title with its chapter/section path, and a
  mono page range

#### Scenario: Web source row
- **WHEN** an answer cites a web source with an HTTP(S) URL
- **THEN** the source list shows it with a web/PubMed tag and renders the title as an external
  link (`target="_blank"`, `rel="noopener noreferrer"`)

#### Scenario: Answer with no sources
- **WHEN** an answer has no citations
- **THEN** no source list renders and no empty apparatus is shown

### Requirement: Consistent identity across all surfaces
The visual identity SHALL be applied consistently to every Blazor surface: the sidebar/nav, Query
page, Login, and the Admin pages (Books, Upload book, Users, Create user). Admin data tables SHALL
use mono column headers and tabular figures, and record status SHALL be shown as a color-coded
pill.

#### Scenario: Nav in identity
- **WHEN** the sidebar renders
- **THEN** it shows the brand mark and mono wordmark, token-driven nav items, and a user chip

#### Scenario: Admin table in identity
- **WHEN** the Books admin page renders its list
- **THEN** column headers are mono, numeric columns use tabular figures, and each book's index
  status renders as a color-coded pill

#### Scenario: Login in identity
- **WHEN** the Login page renders
- **THEN** it shows the branded card with mono field labels and the azure sign-in action

### Requirement: Directed empty, loading, and error states
The Query page SHALL present a branded empty state before the first question, a "thinking"
indicator while a query is in flight, and a token-driven inline error when a query fails. Error
copy SHALL state what went wrong in the interface's voice without a personal apology.

#### Scenario: Empty state
- **WHEN** the Query page has no messages
- **THEN** a branded prompt invites the user to ask the first clinical question

#### Scenario: Thinking indicator
- **WHEN** a query is in flight
- **THEN** an assistant-side thinking indicator is shown until the answer arrives

#### Scenario: Error state
- **WHEN** a query fails
- **THEN** a token-driven error alert states what failed and how to proceed, without an apology

### Requirement: Accessibility floor preserved
The redesign SHALL preserve an accessibility floor: every interactive control SHALL have a visible
keyboard focus indicator, text/background color pairs SHALL meet WCAG AA contrast at their rendered
sizes, and non-essential motion SHALL be disabled under `prefers-reduced-motion: reduce`.

#### Scenario: Visible focus
- **WHEN** any interactive control (segment, chip, toggle, link, button, input) receives keyboard
  focus
- **THEN** a visible focus indicator is shown

#### Scenario: Reduced motion respected
- **WHEN** the user has `prefers-reduced-motion: reduce` set
- **THEN** the thinking animation, glow pulses, and transitions are disabled

#### Scenario: Contrast floor
- **WHEN** text renders against its background
- **THEN** the pair meets WCAG AA contrast for its size

### Requirement: No behavioural or structural change
This redesign SHALL NOT change any route, endpoint, service call, binding, query flow, or persisted
data; edits SHALL be limited to component markup structure and CSS. The C# build SHALL continue to
succeed with zero warnings.

#### Scenario: Flows unchanged
- **WHEN** a user logs in, runs each query type, filters by book, toggles PubMed fallback, clears
  history, and uses the admin pages after the redesign
- **THEN** every flow behaves exactly as before, with only the presentation changed

#### Scenario: Build stays clean
- **WHEN** `dotnet build MedAssist.slnx` runs after the redesign
- **THEN** it succeeds with zero warnings
