# AIDoctorAssistant — Project Overview

## Status
Early stage / greenfield. No source code yet as of 2026-05-15.

## Purpose
AI-powered doctor/medical assistant application. Details TBD — to be fleshed out via OpenSpec explore/propose workflow.

## Tech Stack
- Platform: .NET (dotnet) — confirmed by user
- Language: C# (inferred from dotnet + csharp-lsp plugin)
- AI integration planned (dotnet-ai plugin enabled)
- Web framework: likely ASP.NET Core (dotnet-aspnet plugin enabled)

## Workflow
User is following: Explore (OpenSpec) → Write Plan (Superpowers) → Write Spec (OpenSpec propose) → Implement (Serena + dotnet skills)

## Key Tools in Use
- Serena: semantic code editing (replaces raw Bash for file ops)
- OpenSpec: spec and change management (skills in .claude/skills/)
- Superpowers: plan writing
- dotnet-* plugins: full .NET skill suite
- csharp-lsp: C# language server
