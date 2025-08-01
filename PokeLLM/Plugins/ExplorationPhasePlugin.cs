using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;

namespace PokeLLM.Game.Plugins;

public class ExplorationPhasePlugin
{
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExplorationPhasePlugin(IGameLogicService gameLogicService)
    {
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("roll_d20")]
    [Description("Rolls a standard d20 die and returns the result")]
    public async Task<string> RollD20()
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] RollD20 called");
        
        var result = await _gameLogicService.RollD20Async();
        
        Debug.WriteLine($"[ExplorationPhasePlugin] d20 roll: {result.Roll}");
        return result.Message;
    }

    [KernelFunction("roll_d20_with_advantage")]
    [Description("Rolls two d20s and takes the higher result (advantage)")]
    public async Task<string> RollD20WithAdvantage()
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] RollD20WithAdvantage called");
        
        var result = await _gameLogicService.RollD20WithAdvantageAsync();
        
        Debug.WriteLine($"[ExplorationPhasePlugin] Advantage roll: {result.Roll1}, {result.Roll2} -> {result.FinalRoll}");
        return result.Message;
    }

    [KernelFunction("roll_d20_with_disadvantage")]
    [Description("Rolls two d20s and takes the lower result (disadvantage)")]
    public async Task<string> RollWithDisadvantage()
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] RollWithDisadvantage called");
        
        var result = await _gameLogicService.RollD20WithDisadvantageAsync();
        
        Debug.WriteLine($"[ExplorationPhasePlugin] Disadvantage roll: {result.Roll1}, {result.Roll2} -> {result.FinalRoll}");
        return result.Message;
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
        Debug.WriteLine($"[ExplorationPhasePlugin] MakeSkillCheck called: stat={statName}, DC={difficultyClass}, adv={advantage}, dis={disadvantage}");
        
        var result = await _gameLogicService.MakeSkillCheckAsync(statName, difficultyClass, advantage, disadvantage, modifier);
        
        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Skill check error: {result.Error}");
            return JsonSerializer.Serialize(new { error = result.Error }, _jsonOptions);
        }

        var formattedResult = $"{result.Outcome} {result.StatName} check: {result.TotalRoll} vs DC {result.DifficultyClass} ({result.Margin:+#;-#;0})";
        
        Debug.WriteLine($"[ExplorationPhasePlugin] Skill check: {result.StatName} {result.TotalRoll} vs DC {result.DifficultyClass} = {(result.Success ? "SUCCESS" : "FAILURE")}");
        return formattedResult;
    }

    [KernelFunction("dice_roll")]
    [Description("Rolls dice with the specified number of sides and count. Used for procedural generation and damage calculations.")]
    public async Task<string> DiceRoll(
        [Description("Number of sides on each die (e.g., 6 for d6, 20 for d20)")] int sides,
        [Description("Number of dice to roll (default 1)")] int count = 1)
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] DiceRoll called: {count}d{sides}");
        
        var result = await _gameLogicService.RollDiceAsync(sides, count);
        
        if (!result.Success)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Dice roll error: {result.Error}");
            return JsonSerializer.Serialize(new { error = result.Error }, _jsonOptions);
        }

        Debug.WriteLine($"[ExplorationPhasePlugin] {count}d{sides} result: {result.Total}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("decide_random_from_options")]
    [Description("Randomly selects from a list of options. Used for procedural event generation in exploration")]
    public async Task<string> DecideRandomFromOptions(
        [Description("list of options to choose from, e.g., [\"Option A\", \"Option B\", \"Option C\"]")]
            List<string> options)
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] DecideRandomFromOptions called");
        
        try
        {
            if (options == null)
            {
                var errorResult = new { error = "Failed to parse options JSON" };
                return JsonSerializer.Serialize(errorResult, _jsonOptions);
            }

            var result = await _gameLogicService.MakeRandomDecisionFromOptionsAsync(options);
            
            if (!result.Success)
            {
                Debug.WriteLine($"[ExplorationPhasePlugin] Random decision from options error: {result.Error}");
                return JsonSerializer.Serialize(new { error = result.Error }, _jsonOptions);
            }

            Debug.WriteLine($"[ExplorationPhasePlugin] Random selection from options: {result.SelectedOption}");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] JSON parsing error: {ex.Message}");
            var errorResult = new { error = $"Invalid JSON format for options: {ex.Message}" };
            return JsonSerializer.Serialize(errorResult, _jsonOptions);
        }
    }
}