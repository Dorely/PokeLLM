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

### Run from Project Directory
When running the game, ensure you're in the correct directory to avoid configuration file errors:
```cmd
cd PokeLLM
dotnet run
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

## Enhanced Logging System

The PokeLLM application features a comprehensive logging system designed for debugging LLM function calls and understanding game flow. All logs are written to timestamped files in the `C:/Users/jonth/Documents/PokeLLM/Logs/` directory.

### Log File Structure

**Location**: `Logs/pokellm-{timestamp}-{uniqueId}.log`
- **Format**: UTF-8 text with structured entries
- **Rotation**: New file per session
- **Retention**: Files persist until manually deleted

### Log Entry Format

Each log entry follows this structure:
```
[YYYY-MM-DD HH:mm:ss.fff] LEVEL        Message content
```

**Components**:
- **Timestamp**: Precise to milliseconds for temporal analysis
- **Level**: 12-character padded log level for easy parsing
- **Message**: Context-specific content with structured data

### Log Levels and Their Usage

| Level | Usage | Example |
|-------|-------|---------|
| `DEBUG` | Internal application flow, configuration details | `[GameSetupPhaseService] Function invoking: create_character_class` |
| `USERINPUT` | All user input captured verbatim | User's actual input text |
| `LLMRESPONSE` | Complete LLM responses, including streaming content | Full assistant responses |
| `FUNCTIONCALL` | Function invocations with parameters and results | JSON-formatted function calls |
| `GAMESTATE` | Complete game state snapshots | JSON game state after each turn |
| `PROMPT` | System prompts and template processing | Final prompts sent to LLM |
| `ERROR` | Errors, exceptions, and failure conditions | Exception details and stack traces |
| `PHASETRANS` | Game phase transitions | Phase changes with reasoning |

### Enhanced Function Call Logging

The modern logging system uses **Function Invocation Filters** (replacing deprecated Kernel events) to capture:

#### Function Call Start
```
[YYYY-MM-DD HH:mm:ss.fff] DEBUG        [GameSetupPhaseService] Function invoking: create_character_class
[YYYY-MM-DD HH:mm:ss.fff] DEBUG        [GameSetupPhaseService] Function arguments: {"className":"Trainer","description":"A pokemon trainer class"}
[YYYY-MM-DD HH:mm:ss.fff] FUNCTIONCALL Function: create_character_class
Parameters: {"className":"Trainer","description":"A pokemon trainer class"}
Result: INVOKING
```

#### Function Call Completion
```
[YYYY-MM-DD HH:mm:ss.fff] DEBUG        [GameSetupPhaseService] Function invoked: create_character_class
[YYYY-MM-DD HH:mm:ss.fff] DEBUG        [GameSetupPhaseService] Function result length: 145 characters
[YYYY-MM-DD HH:mm:ss.fff] FUNCTIONCALL Function: create_character_class
Parameters: {"className":"Trainer","description":"A pokemon trainer class"}
Result: {"success":true,"message":"Character class created successfully"}
```

#### Function Call Errors
```
[YYYY-MM-DD HH:mm:ss.fff] ERROR        [GameSetupPhaseService] Error in function create_character_class: The ruleset currently does not recognize it as a valid class to add
[YYYY-MM-DD HH:mm:ss.fff] FUNCTIONCALL Function: create_character_class
Parameters: {"className":"Trainer","description":"A pokemon trainer class"}
Result: ERROR: The ruleset currently does not recognize it as a valid class to add
```

### Session Header Information

Each log file begins with session metadata:
```
================================================================================
PokeLLM Session Started: 2025-08-21 12:29:24
Debug Mode: False
Verbose Logging: False
Debug Prompts: False
Log File Path: C:\Users\jonth\Documents\PokeLLM\Logs\pokellm-2025-08-21_12-29-24-22f6d194.log
Environment Variables:
  POKELLM_DEBUG: not set
  POKELLM_VERBOSE: not set
  POKELLM_DEBUG_PROMPTS: not set
  POKELLM_LOG_PATH: not set
================================================================================
```

### Structured Data Formats

#### Game State Logs
Game state is logged as formatted JSON after each user interaction:
```json
{
  "sessionId": "0b85b69d-7df7-466e-92cf-84c9b8f0a33a",
  "gameId": "pokemon-adventure_20250821_122928",
  "gameTurnNumber": 8,
  "currentPhase": "GameSetup",
  "player": { ... },
  "worldLocations": { ... },
  "activeRulesetId": "pokemon-adventure"
}
```

#### Prompt Logs
System prompts are logged with type identification:
```
[YYYY-MM-DD HH:mm:ss.fff] PROMPT       Prompt Type: Final GameSetupPhase System Prompt
[YYYY-MM-DD HH:mm:ss.fff] PROMPT       ## Available Functions
[YYYY-MM-DD HH:mm:ss.fff] PROMPT       - Setting: search_existing_region_knowledge, set_region
[YYYY-MM-DD HH:mm:ss.fff] PROMPT       - Character Types: search_character_classes, create_character_class, set_player_character_class
```

### Debugging Function Call Issues

When analyzing function call problems:

1. **Search for `FUNCTIONCALL` entries** to see actual invocations
2. **Look for `ERROR` level entries** around failed function calls
3. **Check `DEBUG` entries** with function names for detailed flow
4. **Review `PROMPT` entries** to verify functions are advertised to the LLM
5. **Examine `GAMESTATE` logs** to understand context when functions failed

### Log Analysis Tips

- **Grep by function name**: `grep "create_character_class" logfile.log`
- **Filter by level**: `grep "FUNCTIONCALL" logfile.log`
- **Time-based analysis**: Use timestamps to trace execution flow
- **Error correlation**: Match ERROR entries with preceding FUNCTIONCALL entries
- **State progression**: Track game state changes between user inputs

This enhanced logging system provides complete visibility into LLM function calling behavior, making it much easier to diagnose issues like the "create the class" problem where functions appeared available but failed to execute properly.
