using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling character creation mechanics using D&D 5e-style ability scores
/// </summary>
public class CharacterCreationPhasePlugin
{
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly IGameStateRepository _gameStateRepo;
    private readonly JsonSerializerOptions _jsonOptions;

    public CharacterCreationPhasePlugin(
        ICharacterManagementService characterManagementService,
        IInformationManagementService informationManagementService,
        IGameLogicService gameLogicService,
        IGameStateRepository gameStateRepo)
    {
        _characterManagementService = characterManagementService;
        _informationManagementService = informationManagementService;
        _gameLogicService = gameLogicService;
        _gameStateRepo = gameStateRepo;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    //TODO refactor/rename this
    [KernelFunction("vector_lookups")]
    [Description("Search for trainer class data and other entities in the vector store")]
    public async Task<string> VectorLookups(
        [Description("List of search queries to find relevant entities (classes, lore, etc.)")] List<string> queries,
        [Description("Optional entity type filter (e.g., 'TrainerClass', 'Rule')")] string entityType = null)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] VectorLookups called with queries: {string.Join(", ", queries)}");
        
        try
        {
            var entities = await _informationManagementService.SearchEntitiesAsync(queries.ToArray(), entityType);
            var lore = await _informationManagementService.SearchLoreAsync(queries.ToArray(), entityType);
            
            var results = new
            {
                entities = entities.Select(e => new
                {
                    id = e.EntityId,
                    type = e.EntityType,
                    name = e.Name,
                    description = e.Description,
                    properties = e.PropertiesJson
                }).ToList(),
                lore = lore.Select(l => new
                {
                    id = l.EntryId,
                    type = l.EntryType,
                    title = l.Title,
                    content = l.Content,
                    tags = l.Tags
                }).ToList()
            };
            
            var json = JsonSerializer.Serialize(results, _jsonOptions);
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Found {entities.Count()} entities and {lore.Count()} lore entries");
            return json;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error in VectorLookups: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    //TODO refactor/rename this
    [KernelFunction("vector_upserts")]
    [Description("Store new trainer class or lore data in the vector store")]
    public async Task<string> VectorUpserts(
        [Description("JSON data containing entity or lore information to store")] string data)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] VectorUpserts called with data length: {data.Length}");
        
        try
        {
            var dataObject = JsonSerializer.Deserialize<JsonElement>(data);
            var results = new List<string>();
            
            if (dataObject.TryGetProperty("entities", out var entitiesElement))
            {
                foreach (var entity in entitiesElement.EnumerateArray())
                {
                    var entityId = entity.GetProperty("id").GetString();
                    var entityType = entity.GetProperty("type").GetString();
                    var name = entity.GetProperty("name").GetString();
                    var description = entity.GetProperty("description").GetString();
                    var properties = entity.TryGetProperty("properties", out var prop) ? prop.GetRawText() : "{}";
                    
                    var result = await _informationManagementService.UpsertEntityAsync(entityId, entityType, name, description, properties);
                    results.Add(result);
                }
            }
            
            if (dataObject.TryGetProperty("lore", out var loreElement))
            {
                foreach (var loreEntry in loreElement.EnumerateArray())
                {
                    var entryId = loreEntry.GetProperty("id").GetString();
                    var entryType = loreEntry.GetProperty("type").GetString();
                    var title = loreEntry.GetProperty("title").GetString();
                    var content = loreEntry.GetProperty("content").GetString();
                    var tags = loreEntry.TryGetProperty("tags", out var tagsArray) 
                        ? tagsArray.EnumerateArray().Select(t => t.GetString()).ToArray() 
                        : Array.Empty<string>();
                    
                    var result = await _informationManagementService.UpsertLoreAsync(entryId, entryType, title, content, tags);
                    results.Add(result);
                }
            }
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Successfully processed {results.Count} upserts");
            return JsonSerializer.Serialize(new { success = true, results }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error in VectorUpserts: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("set_player_name")]
    [Description("Set the player's trainer name")]
    public async Task<string> SetPlayerName(
        [Description("The chosen trainer name")] string name)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] SetPlayerName called with name: {name}");
        
