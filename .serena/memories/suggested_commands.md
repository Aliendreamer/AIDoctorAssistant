# Suggested Commands

## Build / test (.NET 10, MedAssist.slnx)

- `dotnet build MedAssist.slnx` — 0 warnings expected (**TreatWarningsAsErrors is ON**).
- `dotnet test MedAssist.Tests` — xUnit.
- `dotnet test MedAssist.Tests --filter "FullyQualifiedName~<ClassName>"` — one test class.
- `dotnet build MedAssist.Web/MedAssist.Web.csproj -clp:ErrorsOnly` — quick web-only compile.
- `dotnet build`/`dotnet test` are fine to run without user approval.

## Docker (needs explicit user approval per CLAUDE.md)

- PCC stack must be up first (external network `personalcommandcenter_default`); the app is at
  <http://localhost:8080>.
- `docker compose up -d --build web` — normal path.
- **WSL2 GOTCHA:** `docker build`'s NuGet restore blackholes on the default-bridge MTU
  ("connection reset by peer" mid-restore). Build with host networking instead:
  `DOCKER_BUILDKIT=1 docker build --network=host -t aidoctorassistant-web -f MedAssist.Web/Dockerfile .`
  then `docker compose up -d --no-build web`. (Permanent fix: daemon.json `"mtu": 1492`.)
- Default dev login: see README "Default credentials" (admin, seeded from git-crypt `Auth:Users`).
- Read-only checks (`docker ps`, `docker logs`) still require stating intent first.

## Git (needs explicit user approval)

- Inspection: `git status` / `git log` / `git diff`. Commit messages end with the
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` trailer.

## OpenSpec

- `openspec list` · `openspec validate <change> --strict` · `openspec archive <change> -y`.
- Skills: `/opsx:explore`, `/opsx:propose`, `/opsx:apply`, `/opsx:archive`.

## Live verification (UI/behaviour changes)

- Drive the running app in Playwright at <http://localhost:8080> (login admin / seeded pwd) — see
  `mem:task_completion_checklist`.
