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

    [KernelFunction("vector_game_rule_lookup")]
    [Description("Search for trainer class data or other rule data in the vector store")]
    public async Task<string> VectorLookups(
        [Description("List of Search queries to find relevant class or other rule data")] List<string> queries)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] VectorLookups called with queries: {queries.ToString()}");
        
        try
        {
            var ruleResults = await _informationManagementService.SearchGameRulesAsync(queries);
            
            var json = JsonSerializer.Serialize(ruleResults, _jsonOptions);
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Found {ruleResults.Count()} Results");
            return json;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error in VectorLookup: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("vector_game_rule_upsert")]
    [Description("Store new trainer class data to the vector store for future use")]
    public async Task<string> VectorUpsert(
        [Description("An object defining the class or other rule data to be inserted/updated")] GameRuleVectorRecord data)
    {
        Debug.WriteLine($"[CharacterCreationPhasePlugin] VectorUpserts called with data length: {data.ToString()}");
        
        try
        {
            var result = await _informationManagementService
                .UpsertGameRuleAsync(
                    data.EntryId, 
                    data.EntryType, 
                    data.Title, 
                    data.Content, 
                    data.Tags?.ToList(), 
                    data.Id
                );
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Successfully processed upsert. {result}");
            return JsonSerializer.Serialize(new { success = true, result }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Error in VectorUpsert: {ex.Message}");
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
            
            var result = new
            {
                stats,
                total = stats.Sum(),
                average = stats.Average()
            };
            
            Debug.WriteLine($"[CharacterCreationPhasePlugin] Generated random stats: {stats.ToString()}");
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
