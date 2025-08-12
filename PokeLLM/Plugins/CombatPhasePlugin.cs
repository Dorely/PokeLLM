using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing D&D 5e-style Pokemon combat encounters
/// </summary>
public class CombatPhasePlugin
{
    private readonly IGameLogicService _gameLogicService;
    private readonly IGameStateRepository _gameStateRepo;
    private readonly JsonSerializerOptions _jsonOptions;

    public CombatPhasePlugin(IGameStateRepository gameStateRepo, IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("end_combat")]
    [Description("End the combat encounter and transition back to exploration")]
    public async Task EndCombat([Description("A summary of the combat encounter and the results")] string combatSummary)
    {
        Debug.WriteLine($"[CombatPhasePlugin] EndCombat called with summary: {combatSummary}");
        
        try
        {
            // Load the current game state
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Set the phase back to Exploration
            gameState.CurrentPhase = GamePhase.Exploration;
            
            // Save the combat summary in the phase change summary
            gameState.PhaseChangeSummary = combatSummary;
            
            // Clear the combat state since combat is ending
            gameState.CombatState = null;
            
            // Add the combat result to recent events for continuity
            gameState.RecentEvents.Add(new EventLog { TurnNumber = gameState.GameTurnNumber, EventDescription = $"Combat Ended: {combatSummary}" });
            
            // Update the last save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the updated game state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            Debug.WriteLine($"[CombatPhasePlugin] Successfully ended combat and transitioned to Exploration phase");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CombatPhasePlugin] Error ending combat: {ex.Message}");
            throw;
        }
    }

    [KernelFunction("make_skill_check")]
    [Description("Makes a skill check using D&D 5e rules with the player's ability scores")]
    public async Task<string> MakeSkillCheck(
        [Description("Stat to use: Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma")] string statName,
        [Description("Difficulty Class (5=Very Easy, 8=Easy, 11=Medium, 14=Hard, 17=Very Hard, 20=Nearly Impossible)")] int difficultyClass,
        [Description("Whether the check has advantage (roll twice, take higher)")] bool advantage = false,
        [Description("Whether the check has disadvantage (roll twice, take lower)")] bool disadvantage = false,
        [Description("Additional modifier to add to the roll")] int modifier = 0)
    {
        Debug.WriteLine($"[CombatPhasePlugin] MakeSkillCheck called: stat={statName}, DC={difficultyClass}, adv={advantage}, dis={disadvantage}");

        try
        {
            var result = await _gameLogicService.MakeSkillCheckAsync(statName, difficultyClass, advantage, disadvantage, modifier);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.WriteLine($"[CombatPhasePlugin] Skill check error: {result.Error}");
                return JsonSerializer.Serialize(new { error = result.Error }, _jsonOptions);
            }

            var formattedResult = $"{result.Outcome} {result.StatName} check: {result.TotalRoll} vs DC {result.DifficultyClass} ({result.Margin:+#;-#;0})";

            Debug.WriteLine($"[CombatPhasePlugin] Skill check: {result.StatName} {result.TotalRoll} vs DC {result.DifficultyClass} = {(result.Success ? "SUCCESS" : "FAILURE")}");
            return formattedResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CombatPhasePlugin] Error in MakeSkillCheck: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}