# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Hard Rules

- **Never run `docker` or `git` commands without explicit user approval.** Always state the command and reason first, then wait for confirmation. This includes `docker compose up`, `docker compose build`, container restarts, `git commit`, `git push`, etc. Read-only commands (`docker logs`, `docker ps`) are lower risk but still require stating intent first.

## Project Status

This project is in its initial state. Only a README.md exists; no source code, tooling, or configuration has been added yet.

As the project grows, update this file with:
- Build, lint, and test commands
- How to run the dev server and individual tests
- Architecture decisions and key data flows
