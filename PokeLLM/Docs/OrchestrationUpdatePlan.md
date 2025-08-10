# PokeLLM Orchestration Restructuring Implementation Guide

## Architectural Changes Overview

### Current Architecture
```
GameCreation → WorldGeneration → CharacterCreation → Exploration
    ↓              ↓                 ↓              ↓
Context Gathering (pre-turn) + Context Management (post-phase) + Chat Management (periodic)
```

### Target Architecture
```
Pre-Game Setup (Separate Services):
GameSetup → WorldGeneration → [Unified Context Management after each turn]

Main Game Orchestration:
Exploration ↔ Combat ↔ LevelUp → [Unified Context Management after each turn]
```

### Key Changes
1. **Phase Consolidation**: Merge GameCreation + CharacterCreation → GameSetup
2. **Pre-Game Isolation**: Remove GameSetup/WorldGeneration from main orchestration 
3. **Context Unification**: Replace 3 context systems with 1 post-turn system
4. **Context Injection**: Inject CurrentContext into system prompts via {{context}} variable
5. **TrainerClass Integration**: Utilize existing TrainerClassData (renamed TrainerClass)

---

## Implementation Steps

## Phase 1: Data Model Updates

### Step 1.1: Update GameStateModel for TrainerClass Integration

**File:** `GameState/Models/GameStateModel.cs`

#### Required Changes:

1. **Rename existing TrainerClassData to TrainerClass**
```csharp
// Find existing TrainerClassData class and rename to TrainerClass
public class TrainerClass  // Renamed from TrainerClassData
{
    // Keep all existing properties as-is
}
```

2. **Update Player Model to reference TrainerClass**
```csharp
public class Player : Character
{
    // ADD: Reference to trainer class data
    public TrainerClass TrainerClassData { get; set; } = new();
    
    // KEEP: All existing properties unchanged
    // KEEP: Existing Stats, Level, Experience, etc.
    
    // ADD: Calculated effective stats property
    public Stats EffectiveStats => CalculateEffectiveStats();
    
    private Stats CalculateEffectiveStats()
    {
        return new Stats
        {
            Strength = Stats.Strength + (TrainerClassData.StatModifiers?.GetValueOrDefault("Strength", 0) ?? 0),
            Dexterity = Stats.Dexterity + (TrainerClassData.StatModifiers?.GetValueOrDefault("Dexterity", 0) ?? 0),
            Constitution = Stats.Constitution + (TrainerClassData.StatModifiers?.GetValueOrDefault("Constitution", 0) ?? 0),
            Intelligence = Stats.Intelligence + (TrainerClassData.StatModifiers?.GetValueOrDefault("Intelligence", 0) ?? 0),
            Wisdom = Stats.Wisdom + (TrainerClassData.StatModifiers?.GetValueOrDefault("Wisdom", 0) ?? 0),
            Charisma = Stats.Charisma + (TrainerClassData.StatModifiers?.GetValueOrDefault("Charisma", 0) ?? 0),
            CurrentVigor = Stats.CurrentVigor,
            MaxVigor = Stats.MaxVigor + (TrainerClassData.StatModifiers?.GetValueOrDefault("Vigor", 0) ?? 0)
        };
    }
}
```

### Step 1.2: Add CurrentContext Field

**File:** `GameState/Models/GameStateModel.cs`

```csharp
public class GameStateModel
{
    // ADD: Current context as simple string field
    public string CurrentContext { get; set; } = "";
    
    // KEEP: All existing properties unchanged
}
```

### Step 1.3: Update GamePhase Enum

**File:** `GameState/Models/GameStateModel.cs`

```csharp
// CHANGE: Remove GameCreation, CharacterCreation, WorldGeneration from enum
public enum GamePhase 
{ 
    Exploration, Combat, LevelUp  // Only gameplay phases remain in orchestration
}

// NOTE: GameSetup and WorldGeneration handled by separate services, not orchestration
```

---

## Phase 2: Context System Unification

### Step 2.1: Create UnifiedContextPlugin

