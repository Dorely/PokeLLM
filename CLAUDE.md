# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PokeLLM is a C# console application that creates a structured Pokemon role-playing game powered by Large Language Models (LLM). The application supports multiple LLM providers (OpenAI, Ollama, Gemini) with flexible provider configuration and uses a layered architecture with game controllers, specialized services, and vector storage.

## Essential Commands

### Build and Run
```cmd
dotnet build
dotnet run --project PokeLLM/PokeLLM.Game.csproj
```

### Testing
```cmd
dotnet test Tests/Tests.csproj
```

### Clean and Rebuild
```cmd
dotnet clean
dotnet build --no-restore
```

## Architecture Overview

### Core Components

**Game Controller System** (`GameLogic/GameController.cs`):
- Main entry point that routes player input based on game status
- Coordinates between setup, world generation, and gameplay phases
- Handles automatic phase transitions
- Implements streaming responses with cancellation support

**Game Logic Services** (`GameLogic/`):
- `GameSetupService`: Handles initial game configuration
- `WorldGenerationService`: Manages world creation process
- `UnifiedContextService`: Centralized context management
- Specialized management services for characters, NPCs, Pokemon, and world state

**Orchestration System** (`Orchestration/OrchestrationService.cs`):
- Focused on gameplay phase management during exploration/combat
- Handles context gathering and management subroutines
- Maintains separate Semantic Kernel instances for each game phase
- Implements streaming responses with error handling

**Game Phases**:
- GameSetup: Initial setup (replaces GameCreation/CharacterCreation)
- WorldGeneration: World building
- Exploration: Main gameplay phase
- Combat: Battle encounters
- LevelUp: Character progression

**LLM Provider System** (`LLM/`):
- `ILLMProvider` interface with three implementations:
  - `OpenAiLLMProvider`: OpenAI GPT models
  - `OllamaLLMProvider`: Local Ollama models
  - `GeminiLLMProvider`: Google Gemini models
- Flexible provider system with separate main LLM and embedding providers
- Provider selection configured in `ServiceConfiguration.cs` via `MAIN_LLM_PROVIDER` and `EMBEDDING_PROVIDER` constants
- Supports mixing providers (e.g., Gemini for chat, Ollama for embeddings)

**Plugin System** (`Plugins/`):
- Each game phase has a dedicated plugin with specific functions
- `GameSetupPhasePlugin`: Initial game configuration
- `WorldGenerationPhasePlugin`: World creation functions
- `ExplorationPhasePlugin`: Main gameplay interactions
- `CombatPhasePlugin`: Battle mechanics
- `LevelUpPhasePlugin`: Character progression
- `UnifiedContextPlugin`: Centralized context management
- `ContextGatheringPlugin`: Lightweight context assembly
- `ContextManagementPlugin`: Comprehensive context validation
- `ChatManagementPlugin`: Chat history compression

**Game State Management** (`GameState/`):
- `GameStateRepository`: SQLite-based persistence
- `GameStateModel`: Core game state structure
- Tracks locations, NPCs, Pokemon, player data, and narrative events

**Vector Storage** (`VectorStore/`):
- `QdrantVectorStoreService`: Manages embedding storage and retrieval
- Supports both OpenAI and Ollama embeddings
- Used for lore, descriptions, and contextual information

### Configuration System

**Service Configuration** (`Configuration/ServiceConfiguration.cs`):
- Change `MAIN_LLM_PROVIDER` constant to switch between "OpenAI", "Ollama", or "Gemini"
- Change `EMBEDDING_PROVIDER` constant to switch between "OpenAI" or "Ollama" for embeddings
- Uses `FlexibleProviderConfig` for independent LLM and embedding provider configuration
- Auto-configures embedding dimensions and default models based on provider selection

**Settings Files**:
- `appsettings.json`: Main configuration
- User secrets for API keys (project has UserSecrets enabled)
- Configuration sections: OpenAI, Ollama, Gemini, Qdrant

### Context Management Flow

The application uses a layered architecture with centralized context management:

1. **Game Controller Layer**: Routes input based on game status (setup, world generation, gameplay)
2. **Service Layer**: Specialized services handle specific game logic areas
3. **Unified Context Service**: Centralized context management across all phases
4. **Orchestration Layer**: Manages complex gameplay interactions during exploration/combat
5. **Plugin Layer**: Provides LLM-accessible functions for each game phase

### Key Patterns

**Dependency Injection**: All services registered in `ServiceConfiguration.ConfigureServices()`

**Error Handling**: Comprehensive error handling with debug logging throughout orchestration

**Tool Call Sequences**: Special handling for Semantic Kernel function calling to prevent API errors

**Game Status Management**: Controller-based routing with automatic phase transitions based on game completion status

## Development Notes

### Testing Strategy
- Unit tests in `Tests/` project using xUnit and Moq
- Focus on dependency resolution, configuration, and vector store operations

### Prompt System
- Each game phase has a corresponding `.md` file in `Prompts/` directory
- System prompts define behavior and available functions for each phase
- Prompts are loaded dynamically by `OrchestrationService`

### State Management
- Game state persisted automatically after each turn
- Turn numbers incremented for each player interaction
- Phase transitions trigger context management for consistency

### Vector Database Integration
- Qdrant used for semantic search and embedding storage
- Supports both local and cloud Qdrant instances
- Embedding generation handled by configured LLM provider

## Common Issues

### LLM Provider Configuration
- Ensure API keys are properly set in user secrets or appsettings.json
- For mixed providers, verify configurations for both main LLM and embedding providers
- Check that Ollama server is running if using Ollama for either main LLM or embeddings
- Verify Gemini API key if using Gemini provider

### Vector Store Connection
- Verify Qdrant configuration in appsettings.json
- Ensure collection exists and dimensions match embedding model

### Build Dependencies
- Project targets .NET 8.0
- Uses preview packages for Semantic Kernel connectors
- All content files (prompts, appsettings) are copied to output directory