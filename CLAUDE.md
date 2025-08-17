# CLAUDE.md

This file provides guidance to Claude Code when working with the PokeLLM codebase.

## Project Overview

PokeLLM is a C# console application that creates a Pokemon RPG powered by Large Language Models. It features a dynamic ruleset system, multiple LLM providers (OpenAI, Ollama, Gemini), and a layered architecture with game controllers and specialized services.

## Essential Commands

### Build and Run
```cmd
dotnet build
dotnet run --project PokeLLM/PokeLLM.Game.csproj
```

### Testing
```cmd
dotnet test Tests/PokeLLM.Tests.csproj
```

### Clean and Rebuild
```cmd
dotnet clean
dotnet build --no-restore
```

### Debug Mode
Enable comprehensive debug logging and enhanced prompts:
```cmd
set POKELLM_DEBUG=true
dotnet run --project PokeLLM/PokeLLM.Game.csproj
```

**Debug Logs Location**: `Logs/pokellm-debug-{timestamp}.log` in the application directory
- Contains all user input, LLM responses, function calls, and game state changes
- Debug prompts force LLM to list all available functions and be verbose with explanations
- See `DEBUG_MODE.md` for complete documentation

## Architecture Overview

### Core Components

**Phase Service** (`Orchestration/PhaseService.cs`):
- Main orchestration component for all game phases
- Handles LLM integration via Semantic Kernel
- Manages context gathering and streaming responses
- Integrates with dynamic ruleset system

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

**Dynamic Ruleset System**:
- Game mechanics defined in JSON ruleset files
- `IRulesetManager`: Loads and applies rulesets
- `IDynamicFunctionFactory`: Generates LLM functions from rulesets
- Functions generated dynamically replacing static plugins
- Current ruleset: `pokemon-adventure.json`

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

### Application Flow

1. **Entry Point**: `Program.cs` initializes services and loads the pokemon-adventure ruleset
2. **Game Controller**: Routes input based on current game phase
3. **Phase Service**: Orchestrates LLM interactions with dynamic functions
4. **State Management**: SQLite persistence with automatic saves
5. **Streaming Responses**: Real-time LLM output to console

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
- System prompts defined in ruleset JSON files
- Phase-specific prompts generated dynamically
- Context templates configurable per ruleset

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
- Uses Semantic Kernel for LLM integration
- Content files (rulesets, appsettings) copied to output directory

## Dynamic Ruleset System

Game mechanics are completely configurable through JSON ruleset files located in `PokeLLM/Rulesets/`.

### Key Components
- `IRulesetManager`: Loads and applies rulesets
- `IDynamicFunctionFactory`: Generates LLM functions from rulesets  
- `RulesetManagementPlugin`: Handles ruleset operations
- `GameStateModel.RulesetGameData`: Dynamic game data storage

### Ruleset Structure
- **Metadata**: Name, version, description
- **Game Data**: Species, moves, items, type effectiveness
- **Function Definitions**: LLM-callable functions per phase
- **Prompt Templates**: Phase-specific system prompts
- **Validation Rules**: Game mechanics compliance

### Current Ruleset
`pokemon-adventure.json` - Complete Pokemon RPG with:
- 151 original Pokemon species with stats and movesets
- Type effectiveness chart and battle mechanics
- Trainer classes and progression systems
- Items, abilities, and status effects
- All 5 game phases with 50+ dynamic functions
