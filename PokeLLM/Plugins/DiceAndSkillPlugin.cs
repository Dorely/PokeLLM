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
public class DiceAndSkillPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public DiceAndSkillPlugin(IGameStateRepository repository)
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
    [Description("Roll a single d20 die for skill checks, saves, and random events. Returns the raw roll result. Example: Use for initiative, ability checks, and determining random outcomes.")]
    public async Task<string> RollD20()
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollD20 called");
        
        var roll = _random.Next(1, 21);
        
        var result = new
        {
            diceType = "d20",
            roll = roll,
            isCriticalSuccess = roll == 20,
            isCriticalFailure = roll == 1,
            message = roll switch
            {
                20 => "Natural 20! Critical Success!",
                1 => "Natural 1! Critical Failure!",
                >= 15 => "High roll!",
                <= 5 => "Low roll...",
                _ => $"Rolled {roll}"
            }
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] d20 roll: {roll}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("roll_dice")]
    [Description("Roll multiple dice of a specified type for damage, complex checks, or random generation. Example: roll_dice(3, 6) for 3d6 damage or roll_dice(2, 10) for percentile-style rolls.")]
    public async Task<string> RollDice(
        [Description("Number of dice to roll")] int count,
        [Description("Number of sides on each die (4, 6, 8, 10, 12, 20, 100)")] int sides)
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollDice called: {count}d{sides}");
        
        if (count <= 0 || count > 20)
            return JsonSerializer.Serialize(new { error = "Dice count must be between 1 and 20" }, _jsonOptions);

        if (!new[] { 4, 6, 8, 10, 12, 20, 100 }.Contains(sides))
            return JsonSerializer.Serialize(new { error = "Invalid die type. Use 4, 6, 8, 10, 12, 20, or 100 sides" }, _jsonOptions);

        var rolls = new List<int>();
        var total = 0;

        for (int i = 0; i < count; i++)
        {
            var roll = _random.Next(1, sides + 1);
            rolls.Add(roll);
            total += roll;
        }

        var result = new
        {
            diceExpression = $"{count}d{sides}",
            individualRolls = rolls,
            total = total,
            average = (double)total / count,
            minimum = count,
            maximum = count * sides,
            message = $"Rolled {count}d{sides}: {string.Join(" + ", rolls)} = {total}"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] {count}d{sides} roll: {total} ({string.Join(",", rolls)})");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("roll_with_advantage")]
    [Description("Roll 2d20 and take the higher result (advantage mechanic). Used for favorable conditions, assistance, or bonuses. Example: When Pokemon has type advantage or trainer has expert knowledge.")]
    public async Task<string> RollWithAdvantage()
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollWithAdvantage called");
        
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var finalRoll = Math.Max(roll1, roll2);

        var result = new
        {
            diceType = "2d20 (advantage)",
            roll1 = roll1,
            roll2 = roll2,
            finalRoll = finalRoll,
            discardedRoll = Math.Min(roll1, roll2),
            isCriticalSuccess = finalRoll == 20,
            isCriticalFailure = finalRoll == 1,
            message = $"Advantage roll: {roll1}, {roll2} -> Using {finalRoll}"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Advantage roll: {roll1}, {roll2} -> {finalRoll}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("roll_with_disadvantage")]
    [Description("Roll 2d20 and take the lower result (disadvantage mechanic). Used for unfavorable conditions, penalties, or hindrances. Example: When exhausted, injured, or facing type disadvantage.")]
    public async Task<string> RollWithDisadvantage()
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollWithDisadvantage called");
        
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var finalRoll = Math.Min(roll1, roll2);

        var result = new
        {
            diceType = "2d20 (disadvantage)",
            roll1 = roll1,
            roll2 = roll2,
            finalRoll = finalRoll,
            discardedRoll = Math.Max(roll1, roll2),
            isCriticalSuccess = finalRoll == 20,
            isCriticalFailure = finalRoll == 1,
            message = $"Disadvantage roll: {roll1}, {roll2} -> Using {finalRoll}"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Disadvantage roll: {roll1}, {roll2} -> {finalRoll}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Skill Checks

    [KernelFunction("make_skill_check")]
    [Description("Make a skill check using trainer stats with difficulty class and modifiers. Core mechanic for resolving actions. Example: make_skill_check('Mind', 15, false) for solving a puzzle or make_skill_check('Charm', 12, true) for persuading with advantage.")]
    public async Task<string> MakeSkillCheck(
        [Description("Stat to use: Power, Speed, Mind, Charm, Defense, Spirit")] string statName,
        [Description("Difficulty Class (5=Very Easy, 8=Easy, 11=Medium, 14=Hard, 17=Very Hard, 20=Nearly Impossible)")] int difficultyClass,
        [Description("Whether the check has advantage (roll twice, take higher)")] bool advantage = false,
        [Description("Whether the check has disadvantage (roll twice, take lower)")] bool disadvantage = false,
        [Description("Additional modifier to add to the roll")] int modifier = 0)
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] MakeSkillCheck called: stat={statName}, DC={difficultyClass}, adv={advantage}, dis={disadvantage}");
        
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
            outcome = margin >= 10 ? "Great Success!" :
                     margin >= 5 ? "Good Success!" : "Success!";
        }
        else
        {
            outcome = margin <= -10 ? "Critical Failure!" :
                     margin <= -5 ? "Bad Failure!" : "Failure!";
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

        var result = new
        {
            success = success,
            stat = statName,
            statLevel = statLevel.ToString(),
            statModifier = statModifier,
            diceRoll = diceRoll,
            rollDetails = rollDetails,
            additionalModifier = modifier,
            totalRoll = totalRoll,
            difficultyClass = difficultyClass,
            difficultyDescription = difficultyDescription,
            margin = margin,
            outcome = outcome,
            isCriticalSuccess = diceRoll == 20,
            isCriticalFailure = diceRoll == 1,
            hasAdvantage = advantage && !disadvantage,
            hasDisadvantage = disadvantage && !advantage,
            calculation = $"{diceRoll} + {statModifier} ({statName}) + {modifier} = {totalRoll} vs DC {difficultyClass}",
            message = $"{outcome} {statName} check: {totalRoll} vs DC {difficultyClass} ({margin:+#;-#;0})"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Skill check: {statName} {totalRoll} vs DC {difficultyClass} = {(success ? "SUCCESS" : "FAILURE")}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("make_opposed_check")]
    [Description("Make an opposed skill check between two participants (trainer vs NPC, Pokemon vs Pokemon). Example: make_opposed_check('Charm', 'Mind', 'Trainer', 'NPC') for social contests or persuasion vs insight.")]
    public async Task<string> MakeOpposedCheck(
        [Description("First participant's stat (Power, Speed, Mind, Charm, Defense, Spirit)")] string stat1,
        [Description("Second participant's stat (Power, Speed, Mind, Charm, Defense, Spirit)")] string stat2,
        [Description("Description of first participant")] string participant1 = "Trainer",
        [Description("Description of second participant")] string participant2 = "Opponent",
        [Description("Modifier for first participant")] int modifier1 = 0,
        [Description("Modifier for second participant")] int modifier2 = 0)
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] MakeOpposedCheck called: {stat1} vs {stat2}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Validate stat names
        if (!IsValidStatName(stat1) || !IsValidStatName(stat2))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Power, Speed, Mind, Charm, Defense, or Spirit" }, _jsonOptions);

        // Get stat modifiers (assume participant 1 is the trainer, participant 2 uses average stats)
        var stat1Level = GetStatLevel(gameState.Player.Character.Stats, stat1);
        var stat1Modifier = (int)stat1Level;
        
        // For opponent, use a reasonable default based on context
        var stat2Modifier = 0; // Baseline human/NPC stats

        // Roll for both participants
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);

        var total1 = roll1 + stat1Modifier + modifier1;
        var total2 = roll2 + stat2Modifier + modifier2;

        var winner = total1 > total2 ? participant1 : 
                    total2 > total1 ? participant2 : "Tie";
        var margin = Math.Abs(total1 - total2);

        var result = new
        {
            participant1 = new
            {
                name = participant1,
                stat = stat1,
                statModifier = stat1Modifier,
                roll = roll1,
                modifier = modifier1,
                total = total1
            },
            participant2 = new
            {
                name = participant2,
                stat = stat2,
                statModifier = stat2Modifier,
                roll = roll2,
                modifier = modifier2,
                total = total2
            },
            winner = winner,
            margin = margin,
            tie = total1 == total2,
            message = winner == "Tie" ? 
                $"Tie! Both rolled {total1}" :
                $"{winner} wins! ({(winner == participant1 ? total1 : total2)} vs {(winner == participant1 ? total2 : total1)})"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Opposed check: {participant1} {total1} vs {participant2} {total2} = {winner}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("make_saving_throw")]
    [Description("Make a saving throw to resist effects, avoid damage, or overcome challenges. Uses trainer stats with specific difficulty. Example: make_saving_throw('Spirit', 18) to resist psychic effects or make_saving_throw('Defense', 15) to avoid environmental damage.")]
    public async Task<string> MakeSavingThrow(
        [Description("Stat to use for the save: Power, Speed, Mind, Charm, Defense, Spirit")] string statName,
        [Description("Difficulty Class to meet or exceed")] int difficultyClass,
        [Description("Additional modifier to the save")] int modifier = 0,
        [Description("Description of what is being saved against")] string saveDescription = "")
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] MakeSavingThrow called: stat={statName}, DC={difficultyClass}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Validate stat name
        if (!IsValidStatName(statName))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Power, Speed, Mind, Charm, Defense, or Spirit" }, _jsonOptions);

        // Get stat modifier
        var statLevel = GetStatLevel(gameState.Player.Character.Stats, statName);
        var statModifier = (int)statLevel;

        // Check for conditions that might affect saves
        var conditionModifier = 0;
        foreach (var condition in gameState.Player.Character.Conditions)
        {
            switch (condition.Type)
            {
                case TrainerCondition.Inspired:
                    conditionModifier += 2;
                    break;
                case TrainerCondition.Tired:
                    conditionModifier -= 1;
                    break;
                case TrainerCondition.Exhausted:
                    conditionModifier -= 2;
                    break;
                case TrainerCondition.Poisoned:
                    conditionModifier -= 1;
                    break;
                case TrainerCondition.Injured:
                    conditionModifier -= 1;
                    break;
            }
        }

        var diceRoll = _random.Next(1, 21);
        var totalRoll = diceRoll + statModifier + modifier + conditionModifier;
        var success = totalRoll >= difficultyClass;
        var margin = totalRoll - difficultyClass;

        string outcome;
        if (diceRoll == 20)
            outcome = "Critical Success!";
        else if (diceRoll == 1)
            outcome = "Critical Failure!";
        else if (success)
            outcome = margin >= 10 ? "Great Save!" : "Successful Save!";
        else
            outcome = margin <= -10 ? "Critical Failure!" : "Failed Save!";

        var result = new
        {
            success = success,
            stat = statName,
            statLevel = statLevel.ToString(),
            statModifier = statModifier,
            diceRoll = diceRoll,
            modifier = modifier,
            conditionModifier = conditionModifier,
            totalRoll = totalRoll,
            difficultyClass = difficultyClass,
            margin = margin,
            outcome = outcome,
            saveDescription = saveDescription,
            isCriticalSuccess = diceRoll == 20,
            isCriticalFailure = diceRoll == 1,
            calculation = $"{diceRoll} + {statModifier} ({statName}) + {modifier} + {conditionModifier} (conditions) = {totalRoll} vs DC {difficultyClass}",
            message = $"{outcome} {statName} save: {totalRoll} vs DC {difficultyClass}. {saveDescription}"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Saving throw: {statName} {totalRoll} vs DC {difficultyClass} = {(success ? "SUCCESS" : "FAILURE")}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("roll_initiative")]
    [Description("Roll initiative for turn order in combat or time-sensitive situations. Uses Speed stat + d20. Example: Use at start of Pokemon battles or when timing matters in encounters.")]
    public async Task<string> RollInitiative()
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollInitiative called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var speedModifier = (int)gameState.Player.Character.Stats.Speed.Level;
        var diceRoll = _random.Next(1, 21);
        var totalInitiative = diceRoll + speedModifier;

        var result = new
        {
            diceRoll = diceRoll,
            speedModifier = speedModifier,
            totalInitiative = totalInitiative,
            speedLevel = gameState.Player.Character.Stats.Speed.Level.ToString(),
            calculation = $"{diceRoll} + {speedModifier} (Speed) = {totalInitiative}",
            message = $"Initiative: {totalInitiative} (rolled {diceRoll} + {speedModifier} Speed)"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Initiative: {totalInitiative} ({diceRoll} + {speedModifier})");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Random Generation

    [KernelFunction("roll_percentile")]
    [Description("Roll percentile dice (d100) for random tables, encounter chances, or probability events. Example: Use for determining rare Pokemon encounters, weather changes, or random events.")]
    public async Task<string> RollPercentile()
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollPercentile called");
        
        var roll = _random.Next(1, 101);
        
        var result = new
        {
            roll = roll,
            percentage = $"{roll}%",
            category = roll switch
            {
                <= 1 => "Extremely Rare (1%)",
                <= 5 => "Very Rare (2-5%)",
                <= 10 => "Rare (6-10%)",
                <= 25 => "Uncommon (11-25%)",
                <= 75 => "Common (26-75%)",
                <= 95 => "Very Common (76-95%)",
                _ => "Almost Certain (96-100%)"
            },
            message = $"Rolled {roll}% on d100"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Percentile roll: {roll}%");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("roll_random_encounter")]
    [Description("Roll for random encounters or events with customizable probability. Returns whether an encounter occurs and its intensity. Example: Use for wilderness travel, city exploration, or timed events.")]
    public async Task<string> RollRandomEncounter(
        [Description("Base chance for encounter (1-100, where 100 = always occurs)")] int baseChance = 25,
        [Description("Modifier to the base chance based on circumstances")] int modifier = 0)
    {
        Debug.WriteLine($"[DiceAndSkillPlugin] RollRandomEncounter called: chance={baseChance}, modifier={modifier}");
        
        var adjustedChance = Math.Max(1, Math.Min(100, baseChance + modifier));
        var roll = _random.Next(1, 101);
        var encounterOccurs = roll <= adjustedChance;
        
        // If encounter occurs, determine intensity
        var intensity = "";
        var intensityRoll = 0;
        if (encounterOccurs)
        {
            intensityRoll = _random.Next(1, 21);
            intensity = intensityRoll switch
            {
                <= 5 => "Minor",
                <= 10 => "Moderate", 
                <= 15 => "Significant",
                <= 18 => "Major",
                _ => "Extraordinary"
            };
        }

        var result = new
        {
            encounterOccurs = encounterOccurs,
            roll = roll,
            adjustedChance = adjustedChance,
            baseChance = baseChance,
            modifier = modifier,
            intensity = intensity,
            intensityRoll = intensityRoll,
            message = encounterOccurs ? 
                $"Encounter! Rolled {roll} <= {adjustedChance}% - {intensity} intensity ({intensityRoll}/20)" :
                $"No encounter. Rolled {roll} > {adjustedChance}%"
        };

        Debug.WriteLine($"[DiceAndSkillPlugin] Random encounter: {roll} vs {adjustedChance}% = {(encounterOccurs ? "YES" : "NO")}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Helper Methods

    private StatLevel GetStatLevel(Stats stats, string statName)
    {
        return statName.ToLower() switch
        {
            "power" => stats.Power.Level,
            "speed" => stats.Speed.Level,
            "mind" => stats.Mind.Level,
            "charm" => stats.Charm.Level,
            "defense" => stats.Defense.Level,
            "spirit" => stats.Spirit.Level,
            _ => StatLevel.Novice
        };
    }

    private bool IsValidStatName(string statName)
    {
        return statName.ToLower() is "power" or "speed" or "mind" or "charm" or "defense" or "spirit";
    }

    #endregion
}