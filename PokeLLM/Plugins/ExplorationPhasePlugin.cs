using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

public class ExplorationPhasePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public ExplorationPhasePlugin(IGameStateRepository repository)
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
        [Description("Stat to use: Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma")] string statName,
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
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Strength, Dexterity, Constitution, Intelligence, Wisdom, or Charisma" }, _jsonOptions);

        // Get stat modifier using D&D 5e rules
        var abilityScore = GetAbilityScore(gameState.Player.Stats, statName);
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

    /// <summary>
    /// Gets the ability score for the specified stat name using D&D 5e ability scores
    /// </summary>
    private int GetAbilityScore(Stats stats, string statName)
    {
        return statName.ToLower() switch
        {
            "strength" => stats.Strength,
            "dexterity" => stats.Dexterity,
            "constitution" => stats.Constitution,
            "intelligence" => stats.Intelligence,
            "wisdom" => stats.Wisdom,
            "charisma" => stats.Charisma,
            _ => 10 // Default to 10 (average) if invalid stat name
        };
    }

    /// <summary>
    /// Calculates the D&D 5e ability modifier from an ability score
    /// Formula: floor((abilityScore - 10) / 2)
    /// </summary>
    private int CalculateAbilityModifier(int abilityScore)
    {
        return (int)Math.Floor((abilityScore - 10) / 2.0);
    }

    /// <summary>
    /// Validates that the stat name is one of the six D&D 5e ability scores
    /// </summary>
    private bool IsValidStatName(string statName)
    {
        return statName.ToLower() is "strength" or "dexterity" or "constitution" or "intelligence" or "wisdom" or "charisma";
    }

    #endregion

    #region Exploration Phase Functions


    [KernelFunction("dice_roll")]
    [Description("Rolls dice with the specified number of sides and count. Used for procedural generation and damage calculations.")]
    public async Task<string> DiceRoll(
        [Description("Number of sides on each die (e.g., 6 for d6, 20 for d20)")]
            int sides,
        [Description("Number of dice to roll (default 1)")]
            int count = 1)
    {
        Debug.WriteLine($"[DicePlugin] DiceRoll called: {count}d{sides}");
        
        if (count <= 0 || sides <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Count and sides must be positive numbers" }, _jsonOptions);
        }

        if (count > 100 || sides > 1000)
        {
            return JsonSerializer.Serialize(new { error = "Dice roll too large (max 100d1000)" }, _jsonOptions);
        }

        var rolls = new List<int>();
        var total = 0;

        for (int i = 0; i < count; i++)
        {
            var roll = _random.Next(1, sides + 1);
            rolls.Add(roll);
            total += roll;
        }

        var result = JsonSerializer.Serialize(new {
            success = true,
            total = total,
            rolls = rolls,
            diceNotation = $"{count}d{sides}",
            message = count == 1 ? $"Rolled {total}" : $"Rolled {count}d{sides}: {string.Join(", ", rolls)} = {total}"
        }, _jsonOptions);

        Debug.WriteLine($"[DicePlugin] {count}d{sides} result: {total}");
        return result;
    }

    [KernelFunction("decide_random")]
    [Description("Randomly selects from a number of options. Useful for procedural event generation in exploration.")]
    public async Task<string> DecideRandom(
        [Description("Number of options to choose from (e.g., 5 to choose from options 1-5)")]
            int numberOfOptions)
    {
//TODO make this take a string list of options and return one randomly, instead of just returning a random number
        Debug.WriteLine($"[DicePlugin] DecideRandom called with {numberOfOptions} options");
        
        if (numberOfOptions <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Number of options must be positive" }, _jsonOptions);
        }

        if (numberOfOptions > 100)
        {
            return JsonSerializer.Serialize(new { error = "Too many options (max 100)" }, _jsonOptions);
        }

        var selection = _random.Next(1, numberOfOptions + 1);

        var result = JsonSerializer.Serialize(new {
            success = true,
            selection = selection,
            totalOptions = numberOfOptions,
            message = $"Randomly selected option {selection} out of {numberOfOptions}"
        }, _jsonOptions);

        Debug.WriteLine($"[DicePlugin] Random selection: {selection} out of {numberOfOptions}");
        return result;
    }

    #endregion
}