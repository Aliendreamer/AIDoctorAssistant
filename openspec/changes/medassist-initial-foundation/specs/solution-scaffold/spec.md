## ADDED Requirements

### Requirement: Solution contains four projects with correct types
The solution SHALL contain exactly four projects: `MedAssist.Shared` (class library), `MedAssist.AI` (class library), `MedAssist.Web` (Blazor Server app), `MedAssist.Indexer` (Worker Service). All projects SHALL target `net10.0`.

#### Scenario: Solution builds cleanly
- **WHEN** `dotnet build` is run at solution root
- **THEN** all four projects compile with zero errors and zero warnings

### Requirement: Dependency graph enforces layer boundaries
`MedAssist.AI` SHALL reference only `MedAssist.Shared`. `MedAssist.Web` SHALL reference `MedAssist.AI` and `MedAssist.Shared`. `MedAssist.Indexer` SHALL reference only `MedAssist.Shared`. `MedAssist.AI` and `MedAssist.Indexer` SHALL NOT reference each other.

#### Scenario: Circular dependency is absent
- **WHEN** solution dependency graph is inspected
- **THEN** no cycles exist and Indexer has no reference to AI project

### Requirement: Shared code conventions are enforced
All projects SHALL use `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, and `EnforceCodeStyleInBuild true`. An `.editorconfig` SHALL be present at solution root defining C# style rules.

#### Scenario: Nullable violations fail the build
- **WHEN** nullable reference type is used without null check
- **THEN** build fails with a nullable warning treated as error

### Requirement: Docker Compose runs all runtime services
A `docker-compose.yml` at solution root SHALL define services: `web` (MedAssist.Web), `indexer` (MedAssist.Indexer), `qdrant`, `ollama`, `prometheus`, `grafana`. All services SHALL be on a shared Docker network.

#### Scenario: Full stack starts with one command
- **WHEN** `docker compose up` is run
- **THEN** all services start, health checks pass, and web UI is reachable at `http://localhost:8080`

### Requirement: Solution includes Dockerfile per runnable project
`MedAssist.Web` and `MedAssist.Indexer` SHALL each have a multi-stage `Dockerfile` using the official `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image.

#### Scenario: Docker images build without error
- **WHEN** `docker build` is run for Web and Indexer
- **THEN** both images build successfully and run without missing dependency errors
