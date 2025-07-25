using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling D&D-style dice mechanics, skill checks, and random events
/// Provides the core dice rolling functionality for the Pokemon D&D campaign
/// </summary>
public class DicePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public DicePlugin(IGameStateRepository repository)
    {
        _repository = repository;
        _random = new Random();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Basic Dice Rolling

    [KernelFunction("roll_d20")]
    public async Task<string> RollD20()
    {
        Debug.WriteLine($"[DicePlugin] RollD20 called");
        
        var roll = _random.Next(1, 21);

        var result = roll switch
        {
            20 => "Natural 20! Critical Success!",
            1 => "Natural 1! Critical Failure!",
            _ => $"Rolled {roll}"
        };

        Debug.WriteLine($"[DicePlugin] d20 roll: {roll}");
        return result;
    }

    [KernelFunction("roll_specific_dice")]
    public async Task<string> RollSpecifiedDice(int sizeOfDice = 6)
    {
        Debug.WriteLine($"[DicePlugin] RollSpecifiedDice called: d{sizeOfDice}");
        
        var roll = _random.Next(1, sizeOfDice + 1);

        var result = $"Rolled {roll}";

        Debug.WriteLine($"[DicePlugin] d{sizeOfDice} roll: {roll}");
        return result;
    }

    [KernelFunction("roll_d20_with_advantage")]
    public async Task<string> RollD20WithAdvantage()
    {
        Debug.WriteLine($"[DicePlugin] RollD20WithAdvantage called");
        
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var finalRoll = Math.Max(roll1, roll2);

        var result = $"Advantage roll: {roll1}, {roll2} -> Using {finalRoll}";

        Debug.WriteLine($"[DicePlugin] Advantage roll: {roll1}, {roll2} -> {finalRoll}");
        return result;
    }

    [KernelFunction("roll_d20_with_disadvantage")]
    public async Task<string> RollWithDisadvantage()
    {
        Debug.WriteLine($"[DicePlugin] RollWithDisadvantage called");
        
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var finalRoll = Math.Min(roll1, roll2);

        var result = $"Disadvantage roll: {roll1}, {roll2} -> Using {finalRoll}";

        Debug.WriteLine($"[DicePlugin] Disadvantage roll: {roll1}, {roll2} -> {finalRoll}");
        return result;
    }

    #endregion

    #region Skill Checks

    [KernelFunction("make_skill_check")]
    public async Task<string> MakeSkillCheck(
        [Description("Stat to use: Power, Speed, Mind, Charm, Defense, Spirit")] string statName,
        [Description("Difficulty Class (5=Very Easy, 8=Easy, 11=Medium, 14=Hard, 17=Very Hard, 20=Nearly Impossible)")] int difficultyClass,
        [Description("Whether the check has advantage (roll twice, take higher)")] bool advantage = false,
        [Description("Whether the check has disadvantage (roll twice, take lower)")] bool disadvantage = false,
        [Description("Additional modifier to add to the roll")] int modifier = 0)
    {
        Debug.WriteLine($"[DicePlugin] MakeSkillCheck called: stat={statName}, DC={difficultyClass}, adv={advantage}, dis={disadvantage}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Validate stat name
        if (!IsValidStatName(statName))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Power, Speed, Mind, Charm, Defense, or Spirit" }, _jsonOptions);

        // Get stat modifier
        var statLevel = GetStatLevel(gameState.Player.Character.Stats, statName);
        var statModifier = (int)statLevel;

        // Roll the dice based on advantage/disadvantage
        int diceRoll;
        string rollDetails;
        
        if (advantage && !disadvantage)
        {
            var roll1 = _random.Next(1, 21);
            var roll2 = _random.Next(1, 21);
            diceRoll = Math.Max(roll1, roll2);
            rollDetails = $"Advantage: {roll1}, {roll2} -> {diceRoll}";
        }
        else if (disadvantage && !advantage)
        {
            var roll1 = _random.Next(1, 21);
            var roll2 = _random.Next(1, 21);
            diceRoll = Math.Min(roll1, roll2);
            rollDetails = $"Disadvantage: {roll1}, {roll2} -> {diceRoll}";
        }
        else
        {
            diceRoll = _random.Next(1, 21);
            rollDetails = diceRoll.ToString();
        }

        var totalRoll = diceRoll + statModifier + modifier;
        var success = totalRoll >= difficultyClass;
        var margin = totalRoll - difficultyClass;

        // Determine degree of success/failure
        string outcome;
        if (diceRoll == 20)
            outcome = "Critical Success!";
        else if (diceRoll == 1)
            outcome = "Critical Failure!";
        else if (success)
        {
            outcome = margin >= 10 ? "Great Success!" : margin >= 5 ? "Good Success!" : "Success!";
        }
        else
        {
            outcome = margin <= -10 ? "Critical Failure!" : margin <= -5 ? "Bad Failure!" : "Failure!";
        }

        var difficultyDescription = difficultyClass switch
        {
            <= 5 => "Very Easy",
            <= 8 => "Easy", 
            <= 11 => "Medium",
            <= 14 => "Hard",
            <= 17 => "Very Hard",
            _ => "Nearly Impossible"
        };

        var result = $"{outcome} {statName} check: {totalRoll} vs DC {difficultyClass} ({margin:+#;-#;0})";

        Debug.WriteLine($"[DicePlugin] Skill check: {statName} {totalRoll} vs DC {difficultyClass} = {(success ? "SUCCESS" : "FAILURE")}");
        return result;
    }

    #endregion

    #region Helper Methods

    private StatLevel GetStatLevel(Stats stats, string statName)
    {
        return statName.ToLower() switch
        {
            "power" => stats.Power,
            "speed" => stats.Speed,
            "mind" => stats.Mind,
            "charm" => stats.Charm,
            "defense" => stats.Defense,
            "spirit" => stats.Spirit,
            _ => StatLevel.Novice
        };
    }

    private bool IsValidStatName(string statName)
    {
        return statName.ToLower() is "power" or "speed" or "mind" or "charm" or "defense" or "spirit";
    }

    #endregion
}