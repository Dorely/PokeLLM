using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using PokeLLM.GameState;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.VectorStore.Models;

namespace PokeLLM.Plugins;

public class VectorPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public VectorPlugin(
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

    [KernelFunction("manage_vector_store")]
    [Description("Handle all vector store operations for entities, locations, lore, rules, and narrative logs")]
    public async Task<string> ManageVectorStore(
        [Description("Operation: 'search_entities', 'upsert_entity', 'get_entity', 'search_locations', 'upsert_location', 'get_location', 'search_lore', 'upsert_lore', 'get_lore', 'search_rules', 'upsert_rule', 'get_rule', 'log_event', 'find_memories'")] string operation,
        [Description("Search queries (for search operations)")] List<string> queries = null,
        [Description("Entity/Location/Entry ID (for specific lookups)")] string id = "",
        [Description("Entity/Entry type for filtering")] string type = "",
        [Description("Name/Title of the entry")] string name = "",
        [Description("Description/Content of the entry")] string content = "",
        [Description("Additional properties as JSON")] string propertiesJson = "",
        [Description("Tags for categorization")] List<string> tags = null,
        [Description("Region (for locations)")] string region = "",
        [Description("Event type (for logging)")] string eventType = "",
        [Description("Full transcript (for events)")] string fullTranscript = "",
        [Description("Involved entities (for events)")] List<string> involvedEntities = null,
        [Description("Location ID (for events)")] string locationId = "")
    {
        Debug.WriteLine($"[VectorPlugin] ManageVectorStore called: {operation}");
        
        try
        {
            switch (operation.ToLower())
            {
                // Entity operations
                case "search_entities":
                    var entityResults = await _informationManagementService.SearchEntitiesAsync(queries ?? new List<string>(), type);
                    return JsonSerializer.Serialize(new { entities = entityResults, operation = operation }, _jsonOptions);
                    
                case "upsert_entity":
                    var entityResult = await _informationManagementService.UpsertEntityAsync(id, type, name, content, propertiesJson ?? "{}");
                    return JsonSerializer.Serialize(new { result = entityResult, operation = operation }, _jsonOptions);
                    
                case "get_entity":
                    var entity = await _informationManagementService.GetEntityAsync(id);
                    return JsonSerializer.Serialize(new { entity = entity, operation = operation }, _jsonOptions);
                
                // Location operations
                case "search_locations":
                    // Note: No direct SearchLocationsAsync method exists, so we search by ID only
                    if (queries != null && queries.Any())
                    {
                        var searchLocations = new List<LocationVectorRecord>();
                        foreach (var query in queries.Where(q => !string.IsNullOrWhiteSpace(q)))
                        {
                            var foundLocation = await _informationManagementService.GetLocationAsync(query);
                            if (foundLocation != null)
                                searchLocations.Add(foundLocation);
                        }
                        return JsonSerializer.Serialize(new { locations = searchLocations, operation = operation }, _jsonOptions);
                    }
                    return JsonSerializer.Serialize(new { locations = new List<object>(), operation = operation }, _jsonOptions);
                    
                case "get_location":
                    var location = await _informationManagementService.GetLocationAsync(id);
                    return JsonSerializer.Serialize(new { location = location, operation = operation }, _jsonOptions);
                    
                case "upsert_location":
                    var locationResult = await _informationManagementService.UpsertLocationAsync(id, name, content, region, tags);
                    return JsonSerializer.Serialize(new { result = locationResult, operation = operation }, _jsonOptions);
                
                // Lore operations
                case "search_lore":
                    var loreResults = await _informationManagementService.SearchLoreAsync(queries ?? new List<string>(), type);
                    return JsonSerializer.Serialize(new { lore = loreResults, operation = operation }, _jsonOptions);
                    
                case "upsert_lore":
                    var loreResult = await _informationManagementService.UpsertLoreAsync(id, type, name, content, tags);
                    return JsonSerializer.Serialize(new { result = loreResult, operation = operation }, _jsonOptions);
                    
                case "get_lore":
                    var loreEntry = await _informationManagementService.GetLoreAsync(id);
                    return JsonSerializer.Serialize(new { lore = loreEntry, operation = operation }, _jsonOptions);
                
                // Game rule operations
                case "search_rules":
                    var ruleResults = await _informationManagementService.SearchGameRulesAsync(queries ?? new List<string>(), type);
                    return JsonSerializer.Serialize(new { rules = ruleResults, operation = operation }, _jsonOptions);
                    
                case "upsert_rule":
                    var ruleResult = await _informationManagementService.UpsertGameRuleAsync(id, type, name, content, tags);
                    return JsonSerializer.Serialize(new { result = ruleResult, operation = operation }, _jsonOptions);
                    
                case "get_rule":
                    var rule = await _informationManagementService.GetGameRuleAsync(id);
                    return JsonSerializer.Serialize(new { rule = rule, operation = operation }, _jsonOptions);
                
                // Narrative log operations
                case "log_event":
                    var gameState = await _gameStateRepo.LoadLatestStateAsync();
                    var logResult = await _informationManagementService.LogNarrativeEventAsync(eventType, name, fullTranscript, involvedEntities ?? new List<string>(), locationId, null, gameState.GameTurnNumber);
                    return JsonSerializer.Serialize(new { result = logResult, operation = operation }, _jsonOptions);
                    
                case "find_memories":
                    var currentGameState = await _gameStateRepo.LoadLatestStateAsync();
                    var memories = await _informationManagementService.FindMemoriesAsync(currentGameState.SessionId, content, involvedEntities);
                    return JsonSerializer.Serialize(new { memories = memories, operation = operation }, _jsonOptions);
                
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown vector store operation: {operation}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VectorPlugin] Error in ManageVectorStore: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, operation = operation }, _jsonOptions);
        }
    }
}