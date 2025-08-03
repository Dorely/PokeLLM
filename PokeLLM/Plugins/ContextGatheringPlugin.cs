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
/// Plugin for gathering contextual information before the main game phase processes user input.
/// This is a lightweight, read-only service that assembles relevant context from vector database and game state.
/// </summary>
public class ContextGatheringPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ContextGatheringPlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("get_full_game_state")]
    [Description("Get the complete current game state for context gathering")]
    public async Task<string> GetFullGameState()
    {
        Debug.WriteLine($"[ContextGatheringPlugin] GetFullGameState called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            Debug.WriteLine($"[ContextGatheringPlugin] Retrieved full game state with {gameState.WorldLocations.Count} locations, {gameState.WorldNpcs.Count} NPCs");
            return JsonSerializer.Serialize(gameState, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error getting full game state: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("search_entities_vector")]
    [Description("Search for entities (NPCs, Pokemon, objects) in the vector database by query")]
    public async Task<string> SearchEntitiesVector(
        [Description("Search query for entities")] string query,
        [Description("Optional entity type to filter by")] string entityType = null)
    {
        Debug.WriteLine($"[ContextGatheringPlugin] SearchEntitiesVector called with query: {query}");
        
        try
        {
            var results = await _informationManagementService.SearchEntitiesAsync(new List<string> { query }, entityType);
            
            var response = new
            {
                query = query,
                entityType = entityType,
                results = results.Select(r => new
                {
                    entityId = r.EntityId,
                    entityType = r.EntityType,
                    name = r.Name,
                    description = r.Description,
                    propertiesJson = r.PropertiesJson
                }).ToList()
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Found {results.Count()} entities for query: {query}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error searching entities: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("search_lore_vector")]
    [Description("Search for lore, rules, species data, and world information in the vector database")]
    public async Task<string> SearchLoreVector(
        [Description("Search query for lore")] string query,
        [Description("Optional entry type to filter by")] string entryType = null)
    {
        Debug.WriteLine($"[ContextGatheringPlugin] SearchLoreVector called with query: {query}");
        
        try
        {
            var results = await _informationManagementService.SearchLoreAsync(new List<string> { query }, entryType);
            
            var response = new
            {
                query = query,
                entryType = entryType,
                results = results.Select(r => new
                {
                    entryId = r.EntryId,
                    entryType = r.EntryType,
                    title = r.Title,
                    content = r.Content,
                    tags = r.Tags
                }).ToList()
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Found {results.Count()} lore entries for query: {query}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error searching lore: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("search_game_rules_vector")]
    [Description("Search for game rules, trainer classes, and mechanical data in the vector database")]
    public async Task<string> SearchGameRulesVector(
        [Description("Search query for game rules")] string query,
        [Description("Optional entry type to filter by")] string entryType = null)
    {
        Debug.WriteLine($"[ContextGatheringPlugin] SearchGameRulesVector called with query: {query}");
        
        try
        {
            var results = await _informationManagementService.SearchGameRulesAsync(new List<string> { query }, entryType);
            
            var response = new
            {
                query = query,
                entryType = entryType,
                results = results.Select(r => new
                {
                    entryId = r.EntryId,
                    entryType = r.EntryType,
                    title = r.Title,
                    content = r.Content,
                    tags = r.Tags
                }).ToList()
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Found {results.Count()} game rule entries for query: {query}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error searching game rules: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("search_narrative_memories")]
    [Description("Search for past narrative events and memories in the vector database")]
    public async Task<string> SearchNarrativeMemories(
        [Description("Search query for narrative events")] string query,
        [Description("List of entities to filter by (optional)")] List<string> involvedEntities = null,
        [Description("Minimum relevance score (0.0 to 1.0)")] double minRelevanceScore = 0.75)
    {
        Debug.WriteLine($"[ContextGatheringPlugin] SearchNarrativeMemories called with query: {query}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var results = await _informationManagementService.FindMemoriesAsync(
                gameState.SessionId, 
                query, 
                involvedEntities, 
                minRelevanceScore);
            
            var response = new
            {
                query = query,
                sessionId = gameState.SessionId,
                involvedEntities = involvedEntities,
                minRelevanceScore = minRelevanceScore,
                results = results.Select(r => new
                {
                    gameTurnNumber = r.GameTurnNumber,
                    eventType = r.EventType,
                    eventSummary = r.EventSummary,
                    fullTranscript = r.FullTranscript,
                    involvedEntities = r.InvolvedEntities,
                    locationId = r.LocationId
                }).ToList()
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Found {results.Count()} narrative memories for query: {query}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error searching narrative memories: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("get_entity_by_id")]
    [Description("Get a specific entity by its ID from the vector database")]
    public async Task<string> GetEntityById(
        [Description("The entity ID to retrieve")] string entityId)
    {
        Debug.WriteLine($"[ContextGatheringPlugin] GetEntityById called with ID: {entityId}");
        
        try
        {
            var entity = await _informationManagementService.GetEntityAsync(entityId);
            
            if (entity == null)
            {
                return JsonSerializer.Serialize(new { entityId = entityId, found = false }, _jsonOptions);
            }
            
            var response = new
            {
                entityId = entity.EntityId,
                entityType = entity.EntityType,
                name = entity.Name,
                description = entity.Description,
                propertiesJson = entity.PropertiesJson,
                found = true
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Retrieved entity: {entityId}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error getting entity by ID: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, entityId = entityId }, _jsonOptions);
        }
    }

    [KernelFunction("get_location_by_id")]
    [Description("Get a specific location by its ID from the vector database")]
    public async Task<string> GetLocationById(
        [Description("The location ID to retrieve")] string locationId)
    {
        Debug.WriteLine($"[ContextGatheringPlugin] GetLocationById called with ID: {locationId}");
        
        try
        {
            var location = await _informationManagementService.GetLocationAsync(locationId);
            
            if (location == null)
            {
                return JsonSerializer.Serialize(new { locationId = locationId, found = false }, _jsonOptions);
            }
            
            var response = new
            {
                locationId = location.LocationId,
                name = location.Name,
                description = location.Description,
                region = location.Region,
                tags = location.Tags,
                found = true
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Retrieved location: {locationId}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error getting location by ID: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, locationId = locationId }, _jsonOptions);
        }
    }

    [KernelFunction("get_current_location_context")]
    [Description("Get detailed context about the player's current location including present entities")]
    public async Task<string> GetCurrentLocationContext()
    {
        Debug.WriteLine($"[ContextGatheringPlugin] GetCurrentLocationContext called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            if (string.IsNullOrEmpty(gameState.CurrentLocationId))
            {
                return JsonSerializer.Serialize(new { error = "No current location set" }, _jsonOptions);
            }
            
            // Get location from game state
            var location = gameState.WorldLocations.ContainsKey(gameState.CurrentLocationId)
                ? gameState.WorldLocations[gameState.CurrentLocationId]
                : null;
            
            // Get present NPCs
            var presentNpcs = new List<object>();
            if (location != null)
            {
                foreach (var npcId in location.PresentNpcIds)
                {
                    if (gameState.WorldNpcs.ContainsKey(npcId))
                    {
                        var npcDetails = gameState.WorldNpcs[npcId];
                        presentNpcs.Add(new
                        {
                            id = npcId, // Use the key as the ID
                            characterClass = npcDetails.CharacterDetails.Class,
                            money = npcDetails.CharacterDetails.Money,
                            globalRenown = npcDetails.CharacterDetails.GlobalRenown,
                            globalNotoriety = npcDetails.CharacterDetails.GlobalNotoriety,
                            inventory = npcDetails.CharacterDetails.Inventory
                        });
                    }
                }
            }
            
            // Get present Pokemon
            var presentPokemon = new List<object>();
            if (location != null)
            {
                foreach (var pokemonId in location.PresentPokemonIds)
                {
                    if (gameState.WorldPokemon.ContainsKey(pokemonId))
                    {
                        var pokemon = gameState.WorldPokemon[pokemonId];
                        presentPokemon.Add(new
                        {
                            id = pokemon.Id,
                            species = pokemon.Species,
                            level = pokemon.Level,
                            type1 = pokemon.Type1.ToString(),
                            type2 = pokemon.Type2?.ToString()
                        });
                    }
                }
            }
            
            // Try to get additional location details from vector store
            LocationVectorRecord vectorLocation = null;
            try
            {
                vectorLocation = await _informationManagementService.GetLocationAsync(gameState.CurrentLocationId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextGatheringPlugin] Could not retrieve vector location: {ex.Message}");
            }
            
            var response = new
            {
                currentLocationId = gameState.CurrentLocationId,
                locationDetails = location != null ? new
                {
                    id = location.Id,
                    name = location.Name,
                    exits = location.Exits,
                    pointsOfInterest = location.PointsOfInterest
                } : null,
                vectorLocationDetails = vectorLocation != null ? new
                {
                    name = vectorLocation.Name,
                    description = vectorLocation.Description,
                    region = vectorLocation.Region,
                    tags = vectorLocation.Tags
                } : null,
                presentNpcs = presentNpcs,
                presentPokemon = presentPokemon,
                timeOfDay = gameState.TimeOfDay?.ToString(),
                weather = gameState.Weather?.ToString()
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Retrieved current location context: {gameState.CurrentLocationId}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error getting current location context: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("get_player_context")]
    [Description("Get detailed context about the player character and their current status")]
    public async Task<string> GetPlayerContext()
    {
        Debug.WriteLine($"[ContextGatheringPlugin] GetPlayerContext called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            var response = new
            {
                playerName = gameState.Player.Name,
                playerDescription = gameState.Player.Description,
                level = gameState.Player.Level,
                experience = gameState.Player.Experience,
                characterClass = gameState.Player.CharacterDetails.Class,
                stats = gameState.Player.Stats,
                conditions = gameState.Player.Conditions,
                money = gameState.Player.CharacterDetails.Money,
                globalRenown = gameState.Player.CharacterDetails.GlobalRenown,
                globalNotoriety = gameState.Player.CharacterDetails.GlobalNotoriety,
                gymBadges = gameState.Player.GymBadges,
                teamPokemon = gameState.Player.TeamPokemon.Select(tp => new
                {
                    pokemon = new
                    {
                        id = tp.Pokemon.Id,
                        nickName = tp.Pokemon.NickName,
                        species = tp.Pokemon.Species,
                        level = tp.Pokemon.Level,
                        type1 = tp.Pokemon.Type1.ToString(),
                        type2 = tp.Pokemon.Type2?.ToString(),
                        stats = tp.Pokemon.Stats,
                        knownMoves = tp.Pokemon.KnownMoves,
                        statusEffects = tp.Pokemon.StatusEffects
                    },
                    friendship = tp.Friendship,
                    experience = tp.Experience,
                    caughtLocationId = tp.CaughtLocationId
                }).ToList(),
                inventory = gameState.Player.CharacterDetails.Inventory,
                npcRelationships = gameState.Player.PlayerNpcRelationships,
                factionRelationships = gameState.Player.PlayerFactionRelationships
            };
            
            Debug.WriteLine($"[ContextGatheringPlugin] Retrieved player context for: {gameState.Player.Name}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextGatheringPlugin] Error getting player context: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}