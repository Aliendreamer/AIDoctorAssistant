## 1. Design token foundation

- [x] 1.1 Rewrite `wwwroot/app.css` `:root` with the full token system from `design.md` (color,
      `--sans`/`--mono` stacks, radius). Keep the existing token names that other files already use
      so nothing breaks mid-migration.
- [x] 1.2 Update the global Bootstrap overrides (`.btn-primary`, `.form-control`, `.form-select`,
      `.alert-*`, focus rings) to derive from the new tokens; retune `--accent` usages to azure.
- [x] 1.3 Set the base `body` font to `--sans`, add the ambient accent-glow background, and confirm
      every existing page still renders (tokens are a superset of today's variables).
- [x] 1.4 Add a global `@media (prefers-reduced-motion: reduce)` block and a shared
      `:focus-visible` treatment.

## 2. Sidebar / nav

- [x] 2.1 `NavMenu.razor`: add the brand mark + mono `Med`**`Assist`** wordmark with `CLINICAL RAG`
      sub-label; keep all existing links, roles, and the logout form.
- [x] 2.2 `NavMenu.razor.css`: token-driven nav items (icon + label), active state = accent glow +
      accent-line border, `LIBRARY` mono section label.
- [x] 2.3 Add the bottom user chip (mono initials avatar, name, role) sourced from the existing auth
      state; no new services.
- [x] 2.4 `MainLayout.razor(.css)`: align the shell to tokens; verify the mobile nav toggle still
      works at ≤640px.

## 3. Query page — controls

- [x] 3.1 Replace the query-type `<select>` with a segmented control bound to the same `_queryType`
      field and `OnQueryTypeChangedAsync`; keep it keyboard-operable and labelled.
- [x] 3.2 Restyle the language `<select>` as a mono pill and the PubMed checkbox as a toggle switch
      (same `_webSearchEnabled` binding).
- [x] 3.3 Restyle book-filter chips (selected = accent glow + accent dot + mono language tag), same
      `ToggleBook` binding; restyle the Clear-history button.

## 4. Query page — transcript & citations (signature)

- [x] 4.1 Restyle user/assistant turns: mono eyebrows, azure user bubble, `--surface` answer card
      with the left accent rule.
- [x] 4.2 Build the reference apparatus: dashed divider, `SOURCES` header + accent count badge, and
      3-column source rows (index · path · mono meta). Reuse `GetMessageSources`.
- [~] 4.3 `BookSourceCitation.razor` / `WebSourceCitation.razor` — SKIPPED: these shared components
      are unused dead code (Query.razor renders sources inline, which *is* styled to the apparatus
      treatment with the `WebFetchPolicy.IsHttpUrl` link guard). Left untouched rather than polish
      dead code.
- [x] 4.4 Keep the `Sources` block visible (promote from the `<details>` toggle to the always-shown
      apparatus). *Inline `[n]` prose markers are out of scope — deferred to `cited-answer-markers`.*

## 5. Query page — composer & states

- [x] 5.1 Restyle the composer dock (auto-grow textarea + azure Send with arrow glyph), same
      `SubmitQueryAsync` / `HandleKeyDown` bindings; add the mono hint row.
- [x] 5.2 Replace the bare empty state with the branded prompt panel.
- [x] 5.3 Add an assistant-side "thinking" indicator shown while `_loading` is true (reduced-motion
      safe).
- [x] 5.4 Restyle the error alert to the token-driven treatment; reword copy to the interface voice.

## 6. Login

- [x] 6.1 `Login.razor` + `LoginLayout.razor`: restyle the card (brand mark, mono field labels,
      azure sign-in), consuming the existing `.login-*` classes now driven by tokens. No form/logic
      change.

## 7. Admin surfaces

- [x] 7.1 `Admin/Books.razor`: token-driven table, mono headers, `tabular-nums` counts/dates, and
      status pills (indexed → success, indexing → accent, failed → danger). Keep re-index actions.
- [x] 7.2 `Admin/UploadBook.razor`: restyle the form to the input/composer treatment; keep the
      upload endpoint and validation.
- [x] 7.3 `Admin/Users.razor` + `Admin/CreateUser.razor`: token-driven table and form; keep logic.
- [x] 7.4 `AdminLayout.razor`: align to tokens if it diverges from `MainLayout`.

## 8. Remaining states

- [x] 8.1 `Error.razor` and `NotFound.razor`: identity-consistent restyle.
- [x] 8.2 `ReconnectModal.razor(.css)`: restyle to the identity, reduced-motion safe.

## 9. Verification

- [x] 9.1 `dotnet build MedAssist.slnx` — 0 warnings.
- [ ] 9.2 Drive the LIVE app and confirm every flow is unchanged: login, each query type, book
      filter, PubMed toggle, clear history, and each admin page render + action. NOT YET DONE —
      requires the PCC docker stack (Postgres/Qdrant/Ollama) running. Static verification done
      instead: all bindings/logic preserved verbatim, `dotnet build` is 0-warning, and the real CSS
      was rendered + screenshot in a browser. A live smoke test is still owed once the stack is up.
- [x] 9.3 Compare each surface against the approved mockup and the `design.md` accessibility floor
      (focus-visible on all controls, AA contrast, reduced-motion); note and fix gaps.
