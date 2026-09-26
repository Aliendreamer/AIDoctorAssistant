## Context

SmartTVApp's `.claude/claude-hooks/` splits each hook into a script that works in any repo and a
project-owned text file under `context/`. Per-session markers are kept in
`$TMPDIR/cc-hook-state-<uid>/`. Each hook injects its text on the first fire of a session and stays
silent after that. aidoctor currently has no hooks at all.

## Goals / Non-Goals

**Goals:**

- Reuse the scripts byte-for-byte, so a fix in one repo can be copied to the other.
- Inject only what the always-loaded `CLAUDE.md` does not already say.

**Non-Goals:**

- Porting SmartTVApp's prettier / comment-length hooks. Those are TS-specific; the .NET equivalents
  would be a separate decision.
- Changing `CLAUDE.md`. Refreshing its stale Marker references is a separate edit.

## Decisions

- **Copy, don't symlink.** Both repos must work standalone, and a link across them would break the
  first time either repo is cloned somewhere else.
- **Delta-only content.** `dev-flow.full.txt` carries test-first, live-verify via docker `web` +
  Playwright, `openspec archive` before commit, and narrating progress during multi-step work.
  Anything `CLAUDE.md` already says (build/test commands, docker/git approval, Serena for memory) is
  left out. Target: under ~100 tokens.
- **Include prefer-Serena, softened.** `CLAUDE.md` here doesn't mandate Serena for code search, so
  the text is a preference, not a rule. It fires once, and only on sessions that actually grep.

## Risks / Trade-offs

- [The copies drift from SmartTVApp's] → The README names SmartTVApp as the upstream; a diff of the
  three `.sh` files shows any drift.
- [The injected text drifts back into repeating `CLAUDE.md`] → The README rule: read `CLAUDE.md`
  before editing any `context/*.txt`.
