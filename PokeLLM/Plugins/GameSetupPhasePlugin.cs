using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using PokeLLM.GameState;
using PokeLLM.Game.GameLogic;

namespace PokeLLM.Plugins;

public class GameSetupPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameSetupPhasePlugin(
        IGameStateRepository gameStateRepo,
        ICharacterManagementService characterManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _characterManagementService = characterManagementService;
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

    [KernelFunction("generate_random_stats")]
    [Description("Generate random ability scores using dice rolling method (configurable by ruleset)")]
    public async Task<string> GenerateRandomStats([Description("Number of stats to generate (based on ruleset)")] int statCount = 6)
    {
        try
        {
            // Generate stats using 4d6 drop lowest method (can be customized by ruleset)
            var stats = new List<int>();
            var random = new Random();
            
            for (int i = 0; i < statCount; i++)
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
            
            var result = new
            {
                success = true,
                stats = stats.ToArray(),
                total = stats.Sum(),
                description = $"Random stats generated using 4d6 drop lowest method ({statCount} stats)"
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("generate_standard_stats")]
    [Description("Generate standard ability score array - values configurable by ruleset")]
    public async Task<string> GenerateStandardStats([Description("Standard stat values to use")] int[] standardValues = null)
    {
        try
        {
            // Use default standard array if none provided, but allow ruleset to override
            var stats = standardValues ?? new int[] { 15, 14, 13, 12, 10, 8 };
            
            var result = new
            {
                success = true,
                stats,
                total = stats.Sum(),
                description = $"Standard array: {string.Join(", ", stats)} (assign to desired abilities as defined by ruleset)"
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
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