**File:** `Plugins/UnifiedContextPlugin.cs`

```csharp
namespace PokeLLM.Game.Plugins;

/// <summary>
/// Unified context management system that runs after each turn to maintain 
/// world state consistency and current scene context. Does NOT return structured data - 
/// saves all context via function calls.
/// </summary>
public class UnifiedContextPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public UnifiedContextPlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService,
        IWorldManagementService worldManagementService,
        INpcManagementService npcManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _worldManagementService = worldManagementService;
        _npcManagementService = npcManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("gather_scene_context")]
    [Description("Gather comprehensive context about the current scene and environment")]
    public async Task<string> GatherSceneContext()
    {
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        
        // Get current location details
        var currentLocation = gameState.WorldLocations.GetValueOrDefault(gameState.CurrentLocationId);
        var vectorLocation = await _informationManagementService.GetLocationAsync(gameState.CurrentLocationId);
        
        // Get present NPCs with details
        var presentNpcs = new List<string>();
        if (currentLocation != null)
        {
            foreach (var npcId in currentLocation.PresentNpcIds)
            {
                var npcDetails = await _npcManagementService.GetNpcDetails(npcId);
                if (npcDetails != null)
                {
                    presentNpcs.Add($"{npcDetails.Name} ({npcDetails.CharacterDetails.Class})");
                }
            }
        }
        
        // Get present Pokemon
        var presentPokemon = new List<string>();
        if (currentLocation != null)
        {
            foreach (var pokemonId in currentLocation.PresentPokemonIds)
            {
                var pokemon = gameState.WorldPokemon.GetValueOrDefault(pokemonId);
                if (pokemon != null)
                {
                    presentPokemon.Add($"{pokemon.Species} (Level {pokemon.Level})");
                }
            }
        }
        
        var sceneContextData = new
        {
            location = new
            {
                name = currentLocation?.Name ?? "Unknown Location",
                description = vectorLocation?.Description ?? currentLocation?.Name ?? "",
                exits = currentLocation?.Exits ?? new Dictionary<string, string>(),
                pointsOfInterest = currentLocation?.PointsOfInterest ?? new List<string>()
            },
            presentNpcs = presentNpcs,
            presentPokemon = presentPokemon,
            environment = new
            {
                timeOfDay = gameState.TimeOfDay.ToString(),
                weather = gameState.Weather.ToString(),
                region = gameState.Region
            },
            recentEvents = gameState.RecentEvents.TakeLast(3).Select(e => e.EventDescription).ToList()
        };
        
        return JsonSerializer.Serialize(sceneContextData, _jsonOptions);
    }

    [KernelFunction("search_narrative_context")]
    [Description("Search for relevant narrative history and world knowledge for current scene")]
    public async Task<string> SearchNarrativeContext(
        [Description("Current scene elements to search for context")] List<string> sceneElements)
    {
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        var narrativeContext = new List<string>();
        
        // Search for relevant narrative memories
        foreach (var element in sceneElements)
        {
            var memories = await _informationManagementService.FindMemoriesAsync(
                gameState.SessionId, element, null, 0.7);
            narrativeContext.AddRange(memories.Select(m => $"Memory: {m.EventSummary} (Turn {m.GameTurnNumber})"));
        }
        
        // Search for world lore
        var loreResults = await _informationManagementService.SearchLoreAsync(sceneElements);
        narrativeContext.AddRange(loreResults.Select(l => $"Lore: {l.Title} - {l.Content}"));
        
        return string.Join("\n", narrativeContext);
    }

    [KernelFunction("update_current_context")]
    [Description("Update the game state's current context field with comprehensive scene information as a string")]
    public async Task<string> UpdateCurrentContext(
        [Description("Detailed scene description including all present entities, environment, and narrative context")] string contextDescription)
    {
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        
        // Update the CurrentContext as a simple string
        gameState.CurrentContext = contextDescription;
        await _gameStateRepo.SaveStateAsync(gameState);
        
        return JsonSerializer.Serialize(new { 
            success = true, 
            message = "Current context updated successfully",
            contextLength = contextDescription.Length
        }, _jsonOptions);
    }

    [KernelFunction("validate_entity_consistency")]
    [Description("Validate consistency between game state and vector database entities")]
    public async Task<string> ValidateEntityConsistency()
    {
        // Implementation for entity validation
        return JsonSerializer.Serialize(new { success = true, message = "Consistency validated" }, _jsonOptions);
    }

    [KernelFunction("compress_chat_history")]
    [Description("Compress chat history while preserving important context")]
    public async Task<string> CompressChatHistory(
        [Description("Chat history summary to preserve")] string historySummary)
    {
        // Implementation for chat compression
        return JsonSerializer.Serialize(new { success = true, message = "History compressed" }, _jsonOptions);
    }
}
```

