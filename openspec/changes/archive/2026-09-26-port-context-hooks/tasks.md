## 1. Port scripts

- [x] 1.1 Copy `state.sh`, `dev-flow-reminder.sh`, `prefer-serena.sh` from
      `../SmartTVApp/.claude/claude-hooks/` unchanged, keeping them executable
- [x] 1.2 Copy `README.md` and adapt it: name SmartTVApp as the upstream, drop the SmartTVApp-only
      measurements table or label it as upstream data

## 2. Project context

- [x] 2.1 Write `context/dev-flow.full.txt` as a delta over `CLAUDE.md` (test-first, live-verify via
      docker `web` + Playwright, `openspec archive` before commit, narrate progress), under ~100 tokens
- [x] 2.2 Write `context/prefer-serena.txt` (Serena / C# LSP for code; grep for config, compose, and
      markdown; shown once per session)
- [x] 2.3 Check both texts against `CLAUDE.md` line by line and delete anything it already says

## 3. Wire and verify

- [x] 3.1 Add the `hooks` block (`UserPromptSubmit`; `PreToolUse` for `Grep` and `Bash`) to
      `.claude/settings.json`
- [x] 3.2 Run the README's stdin tests: first prompt emits, second is silent, grep emits once,
      `dotnet test` stays silent, an empty context file stays silent, malformed JSON exits 0
- [x] 3.3 Start a fresh Claude Code session and confirm the dev-flow text appears exactly once
