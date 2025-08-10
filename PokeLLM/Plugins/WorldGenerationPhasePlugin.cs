using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Plugins.Models;
using PokeLLM.Game.VectorStore.Models;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        [Description("Search queries to find existing content")] SearchQueriesDto queries,
        [Description("Content type: 'entities', 'locations', 'lore', 'rules', 'narrative'")] string contentType = "all")
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] SearchExistingContent called: {string.Join(", ", queries.Queries)}");
        
        try
        {
            // Validate required parameters
            if (queries == null || queries.Queries == null || queries.Queries.Count == 0 || queries.Queries.All(string.IsNullOrWhiteSpace))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "At least one non-empty search query is required" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Content type cannot be null or empty" 
                }, _jsonOptions);
            }

            var validContentTypes = new[] { "entities", "locations", "lore", "rules", "narrative", "all" };
            if (!validContentTypes.Contains(contentType.ToLower()))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Invalid content type '{contentType}'. Valid types are: {string.Join(", ", validContentTypes)}" 
                }, _jsonOptions);
            }

            var results = new List<object>();
            
            foreach (var query in queries.Queries.Where(q => !string.IsNullOrWhiteSpace(q)))
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
                queries = queries.Queries,
                contentType = contentType,
                resultsCount = results.Count,
                results = results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in SearchExistingContent: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    #endregion

    #region Vector Upsert Functions

    [KernelFunction("upsert_entity")]
    [Description("Store entity data (NPCs, Pokemon species, items) in the vector database")]
    public async Task<string> UpsertEntity(
        [Description("Complete entity record to store")] EntityVectorRecordDto entityDataDto)
    {
        var entityData = entityDataDto.ToVectorRecord();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertEntity called: {entityData?.EntityId}");
        
        try
        {
            // Validate required parameters
            if (entityData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entity data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(entityData.EntityId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entity ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(entityData.EntityType))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entity type is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(entityData.Name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entity name is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(entityData.Description))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entity description is required and cannot be null or empty" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("upsert_location")]
    [Description("Store location data in the vector database")]
    public async Task<string> UpsertLocation(
        [Description("Complete location record to store")] LocationVectorRecordDto locationDataDto)
    {
        var locationData = locationDataDto.ToVectorRecord();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertLocation called: {locationData?.LocationId}");
        
        try
        {
            // Validate required parameters
            if (locationData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(locationData.LocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(locationData.Name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location name is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(locationData.Description))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location description is required and cannot be null or empty" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("upsert_lore")]
    [Description("Store lore, quest, and story data in the vector database")]
    public async Task<string> UpsertLore(
        [Description("Complete lore record to store")] LoreVectorRecordDto loreDataDto)
    {
        var loreData = loreDataDto.ToVectorRecord();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertLore called: {loreData?.EntryId}");
        
        try
        {
            // Validate required parameters
            if (loreData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Lore data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(loreData.EntryId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entry ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(loreData.EntryType))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entry type is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(loreData.Title))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Lore title is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(loreData.Content))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Lore content is required and cannot be null or empty" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("upsert_game_rule")]
    [Description("Store game rules, mechanics, and class data in the vector database")]
    public async Task<string> UpsertGameRule(
        [Description("Complete game rule record to store")] GameRuleVectorRecordDto ruleDataDto)
    {
        var ruleData = ruleDataDto.ToVectorRecord();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] UpsertGameRule called: {ruleData?.EntryId}");
        
        try
        {
            // Validate required parameters
            if (ruleData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Game rule data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(ruleData.EntryId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entry ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(ruleData.EntryType))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entry type is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(ruleData.Title))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Rule title is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(ruleData.Content))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Rule content is required and cannot be null or empty" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    #endregion

    #region Game State Management Functions

    [KernelFunction("create_npc")]
    [Description("Create an NPC in the game state with complete NPC data")]
    public async Task<string> CreateNpc(
        [Description("Complete NPC object to create")] NpcDto npcDataDto,
        [Description("Optional location ID to place the NPC")] string locationId = "")
    {
        var npcData = npcDataDto.ToGameStateModel();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] CreateNpc called: {npcData?.Id}");
        
        try
        {
            // Validate required parameters
            if (npcData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(npcData.Id))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(npcData.Name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC name is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (npcData.Stats == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC stats are required and cannot be null" 
                }, _jsonOptions);
            }

            if (npcData.CharacterDetails == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC character details are required and cannot be null" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("create_pokemon")]
    [Description("Create a Pokemon instance in the game state")]
    public async Task<string> CreatePokemon(
        [Description("Pokemon data to create")] PokemonDto pokemonDataDto,
        [Description("Optional location ID to place the Pokemon")] string locationId = "")
    {
        var pokemonData = pokemonDataDto.ToGameStateModel();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] CreatePokemon called: {pokemonData?.Id}");
        
        try
        {
            // Validate required parameters
            if (pokemonData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Pokemon data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(pokemonData.Id))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Pokemon ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(pokemonData.Species))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Pokemon species is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (pokemonData.Level <= 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Pokemon level must be greater than 0" 
                }, _jsonOptions);
            }

            if (pokemonData.Stats == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Pokemon stats are required and cannot be null" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("create_location")]
    [Description("Create a location in the game state")]
    public async Task<string> CreateLocation(
        [Description("Location data to create")] LocationDto locationDataDto)
    {
        var locationData = locationDataDto.ToGameStateModel();
        Debug.WriteLine($"[WorldGenerationPhasePlugin] CreateLocation called: {locationData?.Id}");
        
        try
        {
            // Validate required parameters
            if (locationData == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location data is required and cannot be null" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(locationData.Id))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(locationData.Name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location name is required and cannot be null or empty" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
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
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(pokemonId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Pokemon ID is required and cannot be null or empty" 
                }, _jsonOptions);
            }

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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
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
        [Description("List of choices for random selection")] ChoicesDto choices = null,
        [Description("Minimum value (for ranges)")] int minValue = 1,
        [Description("Maximum value (for ranges)")] int maxValue = 100)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] GenerateProceduralContent called: {generationType}");
        
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(generationType))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Generation type is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            var validTypes = new[] { "random_number", "random_choice", "dice_roll", "random_stats" };
            if (!validTypes.Contains(generationType.ToLower()))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Invalid generation type '{generationType}'. Valid types are: {string.Join(", ", validTypes)}" 
                }, _jsonOptions);
            }

            switch (generationType.ToLower())
            {
                case "dice_roll":
                    if (sides <= 0)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Number of sides must be greater than 0" 
                        }, _jsonOptions);
                    }

                    if (count <= 0)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Number of dice to roll must be greater than 0" 
                        }, _jsonOptions);
                    }

                    var diceResult = await _gameLogicService.RollDiceAsync(sides, count);
                    return JsonSerializer.Serialize(diceResult, _jsonOptions);
                    
                case "random_choice":
                    if (choices == null || choices.Choices == null || choices.Choices.Count == 0 || choices.Choices.All(string.IsNullOrWhiteSpace))
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Choices list is required for random selection and must contain at least one non-empty choice" 
                        }, _jsonOptions);
                    }
                    
                    var validChoices = choices.Choices.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                    var choiceResult = await _gameLogicService.MakeRandomDecisionFromOptionsAsync(validChoices);
                    return JsonSerializer.Serialize(choiceResult, _jsonOptions);
                    
                case "random_number":
                    if (minValue >= maxValue)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Maximum value must be greater than minimum value" 
                        }, _jsonOptions);
                    }

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
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"Unknown generation type: {generationType}" 
                    }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in GenerateProceduralContent: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    #endregion

    #region Phase Management

    [KernelFunction("finalize_world_generation")]
    [Description("Complete world generation")]
    public async Task<string> FinalizeWorldGeneration(
        [Description("Opening scenario context that will start the adventure")] string openingScenario)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] FinalizeWorldGeneration called");
        
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(openingScenario))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Opening scenario is required and cannot be null or empty" 
                }, _jsonOptions);
            }


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
            
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"World Generation Completed: {openingScenario}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            
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
                sessionId = gameState.SessionId,
                phaseTransitionCompleted = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in FinalizeWorldGeneration: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    #endregion
}