### Step 2.2: Create UnifiedContext System Prompt

**File:** `Prompts/UnifiedContextSubroutine.md`

```markdown
# Unified Context Management System

You manage world consistency and scene continuity after each player turn. Your role is to maintain the CurrentContext field and ensure all game systems remain synchronized.

## Core Responsibilities

1. **Scene Context Assembly**: Use `gather_scene_context` to collect current environment details
2. **Narrative Context Search**: Use `search_narrative_context` to find relevant memories and lore
3. **Context Field Update**: Use `update_current_context` to save comprehensive scene description
4. **Entity Validation**: Use `validate_entity_consistency` to ensure cross-system consistency
5. **History Management**: Use `compress_chat_history` when chat history becomes too large

## Process Flow

Execute these functions in sequence:
1. Gather current scene context (location, NPCs, Pokemon, environment)
2. Search for relevant narrative context and world knowledge
3. Create comprehensive scene description combining all context
4. Update CurrentContext field with detailed scene information
5. Validate entity consistency across systems
6. Compress chat history if needed (>20 messages or >50k chars)

## Context Description Format

Create a detailed narrative description including:
- Current location with vivid environmental details
- All present NPCs and Pokemon with relevant details
- Time, weather, and regional atmosphere
- Recent significant events that impact the current scene
- Relevant world knowledge, lore, or historical context
- Any ongoing mysteries, relationships, or story threads

Write this as flowing narrative text that provides rich context for storytelling continuity.

## Important Notes

- Focus on creating immersive scene context that supports narrative coherence
- Ensure all plugin-created entities are properly synchronized
- Preserve important story elements and character development
- Do NOT return conversational responses - work through function calls only
- Save all context data via the appropriate functions
```

---

## Phase 3: GameSetup Phase Creation

### Step 3.1: Create GameSetupPhasePlugin

**File:** `Plugins/GameSetupPhasePlugin.cs`

