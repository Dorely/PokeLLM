# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PokeLLM is a C# console application that creates a structured Pokemon role-playing game powered by Large Language Models (LLM). The application supports multiple LLM providers (OpenAI, Ollama, or Hybrid mode) and uses a sophisticated orchestration system with game phases, context management, and vector storage.

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

**Orchestration System** (`Orchestration/OrchestrationService.cs`):
- Central game loop that manages phase transitions
- Handles context gathering and management subroutines
- Maintains separate Semantic Kernel instances for each game phase
- Implements streaming responses with error handling

**Game Phases**:
- GameCreation: Initial setup
- CharacterCreation: Player character setup
- WorldGeneration: World building
- Exploration: Main gameplay phase
- Combat: Battle encounters
- LevelUp: Character progression

**LLM Provider System** (`LLM/`):
- `ILLMProvider` interface with three implementations:
  - `OpenAiLLMProvider`: OpenAI GPT models
  - `OllamaLLMProvider`: Local Ollama models
  - `HybridLLMProvider`: OpenAI for chat + Ollama for embeddings
- Provider selection configured in `ServiceConfiguration.cs` via `LLM_PROVIDER` constant

**Plugin System** (`Plugins/`):
- Each game phase has a dedicated plugin with specific functions
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
- Change `LLM_PROVIDER` constant to switch between providers
- Supports "OpenAI", "Ollama", or "Hybrid" modes
- Auto-configures embedding dimensions and default models

**Settings Files**:
- `appsettings.json`: Main configuration
- User secrets for API keys (project has UserSecrets enabled)
- Configuration sections: OpenAI, Ollama, Hybrid, Qdrant

### Context Management Flow

The application uses a sophisticated context management system:

1. **Context Gathering**: Lightweight pre-processing to identify relevant context
2. **Game Phase Processing**: Main LLM interaction with full plugin access  
3. **Context Management**: Post-processing for consistency and state updates
4. **Chat History Management**: Automatic compression when history gets large

### Key Patterns

**Dependency Injection**: All services registered in `ServiceConfiguration.ConfigureServices()`

**Error Handling**: Comprehensive error handling with debug logging throughout orchestration

**Tool Call Sequences**: Special handling for Semantic Kernel function calling to prevent API errors

**Phase Transitions**: Automatic detection and recursive orchestration when game phases change

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
- For Hybrid mode, verify both OpenAI and Ollama configurations
- Check that Ollama server is running if using Ollama provider

### Vector Store Connection
- Verify Qdrant configuration in appsettings.json
- Ensure collection exists and dimensions match embedding model

### Build Dependencies
- Project targets .NET 8.0
- Uses preview packages for Semantic Kernel connectors
- All content files (prompts, appsettings) are copied to output directory