        try
        {
            await _characterManagementService.SetPlayerName(name);
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Successfully set player name to: {name}");
            return $"Player name set to: {name}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error setting player name: {ex.Message}");
            return $"Error setting player name: {ex.Message}";
        }
    }

    [KernelFunction("set_player_class")]
    [Description("Set the player's trainer class")]
    public async Task<string> SetPlayerClass(
        [Description("The chosen class ID (e.g., 'class_researcher', 'class_athlete')")] string classId)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] SetPlayerClass called with classId: {classId}");
        
        try
        {
            await _characterManagementService.SetPlayerClass(classId);
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Successfully set player class to: {classId}");
            return $"Player class set to: {classId}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error setting player class: {ex.Message}");
            return $"Error setting player class: {ex.Message}";
        }
    }

    [KernelFunction("set_player_stats")]
    [Description("Set the player's ability scores (Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma)")]
    public async Task<string> SetPlayerStats(
        [Description("Array of 6 ability scores: [Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma]")] int[] stats)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] SetPlayerStats called with stats: [{string.Join(", ", stats)}]");
        
        try
        {
            if (stats.Length != 6)
            {
                return "Error: Must provide exactly 6 ability scores [Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma]";
            }
            
            // Validate stat values (3-18 is typical for D&D)
            foreach (var stat in stats)
            {
                if (stat < 3 || stat > 20)
                {
                    return $"Error: Ability scores must be between 3 and 20. Invalid value: {stat}";
                }
            }
            
            await _characterManagementService.SetPlayerStats(stats);
            
            var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            var statString = string.Join(", ", statNames.Zip(stats, (name, value) => $"{name}: {value}"));
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Successfully set player stats: {statString}");
            return $"Player stats set - {statString}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error setting player stats: {ex.Message}");
            return $"Error setting player stats: {ex.Message}";
        }
    }

    [KernelFunction("generate_random_stats")]
    [Description("Generate random ability scores using 4d6 drop lowest method")]
    public async Task<string> GenerateRandomStats()
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] GenerateRandomStats called");
        
        try
        {
            var stats = await _characterManagementService.GenerateRandomStats();
            var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            var statString = string.Join(", ", statNames.Zip(stats, (name, value) => $"{name}: {value}"));
            
            var result = new
            {
                stats,
                total = stats.Sum(),
                average = stats.Average(),
                description = statString
            };
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Generated random stats: {statString}");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error generating random stats: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("generate_standard_stats")]
    [Description("Generate standard ability score array (15, 14, 13, 12, 10, 8) for balanced characters")]
    public async Task<string> GenerateStandardStats()
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] GenerateStandardStats called");
        
        try
        {
            var stats = await _characterManagementService.GenerateStandardStats();
            var result = new
            {
                stats,
                total = stats.Sum(),
                description = "Standard array: 15, 14, 13, 12, 10, 8 (assign to desired abilities)"
            };
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Generated standard stats array");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error generating standard stats: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    
    [KernelFunction("finalize_character_creation")]
    [Description("Complete character creation and transition to the next phase")]
    public async Task finalizeCharacterCreation(
        [Description("Summary of the character creation process and what happens next")] string summary)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] FinalizeCharacterCreation called");
        
        try
        {
            // Load current game state
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Set phase to Exploration (or WorldGeneration if world isn't ready)
            gameState.CurrentPhase = GamePhase.Exploration;
            
            // Set the phase change summary
            gameState.PhaseChangeSummary = summary;
            
            // Add character creation completion to recent events
            gameState.RecentEvents.Add($"Character Creation Completed: {summary}");
            
            // Update the last save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the updated game state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Successfully completed character creation and transitioned to Exploration phase");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error finalizing character creation: {ex.Message}");
            throw;
        }
    }
}
