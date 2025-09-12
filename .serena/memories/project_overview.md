# Project Overview

- Purpose: PokeLLM is a .NET console application that interfaces with LLM APIs to run a structured Pokémon role‑playing game.
- Tech Stack: .NET 8 (C#), xUnit for tests, Microsoft.SemanticKernel (OpenAI, Ollama, Gemini, Qdrant connectors), Microsoft.Extensions.* (DI, Configuration, Options), Qdrant client, SQLite (Microsoft.Data.Sqlite).
- Entrypoint: `PokeLLM/Program.cs` (console app, streams responses, multi‑line user input, DI via `ServiceConfiguration`).
- Configuration: `PokeLLM/appsettings.json` controls LLM provider settings (OpenAI, Gemini, Ollama), Hybrid LLM/Embedding setup, and Qdrant host/port. Secrets (API keys) should be provided via `UserSecrets` or environment variables.
- Solution: `PokeLLM.sln` with two projects: `PokeLLM` (app) and `Tests` (xUnit tests).
- Structure:
  - `PokeLLM/Configuration/*` – DI and options binding (`ServiceConfiguration`, `ModelConfig`, `QdrantConfig`).
  - `PokeLLM/Orchestration/*` – `GameController`, orchestration services, unified context service.
  - `PokeLLM/GameLogic/*` – services for world, characters, combat, player/NPC/Pokémon management, information mgmt.
  - `PokeLLM/GameState/*` – game state repository + models.
  - `PokeLLM/LLM/*` – provider interfaces and implementations (OpenAI, Ollama, Gemini).
  - `PokeLLM/VectorStore/*` – Qdrant vector store interface/impl + models.
  - `PokeLLM/Plugins/*` – phase plugins (setup, exploration, combat, level‑up, world gen, unified context) + DTOs.
  - `PokeLLM/Prompts/*` – phase prompt markdown files copied into output.
- Tests: `Tests/*.cs` with xUnit, Moq. Project reference to app project.
- Run Profile: Console app prompts for multi‑line input; type a blank line to send; type `exit` to quit.
