# PokeLLM — LLM Developer Agent Guide

This document is the operating guide for the LLM developer agent working in this repository. It defines the required completion criteria, validation steps, and working practices for changes to PokeLLM.

## Mission
- Implement precise, minimal changes to the .NET 8 PokeLLM console app without breaking build or tests.
- Maintain architecture and conventions: DI, phase services, plugins, semantic‑kernel usage, and vector store abstractions.

## Definition of Done (Mandatory)
- Build passes with zero errors: run `dotnet build` at repo root and ensure success.
- All unit tests pass: run `dotnet test` and ensure 100% pass, no failures.
- No secrets committed: keep API keys in UserSecrets or env vars, not in source.
- Scope discipline: changes are limited to the task; no unrelated refactors.

Tasks cannot be considered complete until both build and tests succeed. 
Tests can and should be modified to match actual use cases. Obsolete tests should be amended or deleted. 

## How To Validate (Every Task)
- Restore: `dotnet restore PokeLLM.sln`
- Build: `dotnet build`
- Test: `dotnet test --nologo --verbosity minimal`
- Optional smoke run: `dotnet run --project PokeLLM/PokeLLM.Game.csproj`

If build or tests fail, fix the cause, then re‑run until both pass. Do not mark the task complete otherwise.

## Working Workflow
- Understand: read impacted files and adjacent services/plugins.
- Plan: outline minimal steps; avoid broad changes.
- Implement: keep edits focused; prefer constructor/DI patterns; respect async naming and error handling patterns.
- Validate: restore, build, and test as above; iterate until green.
- Document: update `README.md` or this guide only if behavior or workflows change.

## Run And Configure
- Run locally: `dotnet run --project PokeLLM/PokeLLM.Game.csproj`
- Input model: multi‑line input; send with empty line; `exit` quits.
- Config file: `PokeLLM/appsettings.json`
  - Providers: `OpenAi`, `Ollama`, `Gemini` (API keys, model IDs, Ollama endpoint)
  - Embeddings: Hybrid selection in DI; keep dimensions consistent (OpenAI 1536, Ollama 768)
  - Vector store: `Qdrant` host/port; app doesn’t require Qdrant to build or run tests

## Architecture Pointers
- `Orchestration`: `GameController`, `PhaseService`, `UnifiedContextService`
- `Plugins`: phase‑specific `KernelFunction` methods for tools/state updates
- `LLM`: `ILLMProvider` + implementations; configure Semantic Kernel and execution settings
- `VectorStore`: Qdrant service and models; tests use mocks; integration tests requiring Qdrant are commented out
- `GameState`: JSON persistence via `GameStateRepository`

## Coding Conventions
- C#/.NET 8; PascalCase for types/members; `I*` interfaces; async methods end with `Async`.
- Use DI via `Microsoft.Extensions.DependencyInjection`; register in `ServiceConfiguration`.
- Options bound from `appsettings.json`; secrets via UserSecrets or environment variables.
- Favor small, focused services and exception‑safe plugin functions (return structured error info where applicable).

## Guardrails
- Don’t introduce external service dependencies to unit tests.
- Don’t modify commented integration tests unless explicitly tasked.
- Don’t commit credentials or proprietary prompts.
- Don’t expand scope (e.g., renames or reorganizations) without instruction.

## Quick Repo Map
- `PokeLLM/Program.cs` — DI setup + console loop
- `PokeLLM/Configuration/ServiceConfiguration.cs` — provider wiring, options, registrations
- `PokeLLM/Orchestration/*` — controller, phases, unified context
- `PokeLLM/Plugins/*` — LLM‑callable tools per phase
- `PokeLLM/VectorStore/*` — Qdrant models + service
- `PokeLLM/GameState/*` — models + repository
- `PokeLLM/Prompts/*` — system prompts per phase
- `Tests/*` — active unit tests (mocks) and commented integration tests

## AGENTS.md Updates
- When major architectural changes occur that would contradict anything in these agent instructions, you must update this file (AGENTS.md).

## Agent Tools
- Serena MCP: Primarily use for repository indexing, semantic search, symbol/reference queries, and reading and saving project memories. DO NOT use Serena's editing tools (no inserts/replacements) in this repo.
- Context7 Docs: Mandatory for fetching up-to-date documentation before writing code that uses libraries—especially the Semantic Kernel (`/microsoft/semantic-kernel`), and also any other libraries involved.