```csharp
namespace PokeLLM.Game.Plugins;

/// <summary>
/// Combined game and character setup phase - handles region selection and mechanical character creation
/// </summary>
public class GameSetupPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IWorldManagementService _worldManagementService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameSetupPhasePlugin(/* constructor parameters */)
    {
        // Initialize services and JSON options
    }

    // === REGION SETUP FUNCTIONS ===
    // Keep existing region functions from GameCreationPhasePlugin

    // === TRAINER CLASS FUNCTIONS ===

    [KernelFunction("search_trainer_classes")]
    [Description("Search for available trainer class data")]
    public async Task<string> SearchTrainerClasses([Description("Class name or type to search for")] string query)
    {
        var results = await _informationManagementService.SearchGameRulesAsync(new List<string> { query, "class", "trainer class" }, "TrainerClass");
        
        return JsonSerializer.Serialize(new 
        { 
            success = true,
            query = query,
            results = results.Select(r => new
            {
                classId = r.EntryId,
                name = r.Title,
                description = r.Content,
                tags = r.Tags
            }).ToList()
        }, _jsonOptions);
    }

    [KernelFunction("create_trainer_class")]
    [Description("Create a new trainer class and store it")]
    public async Task<string> CreateTrainerClass([Description("Complete trainer class data")] TrainerClass classData)
    {
        // Store in vector database using existing TrainerClass structure
        var vectorResult = await _informationManagementService.UpsertGameRuleAsync(
            classData.Id,
            "TrainerClass", 
            classData.Name,
            JsonSerializer.Serialize(classData, _jsonOptions),
            classData.Tags?.ToList()
        );
        
        return JsonSerializer.Serialize(new 
        { 
            success = true,
            classId = classData.Id,
            name = classData.Name,
            result = vectorResult
        }, _jsonOptions);
    }

    [KernelFunction("set_player_trainer_class")]
    [Description("Set the player's trainer class with full class data integration")]
    public async Task<string> SetPlayerTrainerClass([Description("Trainer class ID")] string classId)
    {
        // Get class data from vector database
        var classData = await _informationManagementService.SearchGameRulesAsync(new List<string> { classId }, "TrainerClass");
        var classInfo = classData.FirstOrDefault();
        
        if (classInfo == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Trainer class {classId} not found" }, _jsonOptions);
        }
        
        // Parse class data using existing TrainerClass structure
        var trainerClass = JsonSerializer.Deserialize<TrainerClass>(classInfo.Content);
        
        // Update player with full class integration
        await _characterManagementService.SetPlayerClass(classId);
        
        // Store full TrainerClass data in player
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        gameState.Player.TrainerClassData = trainerClass;
        await _gameStateRepo.SaveStateAsync(gameState);
        
        // Apply class modifiers and benefits as needed
        
        return JsonSerializer.Serialize(new 
        { 
            success = true,
            classId = classId,
            className = trainerClass.Name,
            // Include relevant class benefits
        }, _jsonOptions);
    }

    // === CHARACTER CREATION FUNCTIONS ===
    // Keep existing character functions from CharacterCreationPhasePlugin

    [KernelFunction("finalize_game_setup")]
    [Description("Complete game setup - does NOT transition phases (handled by service)")]
    public async Task<string> FinalizeGameSetup([Description("Summary of setup choices made")] string setupSummary)
    {
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        var player = gameState.Player;
        
        // Validate setup is complete
        if (string.IsNullOrEmpty(gameState.Region) || 
            string.IsNullOrEmpty(player.Name) || 
            string.IsNullOrEmpty(player.CharacterDetails.Class))
        {
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = "Setup incomplete - region, name, and class required"
            }, _jsonOptions);
        }
        
        // Update adventure summary (but DO NOT change phase)
        gameState.AdventureSummary = $"A new Pokemon adventure begins with {player.Name}, a {player.CharacterDetails.Class} trainer in the {gameState.Region} region. {setupSummary}";
        await _gameStateRepo.SaveStateAsync(gameState);
        
        return JsonSerializer.Serialize(new 
        { 
            success = true,
            message = "Game setup completed successfully",
            playerName = player.Name,
            trainerClass = player.CharacterDetails.Class,
            region = gameState.Region,
            setupComplete = true
        }, _jsonOptions);
    }
}
```

### Step 3.2: Create GameSetup Phase Prompt

**File:** `Prompts/GameSetupPhase.md`

```markdown
# Game Setup Phase - Region Selection and Mechanical Character Creation

{{context}}

## Phase Objective
Guide the player through streamlined setup:
1. Region selection 
2. Mechanical character creation (stats and class only - no storytelling)

## Process Flow

### Part 1: Region Setup
1. Search existing regions, present options
2. Handle region selection and store details

### Part 2: Trainer Class Setup  
3. Search available classes, show mechanical benefits
4. Set player class (applies modifiers automatically)

### Part 3: Character Mechanics
5. Generate and set base stats
6. Set player name
7. Show final summary and finalize

## Focus: Mechanical Only
- Emphasize statistical benefits and class modifiers
- Keep descriptions brief and functional  
- No elaborate backstories or personality development
- Show effective stats after class modifiers

## Available Functions
- Region: search_existing_region_knowledge, set_region
- Classes: search_trainer_classes, create_trainer_class, set_player_trainer_class  
- Character: set_player_name, set_player_stats, generate_random_stats, generate_standard_stats
- Completion: finalize_game_setup
```

---

