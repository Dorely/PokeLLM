using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.Game.VectorStore.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for procedural world generation and content creation
/// </summary>
public class WorldGenerationPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly IPokemonManagementService _pokemonManagementService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public WorldGenerationPhasePlugin(
        IGameStateRepository gameStateRepo,
        IWorldManagementService worldManagementService,
        INpcManagementService npcManagementService,
        IPokemonManagementService pokemonManagementService,
        IInformationManagementService informationManagementService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _worldManagementService = worldManagementService;
        _npcManagementService = npcManagementService;
        _pokemonManagementService = pokemonManagementService;
        _informationManagementService = informationManagementService;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Vector Lookup Functions

    [KernelFunction("search_existing_content")]
    [Description("Search vector store for existing world content before generation")]
    public async Task<string> SearchExistingContent(
        [Description("Search queries to find existing content")] List<string> queries,
        [Description("Content type: 'entities', 'locations', 'lore', 'rules', 'narrative'")] string contentType = "all")
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] SearchExistingContent called: {string.Join(", ", queries)}");
        
        try
        {
            var results = new List<object>();
            
            foreach (var query in queries)
            {
                switch (contentType.ToLower())
                {
                    case "entities":
                        var entities = await _informationManagementService.SearchEntitiesAsync(new List<string> { query });
                        results.AddRange(entities.Select(e => new { type = "entity", data = e }));
                        break;
                        
                    case "locations":
                        var location = await _informationManagementService.GetLocationAsync(query);
                        if (location != null)
                            results.Add(new { type = "location", data = location });
                        break;
                        
                    case "lore":
                        var loreResults = await _informationManagementService.SearchLoreAsync(new List<string> { query });
                        results.AddRange(loreResults.Select(l => new { type = "lore", data = l }));
                        break;
                        
                    case "rules":
                        var ruleResults = await _informationManagementService.SearchGameRulesAsync(new List<string> { query });
                        results.AddRange(ruleResults.Select(r => new { type = "rule", data = r }));
                        break;
                        
                    case "all":
                    default:
                        // Search all types
                        var allEntities = await _informationManagementService.SearchEntitiesAsync(new List<string> { query });
                        results.AddRange(allEntities.Select(e => new { type = "entity", data = e }));
                        
                        var allLore = await _informationManagementService.SearchLoreAsync(new List<string> { query });
                        results.AddRange(allLore.Select(l => new { type = "lore", data = l }));
                        
                        var allRules = await _informationManagementService.SearchGameRulesAsync(new List<string> { query });
                        results.AddRange(allRules.Select(r => new { type = "rule", data = r }));
                        break;
                }
            }
            
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Found {results.Count} total results");
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                queries = queries,
                contentType = contentType,
                resultsCount = results.Count,
                results = results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in SearchExistingContent: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Vector Upsert Functions

    [KernelFunction("upsert_entity")]
    [Description("Store entity data (NPCs, Pokemon species, items) in the vector database")]
    public async Task<string> UpsertEntity(
        [Description("Complete entity record to store")] EntityVectorRecord entityData)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertEntity called: {entityData.EntityId}");
        
        try
        {
            var result = await _informationManagementService.UpsertEntityAsync(
                entityData.EntityId,
                entityData.EntityType,
                entityData.Name,
                entityData.Description,
                entityData.PropertiesJson,
                entityData.Id == Guid.Empty ? null : entityData.Id
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                entityId = entityData.EntityId,
                entityType = entityData.EntityType,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in UpsertEntity: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("upsert_location")]
    [Description("Store location data in the vector database")]
    public async Task<string> UpsertLocation(
        [Description("Complete location record to store")] LocationVectorRecord locationData)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertLocation called: {locationData.LocationId}");
        
        try
        {
            var result = await _informationManagementService.UpsertLocationAsync(
                locationData.LocationId,
                locationData.Name,
                locationData.Description,
                locationData.Region,
                locationData.Tags?.ToList(),
                locationData.Id == Guid.Empty ? null : locationData.Id
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                locationId = locationData.LocationId,
                name = locationData.Name,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in UpsertLocation: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("upsert_lore")]
    [Description("Store lore, quest, and story data in the vector database")]
    public async Task<string> UpsertLore(
        [Description("Complete lore record to store")] LoreVectorRecord loreData)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertLore called: {loreData.EntryId}");
        
        try
        {
            var result = await _informationManagementService.UpsertLoreAsync(
                loreData.EntryId,
                loreData.EntryType,
                loreData.Title,
                loreData.Content,
                loreData.Tags?.ToList(),
                loreData.Id == Guid.Empty ? null : loreData.Id
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                entryId = loreData.EntryId,
                entryType = loreData.EntryType,
                title = loreData.Title,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in UpsertLore: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("upsert_game_rule")]
    [Description("Store game rules, mechanics, and class data in the vector database")]
    public async Task<string> UpsertGameRule(
        [Description("Complete game rule record to store")] GameRuleVectorRecord ruleData)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertGameRule called: {ruleData.EntryId}");
        
        try
        {
            var result = await _informationManagementService.UpsertGameRuleAsync(
                ruleData.EntryId,
                ruleData.EntryType,
                ruleData.Title,
                ruleData.Content,
                ruleData.Tags?.ToList(),
                ruleData.Id == Guid.Empty ? null : ruleData.Id
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                entryId = ruleData.EntryId,
                entryType = ruleData.EntryType,
                title = ruleData.Title,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in UpsertGameRule: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Game State Management Functions

    [KernelFunction("create_npc")]
    [Description("Create an NPC in the game state with complete NPC data")]
    public async Task<string> CreateNpc(
        [Description("Complete NPC object to create")] Npc npcData,
        [Description("Optional location ID to place the NPC")] string locationId = "")
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] CreateNpc called: {npcData.Id}");
        
        try
        {
            var result = await _npcManagementService.CreateNpcAsync(npcData, locationId);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                npcId = npcData.Id,
                npcName = npcData.Name,
                locationId = locationId,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in CreateNpc: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("create_pokemon")]
    [Description("Create a Pokemon instance in the game state")]
    public async Task<string> CreatePokemon(
        [Description("Pokemon data to create")] Pokemon pokemonData,
        [Description("Optional location ID to place the Pokemon")] string locationId = "")
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] CreatePokemon called: {pokemonData.Id}");
        
        try
        {
            var result = await _pokemonManagementService.CreatePokemonAsync(pokemonData, locationId);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                pokemonId = pokemonData.Id,
                species = pokemonData.Species,
                locationId = locationId,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in CreatePokemon: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("create_location")]
    [Description("Create a location in the game state")]
    public async Task<string> CreateLocation(
        [Description("Location data to create")] Location locationData)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] CreateLocation called: {locationData.Id}");
        
        try
        {
            var result = await _worldManagementService.CreateLocationAsync(locationData);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                locationId = locationData.Id,
                name = locationData.Name,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in CreateLocation: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("assign_npc_pokemon")]
    [Description("Assign Pokemon to an NPC's team")]
    public async Task<string> AssignNpcPokemon(
        [Description("NPC ID to assign Pokemon to")] string npcId,
        [Description("Pokemon instance ID to assign")] string pokemonId)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] AssignNpcPokemon called: {npcId} <- {pokemonId}");
        
        try
        {
            var result = await _npcManagementService.AssignPokemonToNpcAsync(npcId, pokemonId);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                npcId = npcId,
                pokemonId = pokemonId,
                result = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in AssignNpcPokemon: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Procedural Generation Functions

    [KernelFunction("generate_procedural_content")]
    [Description("Use dice rolls and random generation for procedural world building")]
    public async Task<string> GenerateProceduralContent(
        [Description("Generation type: 'random_number', 'random_choice', 'dice_roll', 'random_stats'")] string generationType,
        [Description("Number of sides for dice (if applicable)")] int sides = 20,
        [Description("Number of dice to roll")] int count = 1,
        [Description("List of choices for random selection")] List<string> choices = null,
        [Description("Minimum value (for ranges)")] int minValue = 1,
        [Description("Maximum value (for ranges)")] int maxValue = 100)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] GenerateProceduralContent called: {generationType}");
        
        try
        {
            switch (generationType.ToLower())
            {
                case "dice_roll":
                    var diceResult = await _gameLogicService.RollDiceAsync(sides, count);
                    return JsonSerializer.Serialize(diceResult, _jsonOptions);
                    
                case "random_choice":
                    if (choices == null || choices.Count == 0)
                    {
                        return JsonSerializer.Serialize(new { error = "Choices list is required for random selection" }, _jsonOptions);
                    }
                    
                    var choiceResult = await _gameLogicService.MakeRandomDecisionFromOptionsAsync(choices);
                    return JsonSerializer.Serialize(choiceResult, _jsonOptions);
                    
                case "random_number":
                    var randomRoll = await _gameLogicService.RollDiceAsync(maxValue - minValue + 1, 1);
                    var randomValue = randomRoll.Total + minValue - 1;
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        value = randomValue,
                        min = minValue,
                        max = maxValue,
                        generationType = generationType
                    }, _jsonOptions);
                    
                case "random_stats":
                    // Generate random D&D stats (3d6 for each)
                    var stats = new Dictionary<string, int>();
                    var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
                    
                    foreach (var statName in statNames)
                    {
                        var statRoll = await _gameLogicService.RollDiceAsync(6, 3);
                        stats[statName] = statRoll.Total;
                    }
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        stats = stats,
                        generationType = generationType
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown generation type: {generationType}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in GenerateProceduralContent: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Phase Management

    [KernelFunction("finalize_world_generation")]
    [Description("Complete world generation and transition to character creation with opening scenario")]
    public async Task<string> FinalizeWorldGeneration(
        [Description("Opening scenario context that will start the adventure")] string openingScenario,
        [Description("Summary of the generated world content")] string worldSummary)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] FinalizeWorldGeneration called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Verify we have a region selected
            if (string.IsNullOrEmpty(gameState.Region))
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false,
                    error = "Cannot finalize world generation - no region has been set",
                    requiresRegionSelection = true
                }, _jsonOptions);
            }
            
            // Set the phase to CharacterCreation
            gameState.CurrentPhase = GamePhase.CharacterCreation;
            
            // Set the phase change summary with opening scenario
            var fullContext = $"World generation completed for {gameState.Region}. {worldSummary} Opening Scenario: {openingScenario}";
            gameState.PhaseChangeSummary = fullContext;
            
            // Update adventure summary with world details
            gameState.AdventureSummary = $"The {gameState.Region} region has been prepared for adventure. {worldSummary}";
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"World Generation Completed: {worldSummary}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            // Log the completion and opening scenario
            await _informationManagementService.LogNarrativeEventAsync(
                "world_generation_completed",
                $"World generation completed for {gameState.Region}",
                fullContext,
                new List<string>(),
                "",
                null,
                gameState.GameTurnNumber
            );
            
            // Store the opening scenario for immediate use
            await _informationManagementService.UpsertLoreAsync(
                $"opening_scenario_{gameState.SessionId}",
                "opening_scenario",
                $"Opening Scenario for {gameState.Region}",
                openingScenario,
                new List<string> { "opening_scenario", "character_creation", gameState.Region.ToLower() }
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "World generation completed successfully",
                region = gameState.Region,
                nextPhase = "CharacterCreation",
                openingScenario = openingScenario,
                worldSummary = worldSummary,
                sessionId = gameState.SessionId,
                phaseTransitionCompleted = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in FinalizeWorldGeneration: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #endregion
}