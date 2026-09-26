## Why

aidoctor has no Claude Code hooks, so the working agreements that are *not* in the always-loaded
`CLAUDE.md` — TDD first, live verification, OpenSpec archive-then-commit, narrating progress — only
reach the agent if it happens to read the right Serena memory. SmartTVApp solved the same problem
with hooks that inject that delta **once per session**. The measured cost of per-fire injection there
was ~4.8k tokens per session of repeated context, cut to ~160 after the change. Porting the hooks
gives aidoctor the reminders without paying that cost.

## What Changes

- Copy SmartTVApp's universal hook scripts unchanged into `.claude/claude-hooks/`: `state.sh`,
  `dev-flow-reminder.sh`, `prefer-serena.sh`, and `README.md`, with its wording adapted to this repo.
- Write aidoctor-specific `context/dev-flow.full.txt` with only what `CLAUDE.md` does not already say,
  drawn from the Serena `task_completion_checklist` and `feedback/*` memories.
- Write `context/prefer-serena.txt`, pointing code search at Serena / C# LSP (both plugins are
  already enabled here) and raw grep at configs, compose, and markdown.
- Wire `UserPromptSubmit` and `PreToolUse` (`Grep`, `Bash`) in `.claude/settings.json`.
- No per-prompt `dev-flow.short.txt` ships; see the source README for why.

## Capabilities

### New Capabilities

- `agent-context-hooks`: once-per-session context injection for Claude Code sessions in this repo,
  covering what is injected, when, and how a hook fails safe.

### Modified Capabilities

(none)

## Impact

- **Files:** new `.claude/claude-hooks/**`; `.claude/settings.json` gains a `hooks` block.
- **Dependencies:** `jq` (present at `/usr/bin/jq`); without it the hooks silently no-op.
- **Runtime:** none. The application code, build, and tests are untouched.
