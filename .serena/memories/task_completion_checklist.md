# Task Completion Checklist

When a coding task is complete:
1. `dotnet build MedAssist.slnx` — **0 warnings** (TreatWarningsAsErrors is ON; a warning fails the build).
2. `dotnet test MedAssist.Tests` — all green.
3. New logic was built **test-first** (superpowers:test-driven-development): write the test, watch
   it fail, then implement.
4. For UI/behaviour changes, **verify live** when feasible — docker `web` + Playwright at
   http://localhost:8080 — not just unit tests. (Note the WSL2 host-network build gotcha in
   `mem:suggested_commands`.)
5. Update the OpenSpec change `tasks.md`; when the change is fully done, `openspec archive <name> -y`
   (syncs delta specs into `openspec/specs/`).
6. Commit via git **only with explicit user approval**; message ends with the `Co-Authored-By` trailer.
7. Save durable learnings to Serena memory (`write_memory`) — this project uses Serena for memory,
   not the built-in store (see CLAUDE.md "Memory").
