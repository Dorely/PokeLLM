using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.Game.VectorStore.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing context consistency across vector database, game state, and chat histories
/// </summary>
public class ContextManagementPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly IPokemonManagementService _pokemonManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ContextManagementPlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService,
        IWorldManagementService worldManagementService,
        INpcManagementService npcManagementService,
        IPokemonManagementService pokemonManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _worldManagementService = worldManagementService;
        _npcManagementService = npcManagementService;
        _pokemonManagementService = pokemonManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("search_and_verify_entities")]
    [Description("Search for entities in both vector database and game state to verify consistency")]
    public async Task<string> SearchAndVerifyEntities(
        [Description("Entity name or description to search for")] string query,
        [Description("Type of entity: 'npc', 'pokemon', 'object', or 'all'")] string entityType = "all")
    {
        Debug.WriteLine($"[ContextManagementPlugin] SearchAndVerifyEntities called: {query}, type: {entityType}");
        
        try
        {
            // Search vector database
            var vectorResults = await _informationManagementService.SearchEntitiesAsync(new List<string> { query }, entityType);
            
            // Get current game state for comparison
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            var response = new
            {
                query = query,
                entityType = entityType,
                vectorResults = vectorResults.Select(r => new
                {
                    entityId = r.EntityId,
                    entityType = r.EntityType,
                    name = r.Name,
                    description = r.Description,
                    existsInGameState = entityType == "npc" ? gameState.WorldNpcs.ContainsKey(r.EntityId) : 
                                      entityType == "pokemon" ? gameState.WorldPokemon.ContainsKey(r.EntityId) : false
                }).ToList(),
                consistencyIssues = new List<string>()
            };
            
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextManagementPlugin] Error in SearchAndVerifyEntities: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("create_npc")]
    [Description("Create an NPC in the game state with complete NPC data")]
    public async Task<string> CreateNpc(
        [Description("Complete NPC object to create")] Npc npcData,
        [Description("Optional location ID to place the NPC")] string locationId = "")
    {
        Debug.WriteLine($"[ContextManagementPlugin] CreateNpc called: {npcData.Id}");
        
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
            Debug.WriteLine($"[ContextManagementPlugin] Error in CreateNpc: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("create_pokemon")]
    [Description("Create a Pokemon instance in the game state")]
    public async Task<string> CreatePokemon(
        [Description("Pokemon data to create")] Pokemon pokemonData,
        [Description("Optional location ID to place the Pokemon")] string locationId = "")
    {
        Debug.WriteLine($"[ContextManagementPlugin] CreatePokemon called: {pokemonData.Id}");
        
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
            Debug.WriteLine($"[ContextManagementPlugin] Error in CreatePokemon: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("create_location")]
    [Description("Create a location in the game state")]
    public async Task<string> CreateLocation(
        [Description("Location data to create")] Location locationData)
    {
        Debug.WriteLine($"[ContextManagementPlugin] CreateLocation called: {locationData.Id}");
        
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
            Debug.WriteLine($"[ContextManagementPlugin] Error in CreateLocation: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("manage_location_entities")]
    [Description("Add, remove, or verify entities at specific locations")]
    public async Task<string> ManageLocationEntities(
        [Description("Action: 'add', 'remove', 'list', or 'verify'")] string action,
        [Description("Location ID")] string locationId,
        [Description("Entity type: 'npc' or 'pokemon'")] string entityType,
        [Description("Entity ID (for add/remove actions)")] string entityId = "")
    {
        Debug.WriteLine($"[ContextManagementPlugin] ManageLocationEntities called: {action} {entityType} at {locationId}");
        
        try
        {
            switch (action.ToLower())
            {
                case "add":
                    if (entityType.ToLower() == "npc")
                    {
                        await _worldManagementService.AddNpcToLocationAsync(locationId, entityId);
                    }
                    else if (entityType.ToLower() == "pokemon")
                    {
                        await _worldManagementService.AddPokemonToLocationAsync(locationId, entityId);
                    }
                    
                    var addResult = new { success = true, message = $"{entityType} {entityId} added to location {locationId}", action = action };
                    return JsonSerializer.Serialize(addResult, _jsonOptions);
                    
                case "remove":
                    if (entityType.ToLower() == "npc")
                    {
                        await _worldManagementService.RemoveNpcFromLocationAsync(locationId, entityId);
                    }
                    else if (entityType.ToLower() == "pokemon")
                    {
                        await _worldManagementService.RemovePokemonFromLocationAsync(locationId, entityId);
                    }
                    
                    var removeResult = new { success = true, message = $"{entityType} {entityId} removed from location {locationId}", action = action };
                    return JsonSerializer.Serialize(removeResult, _jsonOptions);
                    
                case "list":
                    var gameState = await _gameStateRepo.LoadLatestStateAsync();
                    var location = gameState.WorldLocations.ContainsKey(locationId) ? gameState.WorldLocations[locationId] : null;
                    
                    var listResult = new
                    {
                        success = true,
                        locationId = locationId,
                        npcs = location?.PresentNpcIds ?? new List<string>(),
                        pokemon = location?.PresentPokemonIds ?? new List<string>(),
                        action = action
                    };
                    return JsonSerializer.Serialize(listResult, _jsonOptions);
                    
                case "verify":
                    // Get entities at location and cross-reference with vector database
                    var gameStateVerify = await _gameStateRepo.LoadLatestStateAsync();
                    var locationVerify = gameStateVerify.WorldLocations.ContainsKey(locationId) ? gameStateVerify.WorldLocations[locationId] : null;
                    
                    var verifyResult = new
                    {
                        success = true,
                        locationId = locationId,
                        npcsInGameState = locationVerify?.PresentNpcIds ?? new List<string>(),
                        pokemonInGameState = locationVerify?.PresentPokemonIds ?? new List<string>(),
                        action = action
                    };
                    return JsonSerializer.Serialize(verifyResult, _jsonOptions);
                    
                default:
                    var errorResult = new { success = false, message = $"Unknown action: {action}", action = action };
                    return JsonSerializer.Serialize(errorResult, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextManagementPlugin] Error in ManageLocationEntities: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("search_vector_database")]
    [Description("Search vector database for entities, locations, lore, or narrative history")]
    public async Task<string> SearchVectorDatabase(
        [Description("Type of search: 'entities', 'locations', 'lore', 'rules', or 'narrative'")] string searchType,
        [Description("Search query")] string query,
        [Description("Optional filter (entity type, entry type, etc.)")] string filter = "",
        [Description("Minimum relevance score (0.0 to 1.0)")] double minRelevanceScore = 0.75)
    {
        Debug.WriteLine($"[ContextManagementPlugin] SearchVectorDatabase called: {searchType} - {query}");
        
        try
        {
            object results = null;
            
            switch (searchType.ToLower())
            {
                case "entities":
                    var entityResults = await _informationManagementService.SearchEntitiesAsync(new List<string> { query }, filter);
                    results = entityResults.Select(r => new
                    {
                        entityId = r.EntityId,
                        entityType = r.EntityType,
                        name = r.Name,
                        description = r.Description
                    });
                    break;
                    
                case "locations":
                    var locationResult = await _informationManagementService.GetLocationAsync(query);
                    results = locationResult != null ? new[]
                    {
                        new
                        {
                            locationId = locationResult.LocationId,
                            name = locationResult.Name,
                            description = locationResult.Description,
                            region = locationResult.Region,
                            tags = locationResult.Tags
                        }
                    } : new object[0];
                    break;
                    
                case "lore":
                    var loreResults = await _informationManagementService.SearchLoreAsync(new List<string> { query }, filter);
                    results = loreResults.Select(r => new
                    {
                        entryId = r.EntryId,
                        entryType = r.EntryType,
                        title = r.Title,
                        content = r.Content,
                        tags = r.Tags
                    });
                    break;
                    
                case "rules":
                    var ruleResults = await _informationManagementService.SearchGameRulesAsync(new List<string> { query }, filter);
                    results = ruleResults.Select(r => new
                    {
                        entryId = r.EntryId,
                        entryType = r.EntryType,
                        title = r.Title,
                        content = r.Content,
                        tags = r.Tags
                    });
                    break;
                    
                case "narrative":
                    var gameState = await _gameStateRepo.LoadLatestStateAsync();
                    var narrativeResults = await _informationManagementService.FindMemoriesAsync(gameState.SessionId, query, null, minRelevanceScore);
                    results = narrativeResults.Select(r => new
                    {
                        gameTurnNumber = r.GameTurnNumber,
                        eventType = r.EventType,
                        eventSummary = r.EventSummary,
                        involvedEntities = r.InvolvedEntities,
                        locationId = r.LocationId
                    });
                    break;
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown search type: {searchType}" }, _jsonOptions);
            }
            
            var response = new
            {
                searchType = searchType,
                query = query,
                filter = filter,
                results = results
            };
            
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextManagementPlugin] Error in SearchVectorDatabase: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("log_narrative_event")]
    [Description("Log important narrative events to the vector database for future reference")]
    public async Task<string> LogNarrativeEvent(
        [Description("Type of event (e.g., 'conversation', 'discovery', 'battle', 'story_event')")] string eventType,
        [Description("Brief summary of the event")] string eventSummary,
        [Description("Detailed description or transcript")] string eventDetails = "",
        [Description("List of entities involved in the event")] List<string> involvedEntities = null,
        [Description("Location where event occurred")] string locationId = "",
        [Description("Turn number when event occurred")] int? turnNumber = null)
    {
        Debug.WriteLine($"[ContextManagementPlugin] LogNarrativeEvent called: {eventType} - {eventSummary}");
        
        try
        {
            var result = await _informationManagementService.LogNarrativeEventAsync(
                eventType,
                eventSummary,
                eventDetails,
                involvedEntities ?? new List<string>(),
                locationId,
                null,
                turnNumber
            );
            
            var response = new
            {
                success = true,
                message = result,
                eventType = eventType,
                eventSummary = eventSummary,
                involvedEntities = involvedEntities ?? new List<string>(),
                locationId = locationId,
                loggedAt = DateTime.UtcNow
            };
            
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextManagementPlugin] Error in LogNarrativeEvent: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("update_game_state")]
    [Description("Update various aspects of the game state")]
    public async Task<string> UpdateGameState(
        [Description("Type of update: 'adventure_summary', 'recent_event', 'time', 'weather'")] string updateType,
        [Description("The new value or description for the update")] string value,
        [Description("Additional parameter for specific update types")] string additionalParam = "")
    {
        Debug.WriteLine($"[ContextManagementPlugin] UpdateGameState called: {updateType} - {value}");
        
        try
        {
            switch (updateType.ToLower())
            {
                case "adventure_summary":
                    await _worldManagementService.UpdateAdventureSummaryAsync(value);
                    break;
                    
                case "recent_event":
                    await _worldManagementService.AddRecentEventAsync(value);
                    break;
                    
                case "time":
                    if (Enum.TryParse<TimeOfDay>(value, true, out var timeOfDay))
                    {
                        await _worldManagementService.SetTimeOfDayAsync(timeOfDay);
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = $"Invalid time of day: {value}" }, _jsonOptions);
                    }
                    break;
                    
                case "weather":
                    if (Enum.TryParse<Weather>(value, true, out var weather))
                    {
                        await _worldManagementService.SetWeatherAsync(weather);
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = $"Invalid weather: {value}" }, _jsonOptions);
                    }
                    break;
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown update type: {updateType}" }, _jsonOptions);
            }
            
            var response = new
            {
                success = true,
                updateType = updateType,
                value = value,
                message = $"Game state updated: {updateType} = {value}",
                updatedAt = DateTime.UtcNow
            };
            
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextManagementPlugin] Error in UpdateGameState: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("manage_entity_relationships")]
    [Description("Manage relationships between entities (NPC-Location, Player-NPC, etc.)")]
    public async Task<string> ManageEntityRelationships(
        [Description("Type of relationship: 'npc_location', 'player_npc', 'npc_faction'")] string relationshipType,
        [Description("Action: 'add', 'remove', 'update', or 'get'")] string action,
        [Description("Primary entity ID")] string primaryEntityId,
        [Description("Secondary entity ID or value")] string secondaryEntityId = "",
        [Description("Relationship value (for numeric relationships like affinity)")] int relationshipValue = 0)
    {
        Debug.WriteLine($"[ContextManagementPlugin] ManageEntityRelationships called: {relationshipType} {action}");
        
        try
        {
            switch (relationshipType.ToLower())
            {
                case "npc_location":
                    switch (action.ToLower())
                    {
                        case "add":
                            await _npcManagementService.MoveNpcToLocationAsync(primaryEntityId, secondaryEntityId);
                            break;
                        case "remove":
                            await _npcManagementService.RemoveNpcFromLocationAsync(primaryEntityId);
                            break;
                    }
                    break;
                    
                case "player_npc":
                    switch (action.ToLower())
                    {
                        case "update":
                            await _npcManagementService.UpdateNpcRelationshipWithPlayerAsync(primaryEntityId, relationshipValue);
                            break;
                    }
                    break;
                    
                case "npc_faction":
                    switch (action.ToLower())
                    {
                        case "add":
                            await _npcManagementService.AddNpcToFactionAsync(primaryEntityId, secondaryEntityId);
                            break;
                        case "remove":
                            await _npcManagementService.RemoveNpcFromFactionAsync(primaryEntityId, secondaryEntityId);
                            break;
                    }
                    break;
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown relationship type: {relationshipType}" }, _jsonOptions);
            }
            
            var response = new
            {
                success = true,
                relationshipType = relationshipType,
                action = action,
                primaryEntityId = primaryEntityId,
                secondaryEntityId = secondaryEntityId,
                relationshipValue = relationshipValue,
                message = $"Relationship {action} completed for {relationshipType}",
                updatedAt = DateTime.UtcNow
            };
            
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextManagementPlugin] Error in ManageEntityRelationships: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}