## Phase 4: Standalone Pre-Game Services

### Step 4.1: Create GameSetupService

**File:** `GameLogic/GameSetupService.cs`

```csharp
namespace PokeLLM.Game.GameLogic;

public interface IGameSetupService
{
    IAsyncEnumerable<string> RunGameSetupAsync(string inputMessage, CancellationToken cancellationToken = default);
    Task<bool> IsSetupCompleteAsync();
}

public class GameSetupService : IGameSetupService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnifiedContextService _unifiedContextService;
    private Kernel _gameSetupKernel;
    private ChatHistory _chatHistory;

    public GameSetupService(/* constructor parameters */)
    {
        // Initialize and setup kernel with GameSetupPhasePlugin
    }

    public async IAsyncEnumerable<string> RunGameSetupAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Add user message and stream response from GameSetup plugin
        
        // CRITICAL: Run Unified Context Management after turn
        await _unifiedContextService.RunContextManagementAsync(
            $"GameSetup phase interaction completed. Update CurrentContext with setup progress.",
            cancellationToken);
    }

    public async Task<bool> IsSetupCompleteAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return !string.IsNullOrEmpty(gameState.Region) &&
               !string.IsNullOrEmpty(gameState.Player.Name) &&
               !string.IsNullOrEmpty(gameState.Player.CharacterDetails.Class);
    }
}
```

### Step 4.2: Create WorldGenerationService  

**File:** `GameLogic/WorldGenerationService.cs`

```csharp
namespace PokeLLM.Game.GameLogic;

public interface IWorldGenerationService
{
    IAsyncEnumerable<string> RunWorldGenerationAsync(string inputMessage, CancellationToken cancellationToken = default);
    Task<bool> IsWorldGenerationCompleteAsync();
}

public class WorldGenerationService : IWorldGenerationService
{
    // Similar structure to GameSetupService but using WorldGenerationPhasePlugin
    // CRITICAL: Run Unified Context Management after each turn
}
```

### Step 4.3: Create UnifiedContextService

**File:** `GameLogic/UnifiedContextService.cs`

```csharp
namespace PokeLLM.Game.GameLogic;

public interface IUnifiedContextService
{
    Task<string> RunContextManagementAsync(string directive, CancellationToken cancellationToken = default);
}

public class UnifiedContextService : IUnifiedContextService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IServiceProvider _serviceProvider;
    private Kernel _contextKernel;

    public UnifiedContextService(/* constructor parameters */)
    {
        // Initialize kernel with UnifiedContextPlugin
    }

    public async Task<string> RunContextManagementAsync(string directive, CancellationToken cancellationToken = default)
    {
        var contextHistory = new ChatHistory();
        var systemPrompt = await LoadSystemPromptAsync("UnifiedContext");
        contextHistory.AddSystemMessage(systemPrompt);
        contextHistory.AddUserMessage(directive);

        var result = await chatService.GetChatMessageContentAsync(
            contextHistory, executionSettings, _contextKernel, cancellationToken);

        return result.ToString();
    }
}
```

---

## Phase 5: OrchestrationService Updates

### Step 5.1: Simplified OrchestrationService for Gameplay Only

**File:** `Orchestration/OrchestrationService.cs`

#### Key Changes:

**1. Remove Pre-Game Phase Support**
```csharp
private void SetupPromptsAndPlugins()
{
    // REMOVE: GameSetup and WorldGeneration phases
    // KEEP: Only gameplay phases
    SetupGamePhaseKernel<ExplorationPhasePlugin>("Exploration");
    SetupGamePhaseKernel<CombatPhasePlugin>("Combat");
    SetupGamePhaseKernel<LevelUpPhasePlugin>("LevelUp");
}
```

**2. Update GetPhaseKernelName Method**
```csharp
private string GetPhaseKernelName(GamePhase phase)
{
    return phase switch
    {
        GamePhase.Exploration => "Exploration",
        GamePhase.Combat => "Combat", 
        GamePhase.LevelUp => "LevelUp",
        _ => "Exploration"
    };
}
```

