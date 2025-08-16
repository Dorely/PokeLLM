using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

#region Result Classes for Dice and Skill Systems

/// <summary>
/// Result of a dice roll operation
/// </summary>
public class DiceRollResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rolls")]
    public List<int> Rolls { get; set; } = new();

    [JsonPropertyName("diceNotation")]
    public string DiceNotation { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; }
}

/// <summary>
/// Result of a skill check operation
/// </summary>
public class SkillCheckResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("totalRoll")]
    public int TotalRoll { get; set; }

    [JsonPropertyName("diceRoll")]
    public int DiceRoll { get; set; }

    [JsonPropertyName("statModifier")]
    public int StatModifier { get; set; }

    [JsonPropertyName("additionalModifier")]
    public int AdditionalModifier { get; set; }

    [JsonPropertyName("difficultyClass")]
    public int DifficultyClass { get; set; }

    [JsonPropertyName("margin")]
    public int Margin { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [JsonPropertyName("rollDetails")]
    public string RollDetails { get; set; } = string.Empty;

    [JsonPropertyName("statName")]
    public string StatName { get; set; } = string.Empty;

    [JsonPropertyName("difficultyDescription")]
    public string DifficultyDescription { get; set; } = string.Empty;

    [JsonPropertyName("abilityScore")]
    public int AbilityScore { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }
}

/// <summary>
/// Result of a random decision operation
/// </summary>
public class RandomDecisionResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("selection")]
    public int Selection { get; set; }

    [JsonPropertyName("selectedOption")]
    public string SelectedOption { get; set; }

    [JsonPropertyName("totalOptions")]
    public int TotalOptions { get; set; }

    [JsonPropertyName("allOptions")]
    public List<string> AllOptions { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; }
}

/// <summary>
/// Result of a simple d20 roll
/// </summary>
public class SimpleD20Result
{
    [JsonPropertyName("roll")]
    public int Roll { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("isCriticalSuccess")]
    public bool IsCriticalSuccess { get; set; }

    [JsonPropertyName("isCriticalFailure")]
    public bool IsCriticalFailure { get; set; }
}

/// <summary>
/// Result of an advantage/disadvantage d20 roll
/// </summary>
public class AdvantageD20Result
{
    [JsonPropertyName("roll1")]
    public int Roll1 { get; set; }

    [JsonPropertyName("roll2")]
    public int Roll2 { get; set; }

    [JsonPropertyName("finalRoll")]
    public int FinalRoll { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("isAdvantage")]
    public bool IsAdvantage { get; set; }
}

#endregion

public interface IGameLogicService
{
    // Dice Rolling Methods
    Task<SimpleD20Result> RollD20Async();
    Task<AdvantageD20Result> RollD20WithAdvantageAsync();
    Task<AdvantageD20Result> RollD20WithDisadvantageAsync();
    Task<DiceRollResult> RollDiceAsync(int sides, int count = 1);
    Task<DiceRollResult> RollDiceAsync(string diceNotation);
    Task<SkillCheckResult> MakeSkillCheckAsync(string statName, int difficultyClass, bool advantage = false, bool disadvantage = false, int modifier = 0);
    Task<RandomDecisionResult> MakeRandomDecisionAsync(int numberOfOptions);
    Task<RandomDecisionResult> MakeRandomDecisionFromOptionsAsync(List<string> options);
    
    // Character Creation Dice Rolling
    Task<DiceRollResult> Roll4d6DropLowestAsync();

    // Helper Methods
    int CalculateAbilityModifier(int abilityScore);
    int GetAbilityScore(string statName);
    bool IsValidStatName(string statName);
    string GetDifficultyDescription(int difficultyClass);
}

