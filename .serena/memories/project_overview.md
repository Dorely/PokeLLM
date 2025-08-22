# PokeLLM Project Overview

## Purpose
PokeLLM is a C# console application that creates a structured Pokemon role-playing game powered by Large Language Models (LLM). The application supports multiple LLM providers (OpenAI, Ollama, Gemini) with flexible provider configuration and uses a layered architecture with game controllers, specialized services, and vector storage.

## Tech Stack
- **Language**: C# (.NET 8.0)
- **LLM Framework**: Microsoft Semantic Kernel 1.61.0
- **LLM Providers**: OpenAI, Ollama, Gemini (Google AI)
- **Vector Database**: Qdrant
- **Database**: SQLite via Microsoft.Data.Sqlite
- **Configuration**: Microsoft.Extensions.Configuration with JSON files and user secrets
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Logging**: Microsoft.Extensions.Logging with Console provider
- **Testing**: xUnit with Moq for mocking

## Architecture
The project follows a **multi-agent architecture** built on Semantic Kernel:

### Core Components
1. **Agents** (`PokeLLM.Agents`) - Specialized AI agents for different game functions
   - `SetupAgent` - Initial game setup and Adventure Module generation
   - `GMSupervisorAgent` - Central coordinator for all agent interactions
   - `NarratorAgent` - Creates immersive prose and descriptions
   - `MechanicsAgent` - Exclusive authority for all mechanical calculations

2. **Controllers** (`PokeLLM.Controllers`) - REST-like controllers for UI interaction
   - `GameController` - Main game session management

3. **UI** (`PokeLLM.UI`) - User interface layer
   - `ConsoleGameUI` - Console-based interface

4. **State Management** (`PokeLLM.State`, `PokeLLM.GameState`) - Game state and data persistence
   - Event-sourced state management
   - Immutable state snapshots
   - Vector storage integration

5. **Configuration** (`PokeLLM.Game.Configuration`) - Service configuration and DI setup

## Key Patterns
- **Agent-based Architecture**: Each AI agent has specialized responsibilities
- **Event Sourcing**: Game events are logged for state reconstruction
- **Dependency Injection**: All services registered through DI container
- **Async/Await**: Streaming responses via `IAsyncEnumerable`
- **Immutable State**: Game state snapshots are immutable records