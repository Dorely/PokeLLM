# New Game Loop Architecture Implementation

## Overview
I have successfully rearchitected the main game loop according to your specifications. The new architecture follows this flow:

1. **Player provides input** (e.g., "I enter the cave to see if team rocket went inside")
2. **Context Gathering Subroutine** processes the input with access to game state and vector database
3. **Game Loop Service** processes the gathered context (placeholder for future enhancements)
4. **Main Game Chat** receives both the original input and the structured context to orchestrate the game

## New Components Added

### 1. GameContext Model (`GameStateModel.cs`)
```csharp
public class GameContext
{
    public Dictionary<string, object> RelevantEntities { get; set; } = new();
    public List<string> MissingEntities { get; set; } = new();
    public List<string> GameStateUpdates { get; set; } = new();
    public List<VectorStoreResult> VectorStoreData { get; set; } = new();
    public string ContextSummary { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
}
```

### 2. Context Gathering Subroutine Prompt (`ContextGatheringSubroutine.md`)
- Specialized prompt for gathering context
- Instructs the LLM to search game state and vector store
- Returns structured GameContext object
- Focuses on entity validation and context preparation

### 3. Context Gathering Service (`ContextGatheringService.cs`)
- **Interface**: `IContextGatheringService`
- **Implementation**: `ContextGatheringService`
- Uses its own Kernel with access to all plugins
- Processes player input to gather comprehensive context
- Returns structured GameContext for main game chat

### 4. Game Loop Service (`GameLoopService.cs`)
- **Interface**: `IGameLoopService`
- **Implementation**: `GameLoopService`
- Orchestrates the new flow:
  1. Calls Context Gathering Service
  2. Processes gathered context (placeholder)
  3. Enhances player input with context
  4. Passes to main game chat

### 5. Updated Program.cs
- Now uses `IGameLoopService` instead of direct `ILLMProvider`
- All player input goes through the new architecture
- Welcome message also flows through the system

### 6. Updated ServiceConfiguration.cs
- Registers both new services in dependency injection
- Maintains proper service ordering to avoid circular dependencies

## Architecture Flow

```
Player Input
    ?
GameLoopService.ProcessPlayerInputAsync()
    ?
ContextGatheringService.GatherContextAsync()
    ? (uses separate Kernel with plugins)
Context Gathering Subroutine LLM
    ? (searches game state & vector store)
GameContext (structured object)
    ?
GameLoopService.ProcessGatheredContextAsync() [placeholder]
    ?
Enhanced Input (original + context)
    ?
Main Game Chat (ILLMProvider)
    ?
Response to Player
```

## Key Features

### Context Gathering Capabilities
- **Entity Analysis**: Identifies characters, locations, items, Pokémon mentioned
- **Existence Checking**: Verifies entities exist in game state/vector store
- **Missing Entity Detection**: Flags entities that need to be created
- **Vector Store Research**: Searches for lore, descriptions, background info
- **Context Validation**: Ensures consistency between game state and vector store
- **Structured Output**: Returns organized, relevant context

### Enhanced Input Format
The main game chat now receives:
- Original player input
- Context summary
- Relevant entities found
- Missing entities
- Game state updates made
- Vector store information
- Recommended actions

### Placeholder for Future Enhancements
The `ProcessGatheredContextAsync` method is a placeholder where you can add:
- Additional validation
- Context transformation
- Priority sorting
- Context caching
- Performance optimizations

## Benefits

1. **Separation of Concerns**: Context gathering is separate from game orchestration
2. **Comprehensive Context**: LLM has access to all relevant information before responding
3. **Consistency**: Ensures entities exist and are properly referenced
4. **Extensibility**: Easy to enhance context processing in the future
5. **Maintainability**: Clear separation between research and game master roles

## Example Usage

When a player types: "I enter the cave to see if Team Rocket went inside"

1. **Context Gathering** finds:
   - Cave location details from current location
   - Team Rocket faction information from vector store
   - Any Team Rocket NPCs in the area
   - Related lore about Team Rocket activities

2. **Enhanced Input** includes:
   - Original: "I enter the cave to see if Team Rocket went inside"
   - Context: Cave exists as northern exit, Team Rocket is criminal organization...
   - Entities: Cave (loc_mysterious_cave), Team Rocket (faction_team_rocket)
   - Recommendations: Check for recent Team Rocket activity, describe cave interior...

3. **Main Game Chat** can now respond with full context about the cave, Team Rocket's presence/absence, and create an immersive narrative.

The architecture is now ready and all code compiles successfully!