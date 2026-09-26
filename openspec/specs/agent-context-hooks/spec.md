# agent-context-hooks Specification

## Purpose
TBD - created by archiving change port-context-hooks. Update Purpose after archive.
## Requirements
### Requirement: Once-per-session dev-flow context

On the first user prompt of a Claude Code session, the `UserPromptSubmit` hook SHALL inject the
contents of `context/dev-flow.full.txt` as additional context. On every later prompt of the same
session, it SHALL inject nothing unless a `context/dev-flow.short.txt` file exists.

#### Scenario: First prompt injects

- **WHEN** the hook receives `{"session_id":"t1"}` for the first time
- **THEN** it prints a `UserPromptSubmit` `additionalContext` payload containing the dev-flow text

#### Scenario: Second prompt is silent

- **WHEN** the hook receives `{"session_id":"t1"}` again
- **THEN** it prints nothing and exits 0

### Requirement: Once-per-session code-search nudge

The `PreToolUse` hook SHALL inject `context/prefer-serena.txt` at most once per session. For `Bash`
it SHALL fire only when the command runs `grep`, `egrep`, `fgrep`, or `rg`.

#### Scenario: Grep through Bash nudges once

- **WHEN** a session's first `Bash` call runs `grep -rn x .`
- **THEN** the nudge is injected, and a later grep in that session injects nothing

#### Scenario: Non-search Bash is ignored

- **WHEN** a `Bash` call runs `dotnet test MedAssist.Tests`
- **THEN** the hook prints nothing

### Requirement: Hooks fail safe

A hook SHALL never fail a turn: every run SHALL exit 0. A missing or empty context file, an
unwritable marker directory, or a missing `jq` SHALL additionally produce no output. Malformed stdin
SHALL exit 0 but MAY still inject once (it is treated as an unknown session; Claude Code never sends
it, and the scripts stay byte-identical to upstream).

#### Scenario: Missing context file opts out

- **WHEN** `context/prefer-serena.txt` is renamed to `prefer-serena.txt.off`
- **THEN** the `PreToolUse` hook prints nothing and exits 0

### Requirement: Context is a delta over CLAUDE.md

The files under `context/` SHALL NOT restate instructions already present in the root `CLAUDE.md`.

#### Scenario: No duplicated rule

- **WHEN** `dev-flow.full.txt` is reviewed against `CLAUDE.md`
- **THEN** it doesn't repeat the build/test commands, the docker/git approval rule, or the Serena
  memory rule

