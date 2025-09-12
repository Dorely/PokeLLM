# PokeLLM Project Guide

PokeLLM is a .NET 8 console application that runs a structured, phase‑based Pokémon role‑playing game powered by LLMs. The game streams responses, maintains state on disk, and uses a vector store to ground world, rules, and lore.

## What You Can Do
- Explore a generated world, talk to NPCs, and engage in combat.
- Create a character and choose a region, then auto‑generate a rich world.
- Progress through phases: setup → world generation → exploration → combat → level‑up.

## Run The Game
- Requirements: .NET 8, optional Qdrant running at the host/port in `PokeLLM/appsettings.json`.
- Start: `dotnet run --project PokeLLM/PokeLLM.Game.csproj`
- Input: type multi‑line messages; press an empty line to send. Type `exit` to quit.

## Configuration
- File: `PokeLLM/appsettings.json`
- LLM providers: `OpenAi`, `Ollama`, `Gemini` sections define API keys, model IDs, and (for Ollama) endpoint.
- Hybrid embedding: controlled via `Hybrid` and wired in `ServiceConfiguration` to pick the embedding provider separately from the chat model.
- Vector store: `Qdrant` host/port; embeddings dimensions must match your embedding model.
  - Typical dimensions: OpenAI `text-embedding-3-small` → 1536; Ollama `nomic-embed-text` → 768.

## Architecture Overview
- Orchestration: `GameController` selects a `PhaseService` based on `GamePhase` and streams replies.
- Phases: `PhaseService` loads a phase prompt and exposes plugin tools to the LLM via Semantic Kernel.
- Plugins: Each phase has a plugin class with `KernelFunction` methods to read/write state, rules, lore, and world data.
- Unified Context: `UnifiedContextService` compacts chat history and updates `GameState.CurrentContext` across transitions.
- LLM Layer: `ILLMProvider` abstracts provider specifics; implementations configure Semantic Kernel and execution settings.
- Vector Store: `QdrantVectorStoreService` stores and searches entities, locations, lore, rules, and narrative logs.
- Persistence: `GameStateRepository` saves a single JSON file at `GameData/game_current_state.json`.

## Key Files
- `PokeLLM/Program.cs` – builds DI container and runs the console loop.
- `PokeLLM/Configuration/ServiceConfiguration.cs` – DI + provider selection, options binding, and registrations.
- `PokeLLM/Orchestration/GameController.cs` – routes input to the active phase and handles transitions.
- `PokeLLM/Orchestration/PhaseService.cs` – sets up SK kernel, prompt, chat history, and streaming.
- `PokeLLM/Orchestration/UnifiedContextService.cs` – history compression and context updates.
- `PokeLLM/Plugins/*` – phase plugins (setup, world gen, exploration, combat, level‑up).
- `PokeLLM/VectorStore/QdrantVectorStoreService.cs` – Qdrant collections and searches.
- `PokeLLM/GameState/GameStateRepository.cs` – JSON persistence for game state.
- `PokeLLM/Prompts/*.md` – per‑phase system prompts injected by `PhaseService`.

## Project Structure (high level)
- `Configuration/` – options, provider selection, Qdrant config, DI.
- `Orchestration/` – controller, phase services, unified context subroutine.
- `GameLogic/` – domain services (world, characters, combat, info, player/NPC/Pokémon).
- `LLM/` – provider interface and implementations (OpenAI, Ollama, Gemini).
- `VectorStore/` – vector models and Qdrant integration.
- `GameState/` – game state model and repository.
- `Plugins/` – LLM‑callable functions for each phase.
- `Prompts/` – markdown prompts copied to output at build.
- `Tests/` – xUnit tests and helpers.

## Development Workflow
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project PokeLLM/PokeLLM.Game.csproj`
- Format (optional): `dotnet format`

Conventions
- C#/.NET 8, DI via `Microsoft.Extensions.DependencyInjection`.
- Interface prefix `I*`, PascalCase types/members, async method suffix `Async`.
- Options bound from `appsettings.json`; keep secrets in UserSecrets or env vars.

## Extending The Game
- New Phase: add a plugin (with `KernelFunction` methods), a prompt in `Prompts/`, and register it in `PhaseServiceProvider`.
- New Provider: implement `ILLMProvider` (chat + embeddings) and wire it in `ServiceConfiguration`.
- New Vector Data: add a record model, collection handling in `QdrantVectorStoreService`, and relevant plugin methods.

## Troubleshooting
- No output / errors about tools: ensure the phase prompt exists and the plugin is registered in `PhaseServiceProvider`.
- Vector store errors: Qdrant must be reachable at the configured host/port; embedding dimensions must match the model used to create collections.
- Missing API keys: set with UserSecrets or environment variables; do not commit secrets to source control.

---

Footnotes (Agent Tools)
- Serena MCP: Project indexing and safe edits are available to agents (symbol search, references, and structured edits). Use when refactoring or navigating the codebase.
- Context7 Docs: Agents can resolve a library (e.g., `/microsoft/semantic-kernel`) and fetch focused docs when needed.
