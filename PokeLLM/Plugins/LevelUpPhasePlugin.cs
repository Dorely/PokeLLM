using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling generic level up utilities and phase management
/// </summary>
public class LevelUpPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IEntityService _entityService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public LevelUpPhasePlugin(
        IGameStateRepository gameStateRepo,
        ICharacterManagementService characterManagementService,
        IEntityService entityService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _characterManagementService = characterManagementService;
        _entityService = entityService;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("manage_generic_advancement")]
    [Description("Handle generic character advancement operations")]
    public async Task<string> ManageGenericAdvancement(
        [Description("Action: 'get_player_status', 'add_experience'")] string action,
        [Description("Amount of experience to add (for add_experience action)")] int experiencePoints = 0,
        [Description("Reason or context for the advancement")] string reason = "")
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManageGenericAdvancement called: {action}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "get_player_status":
                    var playerDetails = await _characterManagementService.GetPlayerDetails();
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        playerLevel = playerDetails.Level,
                        playerExperience = playerDetails.Experience,
                        playerConditions = playerDetails.Conditions,
                        action = action
                    }, _jsonOptions);
                    
                case "add_experience":
                    if (experiencePoints <= 0)
                    {
                        return JsonSerializer.Serialize(new { error = "Experience points must be greater than 0" }, _jsonOptions);
                    }
                    
                    await _characterManagementService.AddPlayerExperiencePoints(experiencePoints);
                    
                    // Add to recent events
                    gameState.RecentEvents.Add(new EventLog 
                    { 
                        TurnNumber = gameState.GameTurnNumber, 
                        EventDescription = $"Player gained {experiencePoints} experience - {reason}" 
                    });
                    await _gameStateRepo.SaveStateAsync(gameState);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Added {experiencePoints} experience points",
                        experiencePoints = experiencePoints,
                        reason = reason,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown advancement action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManageGenericAdvancement: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("generate_random_values")]
    [Description("Generate random values for level up rewards and calculations")]
    public async Task<string> GenerateRandomValues(
        [Description("Type of random generation: 'experience_bonus', 'skill_improvement', 'random_range'")] string randomType,
        [Description("Base value for calculations")] int baseValue = 0,
        [Description("Minimum value for range generation")] int minValue = 1,
        [Description("Maximum value for range generation")] int maxValue = 100)
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] GenerateRandomValues called: {randomType}");
        
        try
        {
            var random = new Random();
            
            switch (randomType.ToLower())
            {
                case "experience_bonus":
                    var expBonus = random.Next(1, 21) * 5; // 1d20 * 5
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        randomValue = expBonus,
                        baseValue = baseValue,
                        totalValue = baseValue + expBonus,
                        randomType = randomType
                    }, _jsonOptions);
                    
                case "skill_improvement":
                    var skillBonus = random.Next(1, 7); // 1d6
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        randomValue = skillBonus,
                        randomType = randomType
                    }, _jsonOptions);
                    
                case "random_range":
                    if (minValue >= maxValue)
                    {
                        return JsonSerializer.Serialize(new { error = "Maximum value must be greater than minimum value" }, _jsonOptions);
                    }
                    var rangeValue = random.Next(minValue, maxValue + 1);
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        randomValue = rangeValue,
                        minValue = minValue,
                        maxValue = maxValue,
                        randomType = randomType
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown random type: {randomType}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in GenerateRandomValues: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, randomType = randomType }, _jsonOptions);
        }
    }

    [KernelFunction("finalize_level_up_phase")]
    [Description("Complete the level up phase and return to exploration")]
    public async Task<string> FinalizeLevelUpPhase(
        [Description("Summary of all advancement that occurred during this phase")] string levelUpSummary)
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] FinalizeLevelUpPhase called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Set the phase back to Exploration
            gameState.CurrentPhase = GamePhase.Exploration;
            
            // Set the phase change summary
            gameState.PhaseChangeSummary = $"Level up phase completed. {levelUpSummary}";
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"Level Up Phase Completed: {levelUpSummary}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            // Note: Event logging is now handled through VectorPlugin's manage_vector_store function
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Level up phase completed successfully",
                nextPhase = "Exploration",
                summary = levelUpSummary,
                sessionId = gameState.SessionId,
                phaseTransitionCompleted = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in FinalizeLevelUpPhase: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}