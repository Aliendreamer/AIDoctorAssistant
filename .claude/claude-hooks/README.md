# claude-hooks

Context-injection hooks that say each thing **once per session** instead of once per fire.

Ported from SmartTVApp's `.claude/claude-hooks/` (the upstream). The three `.sh` files are kept
byte-identical to it — fix them there and copy back; only `context/` is aidoctor's own.

## Why

A hook's `additionalContext` is injected into the conversation on every fire and is never
compacted away. Measured over 16 sessions of the upstream repo before the change:

| Hook | Fires | Per fire | Total |
| --- | --- | --- | --- |
| `UserPromptSubmit` dev-flow contract | 255 | ~193 tok | ~49k tok |
| `PreToolUse` prefer-Serena nudge | 880 | ~32 tok | ~28k tok |

All of it re-stating text that had not changed — roughly **4.8k tokens per session** of
self-inflicted context poisoning. After: ~103 and ~60 tokens per session respectively, and
both figures are flat rather than growing with session length.

## Write only the delta

A hook's text should say what the always-loaded `CLAUDE.md` does not. The first draft of
`dev-flow.full.txt` was 817 chars, of which ~500 restated the workflow list and the Serena
mandate that `CLAUDE.md` already carries on every turn — paying twice for one instruction.
What survived is the part `CLAUDE.md` genuinely omits: the literal command names, that TDD is
red-green-refactor, step 5.5's `code-coverage` invocation, and the definition of done.

When editing any `context/*.txt`, read the repo's `CLAUDE.md` first and delete every sentence
it already makes. Duplicating always-loaded text is the failure this directory exists to fix.

## Per-prompt text

There is deliberately none. An earlier revision injected a 160-char pointer on every prompt
after the first; at ~40 tokens a turn that is ~2k tokens in a 50-prompt session and ~4k in a
100-prompt one, and it overtakes the one-shot full contract after about five prompts. It also
re-stated a sentence root `CLAUDE.md` already carries always-loaded, which is the same failure
the hooks exist to fix — only cheaper per fire.

The capability still exists: create `context/dev-flow.short.txt` and it is injected from the
second prompt on. No such file ships here, and the bar for adding one is high — it must say
something the always-loaded `CLAUDE.md` does not, and be worth ~40 tokens on every turn of
every session forever.

## Layout

```
claude-hooks/
  state.sh                      # shared helpers — UNIVERSAL, copy unchanged
  dev-flow-reminder.sh          # UserPromptSubmit hook — UNIVERSAL, copy unchanged
  prefer-serena.sh              # PreToolUse hook      — UNIVERSAL, copy unchanged
  context/
    dev-flow.full.txt           # PROJECT-SPECIFIC — injected on the first prompt
    prefer-serena.txt           # PROJECT-SPECIFIC — injected once per session
```

The three `.sh` files contain no project-specific text. To adopt this in another repo, copy
the directory, then rewrite only the three files under `context/`.

## Contract

- **A missing or empty `context/*.txt` means "stay silent".** That is how a repo opts out of one
  hook without touching `settings.json` — rename the file to `*.off` and the hook no-ops.
- **A hook never fails a turn.** Malformed stdin, an unreadable marker directory, or a missing
  context file all exit 0 with no output.
- **Markers live in `$TMPDIR/cc-hook-state-<uid>/`**, outside the repo, so they never appear in
  `git status` and vanish with the machine's temp sweep. Files older than 7 days are pruned
  opportunistically on the once-per-session path.
- **`jq` is a hard dependency.** Without it every hook silently no-ops rather than erroring.
- `prefer-serena.sh` filters the `Bash` arm to command lines that actually run `grep`/`egrep`/
  `fgrep`/`rg`, so a session that never greps never sees the reminder at all. The `Grep` tool
  is a text search by definition and is never filtered.

## Wiring

In `.claude/settings.json` (paths use `${CLAUDE_PROJECT_DIR:-.}` so the repo stays relocatable):

```json
{
  "hooks": {
    "UserPromptSubmit": [
      { "matcher": "*", "hooks": [{ "type": "command",
        "command": "\"${CLAUDE_PROJECT_DIR:-.}/.claude/claude-hooks/dev-flow-reminder.sh\"" }] }
    ],
    "PreToolUse": [
      { "matcher": "Grep", "hooks": [{ "type": "command",
        "command": "\"${CLAUDE_PROJECT_DIR:-.}/.claude/claude-hooks/prefer-serena.sh\"" }] },
      { "matcher": "Bash", "hooks": [{ "type": "command",
        "command": "\"${CLAUDE_PROJECT_DIR:-.}/.claude/claude-hooks/prefer-serena.sh\"" }] }
    ]
  }
}
```

## Testing a change

Pipe the stdin payload the hook will receive, rather than waiting for a real session:

```sh
echo '{"session_id":"t1"}' | .claude/claude-hooks/dev-flow-reminder.sh          # emits
echo '{"session_id":"t1"}' | .claude/claude-hooks/dev-flow-reminder.sh          # silent
echo '{"session_id":"t2","tool_name":"Bash","tool_input":{"command":"grep -rn x ."}}' \
  | .claude/claude-hooks/prefer-serena.sh                                       # emits
echo '{"session_id":"t2","tool_name":"Bash","tool_input":{"command":"dotnet test MedAssist.Tests"}}' \
  | .claude/claude-hooks/prefer-serena.sh                                       # silent
```

Clear the markers between runs with
`find "${TMPDIR:-/tmp}/cc-hook-state-$(id -u)" -type f -delete`.
