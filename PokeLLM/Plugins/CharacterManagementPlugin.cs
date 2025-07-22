using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing character creation, level-ups, stats, conditions, and trainer progression
/// Handles D&D-style character development within the Pokemon world
/// </summary>
public class CharacterManagementPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public CharacterManagementPlugin(IGameStateRepository repository)
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

    #region Character Creation

    [KernelFunction("get_character_creation_status")]
    [Description("Check if character creation is complete and get current stat allocation status. Returns available stat points, current stat levels, and whether character creation needs completion. Example: Use to guide new players through character setup.")]
    public async Task<string> GetCharacterCreationStatus()
    {
        Debug.WriteLine($"[CharacterManagementPlugin] GetCharacterCreationStatus called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var player = gameState.Player;
        var character = player.Character;
        var result = new
        {
            characterCreationComplete = player.CharacterCreationComplete,
            availableStatPoints = player.AvailableStatPoints,
            currentStats = new
            {
                power = character.Stats.Power.Level.ToString(),
                speed = character.Stats.Speed.Level.ToString(),
                mind = character.Stats.Mind.Level.ToString(),
                charm = character.Stats.Charm.Level.ToString(),
                defense = character.Stats.Defense.Level.ToString(),
                spirit = character.Stats.Spirit.Level.ToString()
            },
            statModifiers = new
            {
                powerModifier = GetStatModifier(character.Stats.Power.Level),
                speedModifier = GetStatModifier(character.Stats.Speed.Level),
                mindModifier = GetStatModifier(character.Stats.Mind.Level),
                charmModifier = GetStatModifier(character.Stats.Charm.Level),
                defenseModifier = GetStatModifier(character.Stats.Defense.Level),
                spiritModifier = GetStatModifier(character.Stats.Spirit.Level)
            },
            canAllocatePoints = player.AvailableStatPoints > 0,
            needsCompletion = !player.CharacterCreationComplete
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("allocate_stat_point")]
    [Description("Allocate an available stat point to increase a stat during character creation or when points are available. Each point increases stat level (Hopeless to Legendary scale). Example: allocate_stat_point('Power') to increase physical strength.")]
    public async Task<string> AllocateStatPoint(
        [Description("The stat to increase: Power (strength/melee), Speed (initiative/agility), Mind (intelligence/special attack), Charm (social/leadership), Defense (physical resistance), Spirit (mental resistance)")] string statName)
    {
        Debug.WriteLine($"[CharacterManagementPlugin] AllocateStatPoint called with stat: {statName}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var player = gameState.Player;
        
        // Check if we have available points
        if (player.AvailableStatPoints <= 0)
            return JsonSerializer.Serialize(new { error = "No available stat points to allocate" }, _jsonOptions);

        // Validate stat name
        if (!IsValidStatName(statName))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Power, Speed, Mind, Charm, Defense, or Spirit" }, _jsonOptions);

        // Get current stat level
        var currentStatLevel = GetStatLevel(player.Character.Stats, statName);
        
        // Check if stat can be increased (max is Legendary = 7)
        if (currentStatLevel >= StatLevel.Legendary)
            return JsonSerializer.Serialize(new { error = $"{statName} is already at maximum level (Legendary)" }, _jsonOptions);

        // Increase the stat and decrease available points
        var newStatLevel = (StatLevel)((int)currentStatLevel + 1);
        await _repository.UpdatePlayerAsync(p =>
        {
            switch (statName.ToLower())
            {
                case "power":
                    p.Character.Stats.Power.Level = newStatLevel;
                    break;
                case "speed":
                    p.Character.Stats.Speed.Level = newStatLevel;
                    break;
                case "mind":
                    p.Character.Stats.Mind.Level = newStatLevel;
                    break;
                case "charm":
                    p.Character.Stats.Charm.Level = newStatLevel;
                    break;
                case "defense":
                    p.Character.Stats.Defense.Level = newStatLevel;
                    break;
                case "spirit":
                    p.Character.Stats.Spirit.Level = newStatLevel;
                    break;
            }
            
            p.AvailableStatPoints--;
        });

        var result = new
        {
            success = true,
            statIncreased = statName,
            previousStatLevel = currentStatLevel.ToString(),
            newStatLevel = newStatLevel.ToString(),
            previousModifier = GetStatModifier(currentStatLevel),
            newModifier = GetStatModifier(newStatLevel),
            remainingPoints = player.AvailableStatPoints - 1,
            message = $"{statName} increased from {currentStatLevel} to {newStatLevel}"
        };
        
        Debug.WriteLine($"[CharacterManagementPlugin] Stat point allocated: {statName} {currentStatLevel} -> {newStatLevel}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("reduce_stat_point")]
    [Description("Reduce a stat by one level to get a stat point back during character creation (minimum Hopeless). Only available before character creation is completed. Example: reduce_stat_point('Defense') to reallocate points elsewhere.")]
    public async Task<string> ReduceStatPoint(
        [Description("The stat to reduce (Power, Speed, Mind, Charm, Defense, Spirit)")] string statName)
    {
        Debug.WriteLine($"[CharacterManagementPlugin] ReduceStatPoint called with stat: {statName}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var player = gameState.Player;
        
        // Only allow during character creation
        if (player.CharacterCreationComplete)
            return JsonSerializer.Serialize(new { error = "Cannot reduce stats after character creation is complete" }, _jsonOptions);

        // Validate stat name
        if (!IsValidStatName(statName))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Power, Speed, Mind, Charm, Defense, or Spirit" }, _jsonOptions);

        // Get current stat level
        var currentStatLevel = GetStatLevel(player.Character.Stats, statName);
        
        // Check if stat can be reduced (minimum is Hopeless = -2)
        if (currentStatLevel <= StatLevel.Hopeless)
            return JsonSerializer.Serialize(new { error = $"{statName} is already at minimum level (Hopeless)" }, _jsonOptions);

        // Reduce the stat and increase available points
        var newStatLevel = (StatLevel)((int)currentStatLevel - 1);
        await _repository.UpdatePlayerAsync(p =>
        {
            switch (statName.ToLower())
            {
                case "power":
                    p.Character.Stats.Power.Level = newStatLevel;
                    break;
                case "speed":
                    p.Character.Stats.Speed.Level = newStatLevel;
                    break;
                case "mind":
                    p.Character.Stats.Mind.Level = newStatLevel;
                    break;
                case "charm":
                    p.Character.Stats.Charm.Level = newStatLevel;
                    break;
                case "defense":
                    p.Character.Stats.Defense.Level = newStatLevel;
                    break;
                case "spirit":
                    p.Character.Stats.Spirit.Level = newStatLevel;
                    break;
            }
            
            p.AvailableStatPoints++;
        });

        var result = new
        {
            success = true,
            statReduced = statName,
            previousStatLevel = currentStatLevel.ToString(),
            newStatLevel = newStatLevel.ToString(),
            previousModifier = GetStatModifier(currentStatLevel),
            newModifier = GetStatModifier(newStatLevel),
            newAvailablePoints = player.AvailableStatPoints + 1,
            message = $"{statName} reduced from {currentStatLevel} to {newStatLevel}"
        };
        
        Debug.WriteLine($"[CharacterManagementPlugin] Stat point reduced: {statName} {currentStatLevel} -> {newStatLevel}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("complete_character_creation")]
    [Description("Finalize character creation and mark it as complete. Any remaining stat points are lost. Allows the character to begin their Pokemon journey. Example: Call when player is satisfied with stat allocation.")]
    public async Task<string> CompleteCharacterCreation()
    {
        Debug.WriteLine($"[CharacterManagementPlugin] CompleteCharacterCreation called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var player = gameState.Player;
        
        if (player.CharacterCreationComplete)
            return JsonSerializer.Serialize(new { error = "Character creation is already complete" }, _jsonOptions);

        await _repository.UpdatePlayerAsync(p =>
        {
            p.CharacterCreationComplete = true;
            // Any remaining stat points are lost when completing creation
            p.AvailableStatPoints = 0;
        });

        var result = new
        {
            success = true,
            characterCreationComplete = true,
            finalStats = new
            {
                power = player.Character.Stats.Power.Level.ToString(),
                speed = player.Character.Stats.Speed.Level.ToString(),
                mind = player.Character.Stats.Mind.Level.ToString(),
                charm = player.Character.Stats.Charm.Level.ToString(),
                defense = player.Character.Stats.Defense.Level.ToString(),
                spirit = player.Character.Stats.Spirit.Level.ToString()
            },
            finalModifiers = new
            {
                powerModifier = GetStatModifier(player.Character.Stats.Power.Level),
                speedModifier = GetStatModifier(player.Character.Stats.Speed.Level),
                mindModifier = GetStatModifier(player.Character.Stats.Mind.Level),
                charmModifier = GetStatModifier(player.Character.Stats.Charm.Level),
                defenseModifier = GetStatModifier(player.Character.Stats.Defense.Level),
                spiritModifier = GetStatModifier(player.Character.Stats.Spirit.Level)
            },
            message = "Character creation completed! Your trainer is ready to begin their Pokemon journey."
        };
        
        Debug.WriteLine($"[CharacterManagementPlugin] Character creation completed for {player.Character.Name}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_stat_allocation_options")]
    [Description("Get detailed information about available stat allocation choices including descriptions, current levels, and potential improvements. Useful for guiding character creation decisions.")]
    public async Task<string> GetStatAllocationOptions()
    {
        Debug.WriteLine($"[CharacterManagementPlugin] GetStatAllocationOptions called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var stats = gameState.Player.Character.Stats;
        var options = new List<object>();

        // Check each stat
        foreach (var statName in new[] { "Power", "Speed", "Mind", "Charm", "Defense", "Spirit" })
        {
            var currentLevel = GetStatLevel(stats, statName);
            var canIncrease = currentLevel < StatLevel.Legendary;
            var canDecrease = currentLevel > StatLevel.Hopeless && !gameState.Player.CharacterCreationComplete;
            var nextLevel = canIncrease ? (StatLevel)((int)currentLevel + 1) : currentLevel;
            var prevLevel = canDecrease ? (StatLevel)((int)currentLevel - 1) : currentLevel;
            
            options.Add(new
            {
                statName = statName,
                currentLevel = currentLevel.ToString(),
                currentModifier = GetStatModifier(currentLevel),
                nextLevel = nextLevel.ToString(),
                nextModifier = GetStatModifier(nextLevel),
                prevLevel = prevLevel.ToString(),
                prevModifier = GetStatModifier(prevLevel),
                canIncrease = canIncrease,
                canDecrease = canDecrease,
                description = GetStatDescription(statName)
            });
        }

        var result = new
        {
            availablePoints = gameState.Player.AvailableStatPoints,
            characterCreationComplete = gameState.Player.CharacterCreationComplete,
            options = options,
            instructions = gameState.Player.CharacterCreationComplete ? 
                "Character creation is complete. Use level-ups to gain more stat points." :
                "During character creation, you can allocate available points or reduce stats to reallocate them."
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Experience and Level Management

    [KernelFunction("award_experience")]
    [Description("Award experience points based on challenge difficulty and circumstances. Experience accumulates but level-ups require separate stat choice calls. Example: award_experience(100, 1.5, 25, 'Defeated Gym Leader') for a challenging victory.")]
    public async Task<string> AwardExperience(
        [Description("Base experience amount for the accomplishment")] int baseExperience,
        [Description("Difficulty modifier: 0.5=Easy, 1.0=Normal, 1.5=Hard, 2.0=Extreme challenges")] double difficultyModifier = 1.0,
        [Description("Bonus for creative, exceptional, or outstanding play")] int creativityBonus = 0,
        [Description("Reason for the experience award for logging")] string reason = "")
    {
        Debug.WriteLine($"[CharacterManagementPlugin] AwardExperience called: base={baseExperience}, modifier={difficultyModifier}, bonus={creativityBonus}");
        
        var totalExperience = (int)(baseExperience * difficultyModifier) + creativityBonus;
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var oldLevel = gameState.Player.Character.Level;
        var oldExperience = gameState.Player.Experience;
        
        // Add experience but don't auto-level up
        gameState.Player.Experience += totalExperience;
        
        // Calculate what level they should be at based on experience
        var expectedLevel = CalculateLevelFromExperience(gameState.Player.Experience);
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
            newExperience = gameState.Player.Experience,
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

    [KernelFunction("award_stat_points")]
    [Description("Award additional stat points for special achievements, milestones, or exceptional roleplay. Separate from normal leveling. Example: award_stat_points(2, 'Exceptional leadership in crisis') for outstanding character moments.")]
    public async Task<string> AwardStatPoints(
        [Description("Number of stat points to award (typically 1-3)")] int points,
        [Description("Reason for awarding stat points for logging")] string reason = "")
    {
        Debug.WriteLine($"[CharacterManagementPlugin] AwardStatPoints called: points={points}, reason={reason}");
        
        if (points <= 0)
            return JsonSerializer.Serialize(new { error = "Must award at least 1 stat point" }, _jsonOptions);

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        await _repository.UpdatePlayerAsync(player =>
        {
            player.AvailableStatPoints += points;
        });

        var result = new
        {
            success = true,
            pointsAwarded = points,
            reason = reason,
            newTotalPoints = gameState.Player.AvailableStatPoints + points,
            message = $"Awarded {points} stat point(s)! Reason: {reason}"
        };
        
        Debug.WriteLine($"[CharacterManagementPlugin] Awarded {points} stat points for: {reason}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("check_pending_level_ups")]
    [Description("Check if the trainer has pending level-ups that need stat choices. Shows how many level-ups are available and current character state. Use before offering level-up choices.")]
    public async Task<string> CheckPendingLevelUps()
    {
        Debug.WriteLine($"[CharacterManagementPlugin] CheckPendingLevelUps called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var currentLevel = gameState.Player.Character.Level;
        var experience = gameState.Player.Experience;
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
                power = gameState.Player.Character.Stats.Power.Level.ToString(),
                speed = gameState.Player.Character.Stats.Speed.Level.ToString(),
                mind = gameState.Player.Character.Stats.Mind.Level.ToString(),
                charm = gameState.Player.Character.Stats.Charm.Level.ToString(),
                defense = gameState.Player.Character.Stats.Defense.Level.ToString(),
                spirit = gameState.Player.Character.Stats.Spirit.Level.ToString()
            }
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("apply_level_up")]
    [Description("Apply a level up by increasing a chosen stat. Must be called once for each pending level. Increases character level and chosen stat. Example: apply_level_up('Mind') to boost special attack and intelligence.")]
    public async Task<string> ApplyLevelUp(
        [Description("The stat to increase: Power, Speed, Mind, Charm, Defense, or Spirit")] string statToIncrease)
    {
        Debug.WriteLine($"[CharacterManagementPlugin] ApplyLevelUp called with stat: {statToIncrease}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var currentLevel = gameState.Player.Character.Level;
        var experience = gameState.Player.Experience;
        var expectedLevel = CalculateLevelFromExperience(experience);
        
        if (currentLevel >= expectedLevel)
        {
            return JsonSerializer.Serialize(new { error = "No pending level ups available" }, _jsonOptions);
        }

        // Validate stat name
        if (!IsValidStatName(statToIncrease))
        {
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Power, Speed, Mind, Charm, Defense, or Spirit" }, _jsonOptions);
        }

        // Get current stat level
        var currentStatLevel = GetStatLevel(gameState.Player.Character.Stats, statToIncrease);
        
        // Check if stat can be increased (max is Legendary = 7)
        if (currentStatLevel >= StatLevel.Legendary)
        {
            return JsonSerializer.Serialize(new { error = $"{statToIncrease} is already at maximum level (Legendary)" }, _jsonOptions);
        }

        // Increase the stat
        var newStatLevel = (StatLevel)((int)currentStatLevel + 1);
        await _repository.UpdatePlayerAsync(player =>
        {
            switch (statToIncrease.ToLower())
            {
                case "power":
                    player.Character.Stats.Power.Level = newStatLevel;
                    break;
                case "speed":
                    player.Character.Stats.Speed.Level = newStatLevel;
                    break;
                case "mind":
                    player.Character.Stats.Mind.Level = newStatLevel;
                    break;
                case "charm":
                    player.Character.Stats.Charm.Level = newStatLevel;
                    break;
                case "defense":
                    player.Character.Stats.Defense.Level = newStatLevel;
                    break;
                case "spirit":
                    player.Character.Stats.Spirit.Level = newStatLevel;
                    break;
            }
            
            // Increase trainer level
            player.Character.Level = currentLevel + 1;
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
        
        Debug.WriteLine($"[CharacterManagementPlugin] Level up applied: {statToIncrease} {currentStatLevel} -> {newStatLevel}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_stat_increase_options")]
    [Description("Get available stat increase options for level up, showing current levels and potential improvements. Use when player needs to choose which stat to increase during level-up.")]
    public async Task<string> GetStatIncreaseOptions()
    {
        Debug.WriteLine($"[CharacterManagementPlugin] GetStatIncreaseOptions called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var stats = gameState.Player.Character.Stats;
        var options = new List<object>();

        // Check each stat
        foreach (var statName in new[] { "Power", "Speed", "Mind", "Charm", "Defense", "Spirit" })
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

    #region Trainer Conditions

    [KernelFunction("add_trainer_condition")]
    [Description("Add a temporary or permanent condition to the trainer that affects gameplay. Conditions provide stat modifiers and roleplay opportunities. Example: add_trainer_condition('Inspired', 10, 2) for a major morale boost.")]
    public async Task<string> AddTrainerCondition(
        [Description("Condition type: Healthy, Tired, Injured, Poisoned, Inspired, Focused, Exhausted, Confident, Intimidated")] string conditionType,
        [Description("Duration in turns/scenes (-1 for permanent until removed)")] int duration = -1,
        [Description("Severity level affecting the magnitude of effects (1-10)")] int severity = 1)
    {
        Debug.WriteLine($"[CharacterManagementPlugin] AddTrainerCondition called with conditionType: '{conditionType}', duration: {duration}, severity: {severity}");
        
        if (!Enum.TryParse<TrainerCondition>(conditionType, true, out var condition))
            return JsonSerializer.Serialize(new { error = "Invalid condition type" }, _jsonOptions);

        await _repository.UpdatePlayerAsync(player =>
        {
            // Remove existing condition of same type
            player.Character.Conditions.RemoveAll(c => c.Type == condition);
            
            // Add new condition
            player.Character.Conditions.Add(new ActiveCondition
            {
                Type = condition,
                Duration = duration,
                Severity = severity
            });
        });

        var result = new
        {
            success = true,
            condition = condition.ToString(),
            duration = duration,
            severity = severity,
            message = $"Added condition {condition} with duration {duration} and severity {severity}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("remove_trainer_condition")]
    [Description("Remove a specific condition from the trainer, typically due to healing, rest, or resolution of circumstances. Example: remove_trainer_condition('Tired') after resting.")]
    public async Task<string> RemoveTrainerCondition(
        [Description("The condition type to remove")] string conditionType)
    {
        Debug.WriteLine($"[CharacterManagementPlugin] RemoveTrainerCondition called with conditionType: '{conditionType}'");
        
        if (!Enum.TryParse<TrainerCondition>(conditionType, true, out var condition))
            return JsonSerializer.Serialize(new { error = "Invalid condition type" }, _jsonOptions);

        var removed = false;
        await _repository.UpdatePlayerAsync(player =>
        {
            var initialCount = player.Character.Conditions.Count;
            player.Character.Conditions.RemoveAll(c => c.Type == condition);
            removed = player.Character.Conditions.Count < initialCount;
        });

        var result = new
        {
            success = removed,
            condition = condition.ToString(),
            message = removed ? $"Removed condition {condition}" : $"Condition {condition} was not active"
        };

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

    private int GetStatModifier(StatLevel statLevel)
    {
        return (int)statLevel; // StatLevel enum values are the modifiers
    }

    private bool IsValidStatName(string statName)
    {
        return statName.ToLower() is "power" or "speed" or "mind" or "charm" or "defense" or "spirit";
    }

    private string GetStatDescription(string statName)
    {
        return statName.ToLower() switch
        {
            "power" => "Physical strength, melee attacks, carrying capacity",
            "speed" => "Initiative, dodging, movement, agility", 
            "mind" => "Intelligence, perception, special attack power, problem-solving",
            "charm" => "Social interactions, trainer influence, Pokemon catching, leadership",
            "defense" => "Physical resistance, fortitude, vigor development",
            "spirit" => "Special resistance, willpower, vigor development, mental fortitude",
            _ => "Unknown stat"
        };
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

    #endregion
}