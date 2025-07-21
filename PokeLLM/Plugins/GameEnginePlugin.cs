using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

public class GameEnginePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public GameEnginePlugin(IGameStateRepository repository)
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

    #region Dice Rolling System

    [KernelFunction("roll_d20")]
    [Description("Roll a d20 die and return the result. Used for skill checks and other random events.")]
    public async Task<string> RollD20()
    {
        await Task.Yield();
        var roll = _random.Next(1, 21);
        Debug.WriteLine($"[GameEnginePlugin] RollD20 result: {roll}");
        return JsonSerializer.Serialize(new { roll = roll, type = "d20" }, _jsonOptions);
    }

    [KernelFunction("roll_dice")]
    [Description("Roll multiple dice of specified type and return results.")]
    public async Task<string> RollDice(
        [Description("Number of dice to roll")] int count,
        [Description("Number of sides on each die (6, 8, 10, 12, 20, 100)")] int sides)
    {
        await Task.Yield();
        var rolls = new List<int>();
        for (int i = 0; i < count; i++)
        {
            rolls.Add(_random.Next(1, sides + 1));
        }
        
        var total = rolls.Sum();
        var result = new { rolls = rolls, total = total, count = count, sides = sides };
        
        Debug.WriteLine($"[GameEnginePlugin] RollDice {count}d{sides} result: {total} (rolls: [{string.Join(", ", rolls)}])");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("roll_with_advantage")]
    [Description("Roll two d20s and take the higher result (advantage mechanic).")]
    public async Task<string> RollWithAdvantage()
    {
        await Task.Yield();
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var result = Math.Max(roll1, roll2);
        
        var rollResult = new { 
            roll1 = roll1, 
            roll2 = roll2, 
            result = result, 
            type = "advantage" 
        };
        
        Debug.WriteLine($"[GameEnginePlugin] RollWithAdvantage: {roll1}, {roll2} -> {result}");
        return JsonSerializer.Serialize(rollResult, _jsonOptions);
    }

    [KernelFunction("roll_with_disadvantage")]
    [Description("Roll two d20s and take the lower result (disadvantage mechanic).")]
    public async Task<string> RollWithDisadvantage()
    {
        await Task.Yield();
        var roll1 = _random.Next(1, 21);
        var roll2 = _random.Next(1, 21);
        var result = Math.Min(roll1, roll2);
        
        var rollResult = new { 
            roll1 = roll1, 
            roll2 = roll2, 
            result = result, 
            type = "disadvantage" 
        };
        
        Debug.WriteLine($"[GameEnginePlugin] RollWithDisadvantage: {roll1}, {roll2} -> {result}");
        return JsonSerializer.Serialize(rollResult, _jsonOptions);
    }

    #endregion

    #region Skill Check System

    [KernelFunction("make_skill_check")]
    [Description("Perform a skill check against a difficulty class (DC) using trainer stats.")]
    public async Task<string> MakeSkillCheck(
        [Description("The stat to use (Strength, Agility, Social, Intelligence)")] string statName,
        [Description("Difficulty Class (5=Very Easy, 8=Easy, 11=Medium, 14=Hard, 19=Very Hard)")] int difficultyClass,
        [Description("Use advantage (true) or disadvantage (false), null for normal roll")] bool? advantage = null)
    {
        Debug.WriteLine($"[GameEnginePlugin] MakeSkillCheck called with stat: {statName}, DC: {difficultyClass}, advantage: {advantage}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { success = false, error = "No game state found" }, _jsonOptions);

        // Get stat modifier
        var statLevel = GetStatLevel(gameState.Trainer.Stats, statName);
        var modifier = GetStatModifier(statLevel);

        // Roll the dice
        int roll;
        string rollType;
        if (advantage == true)
        {
            var roll1 = _random.Next(1, 21);
            var roll2 = _random.Next(1, 21);
            roll = Math.Max(roll1, roll2);
            rollType = $"advantage ({roll1}, {roll2})";
        }
        else if (advantage == false)
        {
            var roll1 = _random.Next(1, 21);
            var roll2 = _random.Next(1, 21);
            roll = Math.Min(roll1, roll2);
            rollType = $"disadvantage ({roll1}, {roll2})";
        }
        else
        {
            roll = _random.Next(1, 21);
            rollType = "normal";
        }

        var total = roll + modifier;
        var success = total >= difficultyClass;

        // Apply condition modifiers
        var conditionModifier = GetConditionModifier(gameState.Trainer.Conditions, statName);
        var finalTotal = total + conditionModifier;
        var finalSuccess = finalTotal >= difficultyClass;

        var result = new
        {
            success = finalSuccess,
            roll = roll,
            rollType = rollType,
            modifier = modifier,
            conditionModifier = conditionModifier,
            total = finalTotal,
            difficultyClass = difficultyClass,
            statName = statName,
            statLevel = statLevel.ToString(),
            margin = finalTotal - difficultyClass
        };

        Debug.WriteLine($"[GameEnginePlugin] Skill check result: {statName} {finalTotal} vs DC {difficultyClass} = {(finalSuccess ? "SUCCESS" : "FAILURE")}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_stat_modifier")]
    [Description("Get the modifier for a stat level (-2 to +7).")]
    public async Task<string> GetStatModifierForLevel(
        [Description("Stat level (-2 to 7)")] int statLevel)
    {
        await Task.Yield();
        var modifier = GetStatModifier((StatLevel)statLevel);
        return JsonSerializer.Serialize(new { statLevel = statLevel, modifier = modifier }, _jsonOptions);
    }

    #endregion

    #region Experience and Level Management

    [KernelFunction("calculate_experience_needed")]
    [Description("Calculate experience needed for the next level.")]
    public async Task<string> CalculateExperienceNeeded(
        [Description("Current level")] int currentLevel)
    {
        await Task.Yield();
        var neededForNext = CalculateExperienceForLevel(currentLevel + 1);
        var neededForCurrent = CalculateExperienceForLevel(currentLevel);
        var expNeeded = neededForNext - neededForCurrent;
        
        var result = new
        {
            currentLevel = currentLevel,
            experienceForCurrentLevel = neededForCurrent,
            experienceForNextLevel = neededForNext,
            experienceNeeded = expNeeded
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("award_experience")]
    [Description("Award experience based on challenge difficulty and circumstances. Level-ups require separate stat choice.")]
    public async Task<string> AwardExperience(
        [Description("Base experience amount")] int baseExperience,
        [Description("Difficulty modifier (0.5=Easy, 1.0=Normal, 1.5=Hard, 2.0=Extreme)")] double difficultyModifier = 1.0,
        [Description("Bonus for creative or exceptional play")] int creativityBonus = 0,
        [Description("Reason for the experience award")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] AwardExperience called: base={baseExperience}, modifier={difficultyModifier}, bonus={creativityBonus}");
        
        var totalExperience = (int)(baseExperience * difficultyModifier) + creativityBonus;
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var oldLevel = gameState.Trainer.Level;
        var oldExperience = gameState.Trainer.Experience;
        
        // Add experience but don't auto-level up
        gameState.Trainer.Experience += totalExperience;
        
        // Calculate what level they should be at based on experience
        var expectedLevel = CalculateLevelFromExperience(gameState.Trainer.Experience);
        var pendingLevelUps = Math.Max(0, expectedLevel - oldLevel);
        
        await _repository.SaveStateAsync(gameState);
        
        var result = new
        {
            experienceAwarded = totalExperience,
            baseExperience = baseExperience,
            difficultyModifier = difficultyModifier,
            creativityBonus = creativityBonus,
            reason = reason,
            oldExperience = oldExperience,
            newExperience = gameState.Trainer.Experience,
            currentLevel = oldLevel,
            expectedLevel = expectedLevel,
            pendingLevelUps = pendingLevelUps,
            levelUpAvailable = pendingLevelUps > 0,
            message = pendingLevelUps > 0 ? 
                $"Gained {totalExperience} XP! {pendingLevelUps} level-up(s) available - choose stats to increase!" :
                $"Gained {totalExperience} XP!"
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Character Creation

    [KernelFunction("generate_starting_stats")]
    [Description("Generate deterministic starting stats for a new trainer based on archetype.")]
    public async Task<string> GenerateStartingStats(
        [Description("Trainer archetype to determine stats")] string archetype = "None")
    {
        await Task.Yield();
        
        // Base stats (all start at Novice = 0)
        var stats = new Dictionary<string, int>
        {
            ["Strength"] = 0,
            ["Agility"] = 0,
            ["Social"] = 0,
            ["Intelligence"] = 0
        };

        // Apply archetype bonuses (deterministic, no randomness)
        if (Enum.TryParse<TrainerArchetype>(archetype, true, out var archetypeEnum))
        {
            ApplyArchetypeStatBonuses(stats, archetypeEnum);
        }

        var result = new
        {
            strength = (StatLevel)stats["Strength"],
            agility = (StatLevel)stats["Agility"],
            social = (StatLevel)stats["Social"],
            intelligence = (StatLevel)stats["Intelligence"],
            archetype = archetype,
            note = "Starting stats are deterministic based on archetype. Additional points awarded at level up."
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Generated starting stats for {archetype}: {JsonSerializer.Serialize(result, _jsonOptions)}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("create_custom_trainer")]
    [Description("Create a new trainer with custom stats and archetype.")]
    public async Task<string> CreateCustomTrainer(
        [Description("Trainer name")] string name,
        [Description("Strength stat level (0-2 for starting)")] int strength = 0,
        [Description("Agility stat level (0-2 for starting)")] int agility = 0,
        [Description("Social stat level (0-2 for starting)")] int social = 0,
        [Description("Intelligence stat level (0-2 for starting)")] int intelligence = 0,
        [Description("Trainer archetype")] string archetype = "None")
    {
        Debug.WriteLine($"[GameEnginePlugin] CreateCustomTrainer called for {name}");
        
        // Validate stat levels
        if (strength < 0 || strength > 2 || agility < 0 || agility > 2 || 
            social < 0 || social > 2 || intelligence < 0 || intelligence > 2)
        {
            return JsonSerializer.Serialize(new { error = "Starting stats must be between 0-2" }, _jsonOptions);
        }

        // Validate total stat points (max based on archetype bonuses)
        var archetypeBonus = 0;
        if (Enum.TryParse<TrainerArchetype>(archetype, true, out var archetypeEnum))
        {
            archetypeBonus = GetArchetypeTotalBonus(archetypeEnum);
        }
        
        var totalStats = strength + agility + social + intelligence;
        var maxAllowed = archetypeBonus + 2; // Base archetype bonus + 2 additional points
        
        if (totalStats > maxAllowed)
        {
            return JsonSerializer.Serialize(new { error = $"Total stat points cannot exceed {maxAllowed} for {archetype} archetype" }, _jsonOptions);
        }

        if (!Enum.TryParse<TrainerArchetype>(archetype, true, out archetypeEnum))
        {
            return JsonSerializer.Serialize(new { error = "Invalid trainer archetype" }, _jsonOptions);
        }

        // Create trainer data
        var trainerData = new
        {
            name = name,
            stats = new
            {
                strength = (StatLevel)strength,
                agility = (StatLevel)agility,
                social = (StatLevel)social,
                intelligence = (StatLevel)intelligence
            },
            archetype = archetypeEnum,
            level = 1,
            experience = 0,
            money = GetStartingMoney(archetypeEnum),
            startingItems = GetStartingItems(archetypeEnum)
        };

        return JsonSerializer.Serialize(trainerData, _jsonOptions);
    }

    #endregion

    #region Level Up System

    [KernelFunction("check_pending_level_ups")]
    [Description("Check if the trainer has pending level-ups that need stat choices.")]
    public async Task<string> CheckPendingLevelUps()
    {
        Debug.WriteLine($"[GameEnginePlugin] CheckPendingLevelUps called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var currentLevel = gameState.Trainer.Level;
        var experience = gameState.Trainer.Experience;
        var expectedLevel = CalculateLevelFromExperience(experience);
        
        var pendingLevels = Math.Max(0, expectedLevel - currentLevel);
        
        var result = new
        {
            currentLevel = currentLevel,
            expectedLevel = expectedLevel,
            pendingLevelUps = pendingLevels,
            currentExperience = experience,
            hasPendingLevelUps = pendingLevels > 0,
            currentStats = new
            {
                strength = gameState.Trainer.Stats.Strength.ToString(),
                agility = gameState.Trainer.Stats.Agility.ToString(),
                social = gameState.Trainer.Stats.Social.ToString(),
                intelligence = gameState.Trainer.Stats.Intelligence.ToString()
            }
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("apply_level_up")]
    [Description("Apply a level up by increasing a chosen stat. Must be called once for each pending level.")]
    public async Task<string> ApplyLevelUp(
        [Description("The stat to increase (Strength, Agility, Social, Intelligence)")] string statToIncrease)
    {
        Debug.WriteLine($"[GameEnginePlugin] ApplyLevelUp called with stat: {statToIncrease}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var currentLevel = gameState.Trainer.Level;
        var experience = gameState.Trainer.Experience;
        var expectedLevel = CalculateLevelFromExperience(experience);
        
        if (currentLevel >= expectedLevel)
        {
            return JsonSerializer.Serialize(new { error = "No pending level ups available" }, _jsonOptions);
        }

        // Validate stat name
        if (!IsValidStatName(statToIncrease))
        {
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Strength, Agility, Social, or Intelligence" }, _jsonOptions);
        }

        // Get current stat level
        var currentStatLevel = GetStatLevel(gameState.Trainer.Stats, statToIncrease);
        
        // Check if stat can be increased (max is Legendary = 7)
        if (currentStatLevel >= StatLevel.Legendary)
        {
            return JsonSerializer.Serialize(new { error = $"{statToIncrease} is already at maximum level (Legendary)" }, _jsonOptions);
        }

        // Increase the stat
        var newStatLevel = (StatLevel)((int)currentStatLevel + 1);
        await _repository.UpdateTrainerAsync(trainer =>
        {
            switch (statToIncrease.ToLower())
            {
                case "strength":
                    trainer.Stats.Strength = newStatLevel;
                    break;
                case "agility":
                    trainer.Stats.Agility = newStatLevel;
                    break;
                case "social":
                    trainer.Stats.Social = newStatLevel;
                    break;
                case "intelligence":
                    trainer.Stats.Intelligence = newStatLevel;
                    break;
            }
            
            // Increase trainer level
            trainer.Level = currentLevel + 1;
        });

        var result = new
        {
            success = true,
            newLevel = currentLevel + 1,
            statIncreased = statToIncrease,
            previousStatLevel = currentStatLevel.ToString(),
            newStatLevel = newStatLevel.ToString(),
            remainingLevelUps = Math.Max(0, expectedLevel - (currentLevel + 1)),
            message = $"Level up! {statToIncrease} increased from {currentStatLevel} to {newStatLevel}"
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Level up applied: {statToIncrease} {currentStatLevel} -> {newStatLevel}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_stat_increase_options")]
    [Description("Get available stat increase options for level up, showing current levels and what they would become.")]
    public async Task<string> GetStatIncreaseOptions()
    {
        Debug.WriteLine($"[GameEnginePlugin] GetStatIncreaseOptions called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var stats = gameState.Trainer.Stats;
        var options = new List<object>();

        // Check each stat
        foreach (var statName in new[] { "Strength", "Agility", "Social", "Intelligence" })
        {
            var currentLevel = GetStatLevel(stats, statName);
            var canIncrease = currentLevel < StatLevel.Legendary;
            var nextLevel = canIncrease ? (StatLevel)((int)currentLevel + 1) : currentLevel;
            
            options.Add(new
            {
                statName = statName,
                currentLevel = currentLevel.ToString(),
                currentModifier = GetStatModifier(currentLevel),
                nextLevel = nextLevel.ToString(),
                nextModifier = GetStatModifier(nextLevel),
                canIncrease = canIncrease,
                description = GetStatDescription(statName)
            });
        }

        var result = new
        {
            options = options,
            note = "Choose one stat to increase when leveling up"
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Pokemon Battle Mechanics

    [KernelFunction("calculate_type_effectiveness")]
    [Description("Calculate type effectiveness multiplier for attack vs defense types.")]
    public async Task<string> CalculateTypeEffectiveness(
        [Description("Attacking move type")] string attackType,
        [Description("Defending Pokemon's first type")] string defenseType1,
        [Description("Defending Pokemon's second type (optional)")] string defenseType2 = "")
    {
        await Task.Yield();
        
        var effectiveness1 = GetTypeEffectiveness(attackType, defenseType1);
        var effectiveness2 = string.IsNullOrEmpty(defenseType2) ? 1.0 : GetTypeEffectiveness(attackType, defenseType2);
        
        var totalEffectiveness = effectiveness1 * effectiveness2;
        
        var result = new
        {
            attackType = attackType,
            defenseType1 = defenseType1,
            defenseType2 = defenseType2,
            effectiveness1 = effectiveness1,
            effectiveness2 = effectiveness2,
            totalEffectiveness = totalEffectiveness,
            description = GetEffectivenessDescription(totalEffectiveness)
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Type effectiveness: {attackType} vs {defenseType1}/{defenseType2} = {totalEffectiveness}x");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

     #endregion

    #region Helper Methods

    private StatLevel GetStatLevel(Stats stats, string statName)
    {
        return statName.ToLower() switch
        {
            "strength" => stats.Strength,
            "agility" => stats.Agility,
            "social" => stats.Social,
            "intelligence" => stats.Intelligence,
            _ => StatLevel.Novice
        };
    }

    private int GetStatModifier(StatLevel statLevel)
    {
        return (int)statLevel; // StatLevel enum values are the modifiers
    }

    private bool IsValidStatName(string statName)
    {
        return statName.ToLower() is "strength" or "agility" or "social" or "intelligence";
    }

    private string GetStatDescription(string statName)
    {
        return statName.ToLower() switch
        {
            "strength" => "Athletics, carrying capacity, physical power",
            "agility" => "Speed, stealth, dodging, sleight of hand",
            "social" => "Persuasion, intimidation, leadership, reputation",
            "intelligence" => "Knowledge, problem-solving, technical skills, perception",
            _ => "Unknown stat"
        };
    }

    private int GetArchetypeTotalBonus(TrainerArchetype archetype)
    {
        return archetype switch
        {
            TrainerArchetype.BugCatcher => 2, // +1 Agility, +1 Intelligence
            TrainerArchetype.Hiker => 2, // +2 Strength
            TrainerArchetype.Psychic => 2, // +2 Intelligence
            TrainerArchetype.Medium => 2, // +1 Intelligence, +1 Social
            TrainerArchetype.AceTrainer => 2, // +1 Strength, +1 Agility
            TrainerArchetype.Researcher => 2, // +2 Intelligence
            TrainerArchetype.Coordinator => 2, // +1 Social, +1 Agility
            TrainerArchetype.Ranger => 2, // +1 Strength, +1 Intelligence
            _ => 0
        };
    }

    private int GetConditionModifier(List<ActiveCondition> conditions, string statName)
    {
        var modifier = 0;
        
        foreach (var condition in conditions)
        {
            modifier += condition.Type switch
            {
                TrainerCondition.Inspired when statName.ToLower() == "social" => +2,
                TrainerCondition.Focused when statName.ToLower() == "intelligence" => +2,
                TrainerCondition.Confident when statName.ToLower() is "social" or "strength" => +1,
                TrainerCondition.Tired => -1,
                TrainerCondition.Exhausted => -2,
                TrainerCondition.Injured when statName.ToLower() is "strength" or "agility" => -2,
                TrainerCondition.Intimidated when statName.ToLower() == "social" => -2,
                TrainerCondition.Poisoned => -1,
                _ => 0
            };
        }
        
        return modifier;
    }

    private int CalculateExperienceForLevel(int level)
    {
        // Experience curve: 1000 * (level - 1)^1.5
        return (int)(1000 * Math.Pow(level - 1, 1.5));
    }

    private int CalculateLevelFromExperience(int experience)
    {
        var level = 1;
        while (CalculateExperienceForLevel(level + 1) <= experience)
        {
            level++;
        }
        return level;
    }

    private void ApplyArchetypeStatBonuses(Dictionary<string, int> stats, TrainerArchetype archetype)
    {
        switch (archetype)
        {
            case TrainerArchetype.BugCatcher:
                stats["Agility"] += 1;
                stats["Intelligence"] += 1;
                break;
            case TrainerArchetype.Hiker:
                stats["Strength"] += 2;
                break;
            case TrainerArchetype.Psychic:
                stats["Intelligence"] += 2;
                break;
            case TrainerArchetype.Medium:
                stats["Intelligence"] += 1;
                stats["Social"] += 1;
                break;
            case TrainerArchetype.AceTrainer:
                stats["Strength"] += 1;
                stats["Agility"] += 1;
                break;
            case TrainerArchetype.Researcher:
                stats["Intelligence"] += 2;
                break;
            case TrainerArchetype.Coordinator:
                stats["Social"] += 1;
                stats["Agility"] += 1;
                break;
            case TrainerArchetype.Ranger:
                stats["Strength"] += 1;
                stats["Intelligence"] += 1;
                break;
        }
    }

    private int GetStartingMoney(TrainerArchetype archetype)
    {
        return archetype switch
        {
            TrainerArchetype.Researcher => 5000,
            TrainerArchetype.AceTrainer => 3000,
            TrainerArchetype.Coordinator => 2500,
            TrainerArchetype.Psychic => 2000,
            TrainerArchetype.Medium => 2000,
            TrainerArchetype.Ranger => 1500,
            TrainerArchetype.Hiker => 1000,
            TrainerArchetype.BugCatcher => 500,
            _ => 1000
        };
    }

    private List<string> GetStartingItems(TrainerArchetype archetype)
    {
        var items = new List<string> { "Pokeball", "Pokeball", "Pokeball", "Pokeball", "Pokeball", "Potion", "Potion" };
        
        return archetype switch
        {
            TrainerArchetype.Researcher => items.Concat(new[] { "Pokedex", "Notebook", "Research Kit" }).ToList(),
            TrainerArchetype.AceTrainer => items.Concat(new[] { "Super Potion", "Great Ball" }).ToList(),
            TrainerArchetype.Coordinator => items.Concat(new[] { "Poffin Case", "Contest Ribbon" }).ToList(),
            TrainerArchetype.Psychic => items.Concat(new[] { "Psychic Gem", "Mental Herb" }).ToList(),
            TrainerArchetype.Medium => items.Concat(new[] { "Cleanse Tag", "Spell Tag" }).ToList(),
            TrainerArchetype.Ranger => items.Concat(new[] { "Ranger Sign", "Survival Kit" }).ToList(),
            TrainerArchetype.Hiker => items.Concat(new[] { "Hiking Boots", "Rock Incense" }).ToList(),
            TrainerArchetype.BugCatcher => items.Concat(new[] { "Bug Net", "Silver Powder" }).ToList(),
            _ => items
        };
    }

    private double GetTypeEffectiveness(string attackType, string defenseType)
    {
        // Simplified type effectiveness chart - in a real implementation, this would be a comprehensive lookup table
        var effectiveness = new Dictionary<string, double>
        {
            // Fire
            ["Fire_Grass"] = 2.0,
            ["Fire_Ice"] = 2.0,
            ["Fire_Bug"] = 2.0,
            ["Fire_Steel"] = 2.0,
            ["Fire_Water"] = 0.5,
            ["Fire_Fire"] = 0.5,
            ["Fire_Rock"] = 0.5,
            ["Fire_Dragon"] = 0.5,
            
            // Water
            ["Water_Fire"] = 2.0,
            ["Water_Ground"] = 2.0,
            ["Water_Rock"] = 2.0,
            ["Water_Water"] = 0.5,
            ["Water_Grass"] = 0.5,
            ["Water_Dragon"] = 0.5,
            
            // Grass
            ["Grass_Water"] = 2.0,
            ["Grass_Ground"] = 2.0,
            ["Grass_Rock"] = 2.0,
            ["Grass_Fire"] = 0.5,
            ["Grass_Grass"] = 0.5,
            ["Grass_Poison"] = 0.5,
            ["Grass_Flying"] = 0.5,
            ["Grass_Bug"] = 0.5,
            ["Grass_Dragon"] = 0.5,
            ["Grass_Steel"] = 0.5,
            
            // Electric
            ["Electric_Water"] = 2.0,
            ["Electric_Flying"] = 2.0,
            ["Electric_Electric"] = 0.5,
            ["Electric_Grass"] = 0.5,
            ["Electric_Dragon"] = 0.5,
            ["Electric_Ground"] = 0.0
        };
        
        var key = $"{attackType}_{defenseType}";
        return effectiveness.GetValueOrDefault(key, 1.0);
    }

    private string GetEffectivenessDescription(double effectiveness)
    {
        return effectiveness switch
        {
            0.0 => "No effect",
            0.25 => "Not very effective",
            0.5 => "Not very effective",
            1.0 => "Normal damage",
            2.0 => "Super effective",
            4.0 => "Super effective",
            _ => "Normal damage"
        };
    }

    #endregion
}