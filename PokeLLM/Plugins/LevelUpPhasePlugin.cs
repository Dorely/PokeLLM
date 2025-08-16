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
/// Plugin for handling character level up progression with generic ruleset support
/// </summary>
public class LevelUpPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IEntityService _entityService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public LevelUpPhasePlugin(
        IGameStateRepository gameStateRepo,
        ICharacterManagementService characterManagementService,
        IEntityService entityService,
        IInformationManagementService informationManagementService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _characterManagementService = characterManagementService;
        _entityService = entityService;
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

    [KernelFunction("manage_player_advancement")]
    [Description("Handle player character level ups and stat improvements")]
    public async Task<string> ManagePlayerAdvancement(
        [Description("Action: 'level_up', 'check_advancement_eligibility'")] string action,
        [Description("Reason or context for the advancement")] string advancementReason = "")
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManagePlayerAdvancement called: {action}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "level_up":
                    var currentPlayer = await _characterManagementService.GetPlayerDetails();
                    var newLevel = currentPlayer.Level + 1;
                    
                    // Update player level directly by adding experience
                    var expNeeded = (newLevel * 1000) - currentPlayer.Experience;
                    await _characterManagementService.AddPlayerExperiencePoints(expNeeded);
                    
                    // Log the level up as a narrative event
                    await _informationManagementService.LogNarrativeEventAsync(
                        "player_level_up",
                        $"Player advanced to level {newLevel}",
                        $"Through {advancementReason}, the player has grown stronger and reached level {newLevel}.",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    // Add to recent events
                    gameState.RecentEvents.Add(new EventLog 
                    { 
                        TurnNumber = gameState.GameTurnNumber, 
                        EventDescription = $"Player Level Up: Reached level {newLevel} - {advancementReason}" 
                    });
                    await _gameStateRepo.SaveStateAsync(gameState);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Player advanced to level {newLevel}!",
                        newLevel = newLevel,
                        previousLevel = newLevel - 1,
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "check_advancement_eligibility":
                    var playerDetails = await _characterManagementService.GetPlayerDetails();
                    var eligibilityInfo = new
                    {
                        currentLevel = playerDetails.Level,
                        currentExperience = playerDetails.Experience,
                        experienceToNextLevel = ((playerDetails.Level + 1) * 1000) - playerDetails.Experience,
                        canLevelUp = playerDetails.Experience >= ((playerDetails.Level + 1) * 1000),
                        conditions = playerDetails.Conditions
                    };
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        eligibility = eligibilityInfo,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown advancement action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManagePlayerAdvancement: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("manage_experience_and_rewards")]
    [Description("Handle experience point distribution and rewards from adventures")]
    public async Task<string> ManageExperienceAndRewards(
        [Description("Action: 'award_experience', 'calculate_exp_gain'")] string action,
        [Description("Amount of experience points")] int experiencePoints = 0,
        [Description("Reason for the experience/reward")] string reason = "",
        [Description("Type of accomplishment: 'battle_victory', 'quest_completion', 'discovery', 'training', 'bonding'")] string accomplishmentType = "battle_victory")
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManageExperienceAndRewards called: {action}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "award_experience":
                    if (experiencePoints <= 0)
                    {
                        return JsonSerializer.Serialize(new { error = "Experience points must be greater than 0" }, _jsonOptions);
                    }
                    
                    // Award experience to player
                    await _characterManagementService.AddPlayerExperiencePoints(experiencePoints);
                    
                    // Log the experience gain
                    await _informationManagementService.LogNarrativeEventAsync(
                        "experience_gained",
                        $"Player gained {experiencePoints} experience",
                        $"Through {accomplishmentType}, the player has gained {experiencePoints} experience points. {reason}",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Gained {experiencePoints} experience!",
                        experiencePoints = experiencePoints,
                        accomplishmentType = accomplishmentType,
                        reason = reason,
                        action = action
                    }, _jsonOptions);
                    
                case "calculate_exp_gain":
                    // Simplified experience calculation
                    var baseExp = accomplishmentType switch
                    {
                        "battle_victory" => 100,
                        "quest_completion" => 200,
                        "discovery" => 50,
                        "training" => 25,
                        "bonding" => 30,
                        _ => 50
                    };
                    
                    // Add some randomness
                    var random = new Random();
                    var randomBonus = random.Next(1, 21) * 5; // Simulate 1d20 * 5
                    var calculatedExp = baseExp + randomBonus;
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        calculatedExperience = calculatedExp,
                        baseExperience = baseExp,
                        randomBonus = randomBonus,
                        accomplishmentType = accomplishmentType,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown experience action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManageExperienceAndRewards: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
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
            
            // Log the completion
            await _informationManagementService.LogNarrativeEventAsync(
                "level_up_phase_completed",
                "Level up phase completed successfully",
                $"The level up phase has been completed with the following advancements: {levelUpSummary}",
                new List<string> { "player" },
                gameState.CurrentLocationId,
                null,
                gameState.GameTurnNumber
            );
            
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