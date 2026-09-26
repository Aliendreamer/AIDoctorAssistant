#!/usr/bin/env bash
# PreToolUse (Bash and Grep): nudge toward the semantic code-search tools.
#
# UNIVERSAL — copy this file to any repo unchanged. The wording lives in
# context/prefer-serena.txt next to it; delete that file to switch the hook off
# without touching settings.json.
#
# Fires once per session, not once per search. The Bash arm still filters to
# commands that actually run a text search, so a session that never greps never
# sees the reminder at all.
set -u

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=./state.sh
. "$HERE/state.sh"

input=$(cat)

# The Grep tool is a text search by definition; a Bash call only counts when the
# command line actually invokes one.
if [ "$(printf '%s' "$input" | jq -r '.tool_name // empty' 2>/dev/null)" = "Bash" ]; then
    printf '%s' "$input" |
        jq -r '.tool_input.command // empty' 2>/dev/null |
        grep -Eq '(^|[|&; ])(grep|egrep|fgrep|rg)( |$)' || exit 0
fi

context=$(hook_context "$HERE/context/prefer-serena.txt")
[ -n "$context" ] || exit 0

hook_claim_once serena "$(hook_session_id "$input")" || exit 0

jq -cn --arg c "$context" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",additionalContext:$c},suppressOutput:true}'