**3. Inject Context into System Prompts**
```csharp
// UPDATE: LoadSystemPromptAsync to inject context
public async Task<string> LoadSystemPromptAsync(string phaseToLoad)
{
    var promptPath = GetPromptPath(phaseToLoad);
    var systemPrompt = await File.ReadAllTextAsync(promptPath);
    
    // INJECT: CurrentContext into prompt using {{context}} variable
    var gameState = await _gameStateRepository.LoadLatestStateAsync();
    var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ? 
        gameState.CurrentContext : "No context available.";
    
    // Replace {{context}} placeholder with actual context
    systemPrompt = systemPrompt.Replace("{{context}}", currentContext);
    
    return systemPrompt;
}
```

**4. Replace Multi-Context System with Unified Post-Turn Processing**
```csharp
public async IAsyncEnumerable<string> OrchestrateAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // REMOVE: Pre-turn context gathering
    // KEEP: Standard setup and streaming
    
    // Stream the response
    await foreach (var chunk in ExecutePromptStreamingAsync(history, kernel, cancellationToken))
    {
        yield return chunk;
    }
    
    // NEW: Post-processing with Unified Context Management
    var contextDirective = $@"Post-turn context update for {_currentPhase} phase.

Turn: {updatedGameState.GameTurnNumber}
Input: {inputMessage}
Response: {fullResponse}

Execute all context management functions to update CurrentContext field and maintain consistency.";

    await _unifiedContextService.RunContextManagementAsync(contextDirective, cancellationToken);
    
    // Handle phase transitions
}
```

**5. Update Constructor**
```csharp
public OrchestrationService(
    ILLMProvider llmProvider,
    IGameStateRepository gameStateRepository,
    IServiceProvider serviceProvider,
    IUnifiedContextService unifiedContextService) // NEW
{
    _unifiedContextService = unifiedContextService; // NEW
    // Initialize other components
}
```

**6. Remove Old Context Management Methods**
- Remove RunContextGathering, RunContextManagement, RunChatHistoryManagement methods
- Remove all tool sequence validation methods
- All context management now handled by UnifiedContextService

---

## Phase 6: Main Application Controller

### Step 6.1: Create Game Controller

**File:** `GameLogic/GameController.cs`

```csharp
namespace PokeLLM.Game.GameLogic;

public interface IGameController
{
    IAsyncEnumerable<string> ProcessInputAsync(string input, CancellationToken cancellationToken = default);
    Task<GameStatus> GetGameStatusAsync();
}

public class GameController : IGameController
{
    private readonly IGameSetupService _gameSetupService;
    private readonly IWorldGenerationService _worldGenerationService;
    private readonly IOrchestrationService _orchestrationService;
    private readonly IGameStateRepository _gameStateRepository;

    public GameController(/* constructor parameters */) { }

    public async IAsyncEnumerable<string> ProcessInputAsync(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var status = await GetGameStatusAsync();
        
        switch (status)
        {
            case GameStatus.SetupNeeded:
                await foreach (var chunk in _gameSetupService.RunGameSetupAsync(input, cancellationToken))
                    yield return chunk;
                
                // Auto-transition to world generation if setup complete
                if (await _gameSetupService.IsSetupCompleteAsync())
                {
                    yield return "\n\n--- Setup Complete! Starting World Generation ---\n\n";
                    await foreach (var chunk in _worldGenerationService.RunWorldGenerationAsync(
                        "Begin world generation based on setup.", cancellationToken))
                        yield return chunk;
                }
                break;
                
            case GameStatus.WorldGenerationNeeded:
                await foreach (var chunk in _worldGenerationService.RunWorldGenerationAsync(input, cancellationToken))
                    yield return chunk;
                
                // Auto-transition to gameplay if world generation complete
                if (await _worldGenerationService.IsWorldGenerationCompleteAsync())
                {
                    var gameState = await _gameStateRepository.LoadLatestStateAsync();
                    gameState.CurrentPhase = GamePhase.Exploration;
                    await _gameStateRepository.SaveStateAsync(gameState);
                    
                    yield return "\n\n--- World Complete! Adventure Begins ---\n\n";
                    await foreach (var chunk in _orchestrationService.OrchestrateAsync(
                        "Describe the opening scene.", cancellationToken))
                        yield return chunk;
                }
                break;
                
            case GameStatus.GameplayActive:
                await foreach (var chunk in _orchestrationService.OrchestrateAsync(input, cancellationToken))
                    yield return chunk;
                break;
        }
    }

    public async Task<GameStatus> GetGameStatusAsync()
    {
        if (!await _gameSetupService.IsSetupCompleteAsync())
            return GameStatus.SetupNeeded;
        
        if (!await _worldGenerationService.IsWorldGenerationCompleteAsync())
            return GameStatus.WorldGenerationNeeded;
        
        return GameStatus.GameplayActive;
    }
}

public enum GameStatus
{
    SetupNeeded,
    WorldGenerationNeeded, 
    GameplayActive
}
```