/// <summary>
/// This service contains high-level game logic orchestration and rule enforcement
/// </summary>
public class GameLogicService : IGameLogicService
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly Random _random;

    public GameLogicService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
        _random = new Random();
    }

    #region Dice Rolling Methods

    public async Task<SimpleD20Result> RollD20Async()
    {
        await Task.Yield();
        
        var roll = _random.Next(1, 21);
        var result = new SimpleD20Result
        {
            Roll = roll,
            IsCriticalSuccess = roll == 20,
            IsCriticalFailure = roll == 1
        };

        result.Message = roll switch
        {
            20 => "Natural 20! Critical Success!",
            1 => "Natural 1! Critical Failure!",
            _ => $"Rolled {roll}"
        };

        return result;
    }

    public async Task<DiceRollResult> RollDiceAsync(string diceNotation)
    {
        await Task.Yield();
        
        try
        {
            // Parse dice notation like "1d8", "2d6", "3d4+2"
            var notation = diceNotation.ToLower().Trim();
            
            // Handle simple cases like "1d8"
            if (notation.Contains('d'))
            {
                var parts = notation.Split('d');
                if (parts.Length == 2)
                {
                    var count = int.Parse(parts[0]);
                    var sidesStr = parts[1];
                    
                    // Handle modifiers like "+2" or "-1"
                    var modifier = 0;
                    if (sidesStr.Contains('+'))
                    {
                        var modParts = sidesStr.Split('+');
                        sidesStr = modParts[0];
                        modifier = int.Parse(modParts[1]);
                    }
                    else if (sidesStr.Contains('-'))
                    {
                        var modParts = sidesStr.Split('-');
                        sidesStr = modParts[0];
                        modifier = -int.Parse(modParts[1]);
                    }
                    
                    var sides = int.Parse(sidesStr);
                    
                    // Roll the dice
                    var baseResult = await RollDiceAsync(sides, count);
                    if (baseResult.Success)
                    {
                        baseResult.Total += modifier;
                        baseResult.DiceNotation = diceNotation;
                        baseResult.Message = $"Rolled {diceNotation}: {string.Join(", ", baseResult.Rolls)}" + 
                                           (modifier != 0 ? $" {modifier:+#;-#;+0}" : "") + 
                                           $" = {baseResult.Total}";
                    }
                    return baseResult;
                }
            }
            
            return new DiceRollResult
            {
                Success = false,
                Error = $"Invalid dice notation: {diceNotation}",
                DiceNotation = diceNotation
            };
        }
        catch (Exception ex)
        {
            return new DiceRollResult
            {
                Success = false,
                Error = $"Error parsing dice notation '{diceNotation}': {ex.Message}",
                DiceNotation = diceNotation
            };
        }
    }

    public async Task<AdvantageD20Result> RollD20WithAdvantageAsync()
    {
        await Task.Yield();
        
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var finalRoll = Math.Max(roll1, roll2);

        return new AdvantageD20Result
        {
            Roll1 = roll1,
            Roll2 = roll2,
            FinalRoll = finalRoll,
            IsAdvantage = true,
            Message = $"Advantage roll: {roll1}, {roll2} -> Using {finalRoll}"
        };
    }

    public async Task<AdvantageD20Result> RollD20WithDisadvantageAsync()
    {
        await Task.Yield();
        
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var finalRoll = Math.Min(roll1, roll2);

        return new AdvantageD20Result
        {
            Roll1 = roll1,
            Roll2 = roll2,
            FinalRoll = finalRoll,
            IsAdvantage = false,
            Message = $"Disadvantage roll: {roll1}, {roll2} -> Using {finalRoll}"
        };
    }

    public async Task<DiceRollResult> RollDiceAsync(int sides, int count = 1)
    {
        await Task.Yield();
        
        if (count <= 0 || sides <= 0)
        {
            return new DiceRollResult
            {
                Success = false,
                Error = "Count and sides must be positive numbers"
            };
        }

        if (count > 100 || sides > 1000)
        {
            return new DiceRollResult
            {
                Success = false,
                Error = "Dice roll too large (max 100d1000)"
            };
        }

        var rolls = new List<int>();
        var total = 0;

        for (int i = 0; i < count; i++)
        {
            var roll = _random.Next(1, sides + 1);
            rolls.Add(roll);
            total += roll;
        }

        return new DiceRollResult
        {
            Success = true,
            Total = total,
            Rolls = rolls,
            DiceNotation = $"{count}d{sides}",
            Message = count == 1 ? $"Rolled {total}" : $"Rolled {count}d{sides}: {string.Join(", ", rolls)} = {total}"
        };
    }

    public async Task<SkillCheckResult> MakeSkillCheckAsync(string statName, int difficultyClass, bool advantage = false, bool disadvantage = false, int modifier = 0)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (gameState == null)
        {
            return new SkillCheckResult
            {
                Success = false,
                Error = "No game state found"
            };
        }

        // For now, use a default ability score of 10 (average) since player stats are now dynamic
        // In a full implementation, this would look up stats from RulesetGameData
        var abilityScore = 10; // Default average ability score
        var statModifier = CalculateAbilityModifier(abilityScore);

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

        return new SkillCheckResult
        {
            Success = success,
            TotalRoll = totalRoll,
            DiceRoll = diceRoll,
            StatModifier = statModifier,
            AdditionalModifier = modifier,
            DifficultyClass = difficultyClass,
            Margin = margin,
            Outcome = outcome,
            RollDetails = rollDetails,
            StatName = statName,
            DifficultyDescription = GetDifficultyDescription(difficultyClass),
            AbilityScore = abilityScore
        };
    }

    public async Task<RandomDecisionResult> MakeRandomDecisionAsync(int numberOfOptions)
    {
        await Task.Yield();
        
        if (numberOfOptions <= 0)
        {
            return new RandomDecisionResult
            {
                Success = false,
                Error = "Number of options must be positive"
            };
        }

        if (numberOfOptions > 100)
        {
            return new RandomDecisionResult
            {
                Success = false,
                Error = "Too many options (max 100)"
            };
        }

        var selection = _random.Next(1, numberOfOptions + 1);

        return new RandomDecisionResult
        {
            Success = true,
            Selection = selection,
            TotalOptions = numberOfOptions,
            Message = $"Randomly selected option {selection} out of {numberOfOptions}"
        };
    }

    public async Task<RandomDecisionResult> MakeRandomDecisionFromOptionsAsync(List<string> options)
    {
        await Task.Yield();
        
        if (options == null || options.Count == 0)
        {
            return new RandomDecisionResult
            {
                Success = false,
                Error = "Options list cannot be null or empty"
            };
        }

        if (options.Count > 100)
        {
            return new RandomDecisionResult
            {
                Success = false,
                Error = "Too many options (max 100)"
            };
        }

        var selectionIndex = _random.Next(0, options.Count);
        var selectedOption = options[selectionIndex];

        return new RandomDecisionResult
        {
            Success = true,
            Selection = selectionIndex + 1, // 1-based for user display
            SelectedOption = selectedOption,
            TotalOptions = options.Count,
            AllOptions = new List<string>(options),
            Message = $"Randomly selected: {selectedOption} (option {selectionIndex + 1} out of {options.Count})"
        };
    }

    public async Task<DiceRollResult> Roll4d6DropLowestAsync()
    {
        await Task.Yield();
        
        var rolls = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            rolls.Add(_random.Next(1, 7));
        }
        
        rolls.Sort();
        var droppedRoll = rolls[0];
        rolls.RemoveAt(0); // Remove the lowest
        var total = rolls.Sum();
        
        return new DiceRollResult
        {
            Success = true,
            Total = total,
            Rolls = rolls,
            DiceNotation = "4d6 drop lowest",
            Message = $"Rolled 4d6 drop lowest: {string.Join(", ", rolls)} (dropped {droppedRoll}) = {total}"
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Calculates the standard RPG ability modifier from an ability score
    /// Formula: floor((abilityScore - 10) / 2)
    /// </summary>
    public int CalculateAbilityModifier(int abilityScore)
    {
        return (int)Math.Floor((abilityScore - 10) / 2.0);
    }

    /// <summary>
    /// Gets the ability score for the specified stat name using default values
    /// In a full implementation, this would look up stats from RulesetGameData
    /// </summary>
    public int GetAbilityScore(string statName)
    {
        // Return default ability score of 10 for all stats during migration
        // In the final implementation, this would look up values from the active ruleset
        return 10;
    }

    /// <summary>
    /// Validates that the stat name is one of the six standard ability scores
    /// </summary>
    public bool IsValidStatName(string statName)
    {
        return statName.ToLower() is "strength" or "dexterity" or "constitution" or "intelligence" or "wisdom" or "charisma";
    }

    /// <summary>
    /// Gets a human-readable description of the difficulty class
    /// </summary>
    public string GetDifficultyDescription(int difficultyClass)
    {
        return difficultyClass switch
        {
            <= 5 => "Very Easy",
            <= 8 => "Easy", 
            <= 11 => "Medium",
            <= 14 => "Hard",
            <= 17 => "Very Hard",
            _ => "Nearly Impossible"
        };
    }

    #endregion
}
