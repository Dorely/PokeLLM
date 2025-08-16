# PokeLLM Architecture Structure

## Directory Structure
```
PokeLLM/
├── Configuration/          # Service configuration and DI setup
├── GameData/              # Game data files
├── GameLogic/             # Core game logic services
├── GameRules/             # Dynamic ruleset system
├── GameState/             # Game state persistence (SQLite)
├── LLM/                   # LLM provider abstractions and implementations
├── Orchestration/         # Game phase orchestration and context management
├── Plugins/               # Semantic Kernel plugins for each game phase
├── Prompts/               # System prompts for each game phase (.md files)
├── Rulesets/              # JSON ruleset definitions
├── VectorStore/           # Qdrant vector storage integration
├── Program.cs             # Main entry point
└── appsettings.json       # Configuration file

Tests/
├── TestData/              # Test data files
├── TestModels/            # Test model definitions
├── TestUtilities/         # Test helper utilities
└── Various test files     # Unit and integration tests
```

## Core Components

### Game Controller System
- Main game flow controller that routes player input
- Handles phase transitions and game state management
- Coordinates between different game phases

### LLM Provider System
- Abstracted provider interface supporting multiple LLM services
- Configurable main LLM and embedding providers
- Flexible provider mixing (e.g., Gemini for chat, Ollama for embeddings)

### Dynamic Ruleset System
- JSON-based game mechanics configuration
- JavaScript rule engine for validation
- Function generation from ruleset definitions
- Supports multiple game types (Pokemon, D&D 5e, etc.)

### Plugin Architecture
- Phase-specific plugins with dedicated functions
- Semantic Kernel integration for LLM function calling
- Context management and gathering plugins

### Vector Storage
- Qdrant integration for semantic search
- Embedding storage for lore and descriptions
- Supports both OpenAI and Ollama embeddings