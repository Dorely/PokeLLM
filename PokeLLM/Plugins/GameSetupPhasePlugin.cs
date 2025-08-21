using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using PokeLLM.GameState;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.VectorStore.Models;

namespace PPokeLLM.Game.Plugins;

public class GameSetupPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IRulesetManager _rulesetManager;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameSetupPhasePlugin(
        IGameStateRepository gameStateRepo,
        ICharacterManagementService characterManagementService,
        IRulesetManager rulesetManager,
        IVectorStoreService vectorStoreService)
    {
        _gameStateRepo = gameStateRepo;
        _characterManagementService = characterManagementService;
        _rulesetManager = rulesetManager;
        _vectorStoreService = vectorStoreService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    // === GENERIC CHARACTER CREATION FUNCTIONS ===

    [KernelFunction("set_player_name")]
    [Description("Set the player's character name")]
    public async Task<string> SetPlayerName([Description("The chosen character name")] string name)
    {
        try
        {
            await _characterManagementService.SetPlayerName(name);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Player name set to: {name}",
                playerName = name
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error setting player name: {ex.Message}"
            }, _jsonOptions);
        }
    }

    [KernelFunction("set_player_stats")]
    [Description("Set the player's ability scores based on the current ruleset")]
    public async Task<string> SetPlayerStats(
        [Description("Dictionary of stat names to values based on current ruleset")] Dictionary<string, int> stats)
    {
        try
        {
            if (stats == null || stats.Count == 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Stats dictionary cannot be null or empty" 
                }, _jsonOptions);
            }
            
            // Store stats in RulesetGameData - let the ruleset define what stats exist
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            gameState.RulesetGameData["stats"] = JsonSerializer.SerializeToElement(stats);
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Player stats set successfully",
                stats = stats
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("set_adventure_setting")]
    [Description("Set the adventure setting for the upcoming game - used by WorldGeneration phase")]
    public async Task<string> SetAdventureSetting([Description("The chosen adventure setting or theme")] string setting)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(setting))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Adventure setting cannot be null or empty" 
                }, _jsonOptions);
            }
            
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            // Store adventure setting in RulesetGameData
            gameState.RulesetGameData["adventureSetting"] = JsonSerializer.SerializeToElement(setting.Trim());
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Adventure setting set to: {setting}",
                adventureSetting = setting.Trim()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error setting adventure setting: {ex.Message}" 
            }, _jsonOptions);
        }
    }




    [KernelFunction("roll_stats")]
    [Description("Generate ability scores using specified method - supports various dice rolling techniques")]
    public async Task<string> RollStats([Description("Rolling method: '4d6_drop_lowest', 'standard_array', 'point_buy', '3d6_straight' - defaults to 4d6_drop_lowest")] string method = "4d6_drop_lowest")
    {
        try
        {
            var stats = new List<int>();
            var random = new Random();
            string description;
            
            switch (method.ToLower())
            {
                case "4d6_drop_lowest":
                    for (int i = 0; i < 6; i++)
                    {
                        var rolls = new List<int>();
                        for (int j = 0; j < 4; j++)
                        {
                            rolls.Add(random.Next(1, 7));
                        }
                        rolls.Sort();
                        rolls.RemoveAt(0); // Remove lowest
                        stats.Add(rolls.Sum());
                    }
                    description = "Random stats generated using 4d6 drop lowest method";
                    break;
                    
                case "standard_array":
                    stats.AddRange(new int[] { 15, 14, 13, 12, 10, 8 });
                    description = "Standard array: 15, 14, 13, 12, 10, 8 (assign to desired abilities)";
                    break;
                    
                case "3d6_straight":
                    for (int i = 0; i < 6; i++)
                    {
                        var roll = 0;
                        for (int j = 0; j < 3; j++)
                        {
                            roll += random.Next(1, 7);
                        }
                        stats.Add(roll);
                    }
                    description = "Random stats generated using 3d6 straight rolls";
                    break;
                    
                case "point_buy":
                    // Start with base 8 in all stats for point buy system
                    stats.AddRange(new int[] { 8, 8, 8, 8, 8, 8 });
                    description = "Point buy base stats (8 in all abilities, use point buy rules to customize)";
                    break;
                    
                default:
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"Unknown rolling method: {method}. Supported methods: 4d6_drop_lowest, standard_array, 3d6_straight, point_buy"
                    }, _jsonOptions);
            }
            
            var result = new
            {
                success = true,
                method = method,
                stats = stats.ToArray(),
                total = stats.Sum(),
                description = description
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error generating stats: {ex.Message}" 
            }, _jsonOptions);
        }
    }


    [KernelFunction("finalize_game_setup")]
    [Description("Complete game setup and signal for transition to World Generation phase")]
    public async Task<string> FinalizeGameSetup([Description("Summary of setup choices made")] string setupSummary)
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var player = gameState.Player;
            
            // Basic validation - only check for player name (let ruleset define other requirements)
            if (string.IsNullOrEmpty(player.Name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = "Setup incomplete - player name is required"
                }, _jsonOptions);
            }
            
            // Update adventure summary and transition to WorldGeneration phase
            gameState.AdventureSummary = $"A new adventure begins with {player.Name}. {setupSummary}";
            gameState.CurrentPhase = GamePhase.WorldGeneration;
            gameState.PhaseChangeSummary = $"Game setup completed successfully. {setupSummary}";
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Game setup completed successfully",
                playerName = player.Name,
                setupComplete = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }
}