---

## Phase 7: Service Registration and Updates

### Step 7.1: Update ServiceConfiguration

**File:** `Configuration/ServiceConfiguration.cs`

```csharp
public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // ADD: New services
    services.AddScoped<IUnifiedContextService, UnifiedContextService>();
    services.AddScoped<IGameSetupService, GameSetupService>();
    services.AddScoped<IWorldGenerationService, WorldGenerationService>();
    services.AddScoped<IGameController, GameController>();
    
    // KEEP: Existing services (OrchestrationService now only handles gameplay)
    services.AddScoped<IOrchestrationService, OrchestrationService>();
    
    // KEEP: All other existing service registrations
}
```

### Step 7.2: Update CharacterManagementService

**File:** `GameLogic/CharacterManagementService.cs`

```csharp
// ADD: Methods to support TrainerClass integration
public async Task<TrainerClass> GetTrainerClassData(string classId)
{
    // Retrieve class data from information service and parse
}

public async Task ApplyTrainerClassModifiers(string playerId, TrainerClass trainerClass)
{
    // Apply stat modifiers, abilities, and starting bonuses
}

public async Task<Stats> CalculateEffectiveStats(string playerId)
{
    // Calculate stats with class modifiers applied using player.TrainerClassData
}
```

---

## Phase 8: Prompt Updates

### Step 8.1: Update Main Phase Prompts

**Files:** `Prompts/ExplorationPhase.md`, `Prompts/CombatPhase.md`, `Prompts/LevelUpPhase.md`

**ADD to beginning of each file:**
```markdown
# Phase Name

## Current Context
{{context}}

## Your Role as Game Master
[Rest of existing prompt content...]
```

### Step 8.2: Update WorldGeneration Plugin

**File:** `Plugins/WorldGenerationPhasePlugin.cs`

**MODIFY:** Remove phase transition from finalize_world_generation function
```csharp
[KernelFunction("finalize_world_generation")]
[Description("Complete world generation - does NOT transition phases (handled by service)")]
public async Task<string> FinalizeWorldGeneration(/* parameters */)
{
    // Update world state but DO NOT change phase
    // Store data but let service handle phase transitions
}
```

---

## Phase 9: Database Migration

### Step 9.1: Game State Migration

**File:** `GameState/GameStateRepository.cs`

```csharp
public async Task MigrateToNewSystem()
{
    // Convert existing games to new phase system
    // Add CurrentContext field to existing game states
    // Ensure TrainerClass data is properly initialized
    // Set default phase to Exploration for existing games
}
```

---

## Implementation Summary

This restructure achieves:

1. **Clean Separation**: Pre-game setup isolated from main orchestration
2. **Unified Context**: Single system maintains CurrentContext string field  
3. **Context Injection**: {{context}} variable injected into all phase system prompts
4. **TrainerClass Integration**: Existing classes utilized with stat modifier calculations
5. **Simplified Flow**: GameController manages transitions, orchestration only handles gameplay
6. **Consistent State Management**: UnifiedContextService runs after every turn across all services

The result is a cleaner, more maintainable architecture with consistent context management throughout the entire application lifecycle.