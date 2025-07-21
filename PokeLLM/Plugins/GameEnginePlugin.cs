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

    #region Character Creation

    [KernelFunction("get_character_creation_status")]
    [Description("Check if character creation is complete and get current stat allocation status")]
    public async Task<string> GetCharacterCreationStatus()
    {
        Debug.WriteLine($"[GameEnginePlugin] GetCharacterCreationStatus called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var trainer = gameState.Trainer;
        var result = new
        {
            characterCreationComplete = trainer.CharacterCreationComplete,
            availableStatPoints = trainer.AvailableStatPoints,
            currentStats = new
            {
                strength = trainer.Stats.Strength.ToString(),
                agility = trainer.Stats.Agility.ToString(),
                social = trainer.Stats.Social.ToString(),
                intelligence = trainer.Stats.Intelligence.ToString()
            },
            statModifiers = new
            {
                strengthModifier = GetStatModifier(trainer.Stats.Strength),
                agilityModifier = GetStatModifier(trainer.Stats.Agility),
                socialModifier = GetStatModifier(trainer.Stats.Social),
                intelligenceModifier = GetStatModifier(trainer.Stats.Intelligence)
            },
            canAllocatePoints = trainer.AvailableStatPoints > 0,
            needsCompletion = !trainer.CharacterCreationComplete
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("allocate_stat_point")]
    [Description("Allocate an available stat point to increase a stat during character creation or when points are available")]
    public async Task<string> AllocateStatPoint(
        [Description("The stat to increase (Strength, Agility, Social, Intelligence)")] string statName)
    {
        Debug.WriteLine($"[GameEnginePlugin] AllocateStatPoint called with stat: {statName}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var trainer = gameState.Trainer;
        
        // Check if we have available points
        if (trainer.AvailableStatPoints <= 0)
            return JsonSerializer.Serialize(new { error = "No available stat points to allocate" }, _jsonOptions);

        // Validate stat name
        if (!IsValidStatName(statName))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Strength, Agility, Social, or Intelligence" }, _jsonOptions);

        // Get current stat level
        var currentStatLevel = GetStatLevel(trainer.Stats, statName);
        
        // Check if stat can be increased (max is Legendary = 7)
        if (currentStatLevel >= StatLevel.Legendary)
            return JsonSerializer.Serialize(new { error = $"{statName} is already at maximum level (Legendary)" }, _jsonOptions);

        // Increase the stat and decrease available points
        var newStatLevel = (StatLevel)((int)currentStatLevel + 1);
        await _repository.UpdateTrainerAsync(t =>
        {
            switch (statName.ToLower())
            {
                case "strength":
                    t.Stats.Strength = newStatLevel;
                    break;
                case "agility":
                    t.Stats.Agility = newStatLevel;
                    break;
                case "social":
                    t.Stats.Social = newStatLevel;
                    break;
                case "intelligence":
                    t.Stats.Intelligence = newStatLevel;
                    break;
            }
            
            t.AvailableStatPoints--;
        });

        var result = new
        {
            success = true,
            statIncreased = statName,
            previousStatLevel = currentStatLevel.ToString(),
            newStatLevel = newStatLevel.ToString(),
            previousModifier = GetStatModifier(currentStatLevel),
            newModifier = GetStatModifier(newStatLevel),
            remainingPoints = trainer.AvailableStatPoints - 1,
            message = $"{statName} increased from {currentStatLevel} to {newStatLevel}"
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Stat point allocated: {statName} {currentStatLevel} -> {newStatLevel}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("reduce_stat_point")]
    [Description("Reduce a stat by one level to get a stat point back during character creation (minimum Hopeless)")]
    public async Task<string> ReduceStatPoint(
        [Description("The stat to reduce (Strength, Agility, Social, Intelligence)")] string statName)
    {
        Debug.WriteLine($"[GameEnginePlugin] ReduceStatPoint called with stat: {statName}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var trainer = gameState.Trainer;
        
        // Only allow during character creation or when explicitly allowed
        if (trainer.CharacterCreationComplete)
            return JsonSerializer.Serialize(new { error = "Cannot reduce stats after character creation is complete" }, _jsonOptions);

        // Validate stat name
        if (!IsValidStatName(statName))
            return JsonSerializer.Serialize(new { error = "Invalid stat name. Use: Strength, Agility, Social, or Intelligence" }, _jsonOptions);

        // Get current stat level
        var currentStatLevel = GetStatLevel(trainer.Stats, statName);
        
        // Check if stat can be reduced (minimum is Hopeless = -2)
        if (currentStatLevel <= StatLevel.Hopeless)
            return JsonSerializer.Serialize(new { error = $"{statName} is already at minimum level (Hopeless)" }, _jsonOptions);

        // Reduce the stat and increase available points
        var newStatLevel = (StatLevel)((int)currentStatLevel - 1);
        await _repository.UpdateTrainerAsync(t =>
        {
            switch (statName.ToLower())
            {
                case "strength":
                    t.Stats.Strength = newStatLevel;
                    break;
                case "agility":
                    t.Stats.Agility = newStatLevel;
                    break;
                case "social":
                    t.Stats.Social = newStatLevel;
                    break;
                case "intelligence":
                    t.Stats.Intelligence = newStatLevel;
                    break;
            }
            
            t.AvailableStatPoints++;
        });

        var result = new
        {
            success = true,
            statReduced = statName,
            previousStatLevel = currentStatLevel.ToString(),
            newStatLevel = newStatLevel.ToString(),
            previousModifier = GetStatModifier(currentStatLevel),
            newModifier = GetStatModifier(newStatLevel),
            newAvailablePoints = trainer.AvailableStatPoints + 1,
            message = $"{statName} reduced from {currentStatLevel} to {newStatLevel}"
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Stat point reduced: {statName} {currentStatLevel} -> {newStatLevel}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("complete_character_creation")]
    [Description("Finalize character creation and mark it as complete")]
    public async Task<string> CompleteCharacterCreation()
    {
        Debug.WriteLine($"[GameEnginePlugin] CompleteCharacterCreation called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var trainer = gameState.Trainer;
        
        if (trainer.CharacterCreationComplete)
            return JsonSerializer.Serialize(new { error = "Character creation is already complete" }, _jsonOptions);

        await _repository.UpdateTrainerAsync(t =>
        {
            t.CharacterCreationComplete = true;
            // Any remaining stat points are lost when completing creation
            t.AvailableStatPoints = 0;
        });

        var result = new
        {
            success = true,
            characterCreationComplete = true,
            finalStats = new
            {
                strength = trainer.Stats.Strength.ToString(),
                agility = trainer.Stats.Agility.ToString(),
                social = trainer.Stats.Social.ToString(),
                intelligence = trainer.Stats.Intelligence.ToString()
            },
            finalModifiers = new
            {
                strengthModifier = GetStatModifier(trainer.Stats.Strength),
                agilityModifier = GetStatModifier(trainer.Stats.Agility),
                socialModifier = GetStatModifier(trainer.Stats.Social),
                intelligenceModifier = GetStatModifier(trainer.Stats.Intelligence)
            },
            message = "Character creation completed! Your trainer is ready to begin their Pokemon journey."
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Character creation completed for {trainer.Name}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_stat_allocation_options")]
    [Description("Get detailed information about available stat allocation choices")]
    public async Task<string> GetStatAllocationOptions()
    {
        Debug.WriteLine($"[GameEnginePlugin] GetStatAllocationOptions called");
        
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
            var canDecrease = currentLevel > StatLevel.Hopeless && !gameState.Trainer.CharacterCreationComplete;
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
            availablePoints = gameState.Trainer.AvailableStatPoints,
            characterCreationComplete = gameState.Trainer.CharacterCreationComplete,
            options = options,
            instructions = gameState.Trainer.CharacterCreationComplete ? 
                "Character creation is complete. Use level-ups to gain more stat points." :
                "During character creation, you can allocate available points or reduce stats to reallocate them."
        };
        
        return JsonSerializer.Serialize(result, _jsonOptions);
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

    [KernelFunction("award_stat_points")]
    [Description("Award additional stat points for special achievements or milestones")]
    public async Task<string> AwardStatPoints(
        [Description("Number of stat points to award")] int points,
        [Description("Reason for awarding stat points")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] AwardStatPoints called: points={points}, reason={reason}");
        
        if (points <= 0)
            return JsonSerializer.Serialize(new { error = "Must award at least 1 stat point" }, _jsonOptions);

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        await _repository.UpdateTrainerAsync(trainer =>
        {
            trainer.AvailableStatPoints += points;
        });

        var result = new
        {
            success = true,
            pointsAwarded = points,
            reason = reason,
            newTotalPoints = gameState.Trainer.AvailableStatPoints + points,
            message = $"Awarded {points} stat point(s)! Reason: {reason}"
        };
        
        Debug.WriteLine($"[GameEnginePlugin] Awarded {points} stat points for: {reason}");
        return JsonSerializer.Serialize(result, _jsonOptions);
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

    #region Trainer Management

    [KernelFunction("add_trainer_condition")]
    [Description("Add a condition to the trainer (Tired, Inspired, Focused, etc.)")]
    public async Task<string> AddTrainerCondition(
        [Description("The condition type (Tired, Inspired, Focused, Confident, Exhausted, Injured, Intimidated, Poisoned)")] string conditionType,
        [Description("Duration in turns (-1 for permanent)")] int duration = -1,
        [Description("Severity level (1-10)")] int severity = 1)
    {
        Debug.WriteLine($"[GameEnginePlugin] AddTrainerCondition called with conditionType: '{conditionType}', duration: {duration}, severity: {severity}");
        
        if (!Enum.TryParse<TrainerCondition>(conditionType, true, out var condition))
            return JsonSerializer.Serialize(new { error = "Invalid condition type" }, _jsonOptions);

        await _repository.UpdateTrainerAsync(trainer =>
        {
            // Remove existing condition of same type
            trainer.Conditions.RemoveAll(c => c.Type == condition);
            
            // Add new condition
            trainer.Conditions.Add(new ActiveCondition
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
    [Description("Remove a specific condition from the trainer")]
    public async Task<string> RemoveTrainerCondition(
        [Description("The condition type to remove")] string conditionType)
    {
        Debug.WriteLine($"[GameEnginePlugin] RemoveTrainerCondition called with conditionType: '{conditionType}'");
        
        if (!Enum.TryParse<TrainerCondition>(conditionType, true, out var condition))
            return JsonSerializer.Serialize(new { error = "Invalid condition type" }, _jsonOptions);

        var removed = false;
        await _repository.UpdateTrainerAsync(trainer =>
        {
            var initialCount = trainer.Conditions.Count;
            trainer.Conditions.RemoveAll(c => c.Type == condition);
            removed = trainer.Conditions.Count < initialCount;
        });

        var result = new
        {
            success = removed,
            condition = condition.ToString(),
            message = removed ? $"Removed condition {condition}" : $"Condition {condition} was not active"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_money")]
    [Description("Add or subtract money from the trainer")]
    public async Task<string> UpdateMoney(
        [Description("Amount to add (positive) or subtract (negative)")] int amount,
        [Description("Reason for the money change")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] UpdateMoney called with amount: {amount}");
        var newAmount = 0;
        
        await _repository.UpdateTrainerAsync(trainer =>
        {
            trainer.Money = Math.Max(0, trainer.Money + amount);
            newAmount = trainer.Money;
        });

        var result = new
        {
            success = true,
            change = amount,
            newTotal = newAmount,
            reason = reason,
            message = $"Money updated by {amount:+#;-#;0}. Current money: {newAmount}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("add_to_inventory")]
    [Description("Add items to the trainer's inventory")]
    public async Task<string> AddToInventory(
        [Description("Name of the item")] string itemName,
        [Description("Quantity to add")] int quantity = 1,
        [Description("Reason for adding item")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] AddToInventory called with itemName: '{itemName}', quantity: {quantity}");
        
        var newQuantity = 0;
        await _repository.UpdateTrainerAsync(trainer =>
        {
            trainer.Inventory[itemName] = trainer.Inventory.GetValueOrDefault(itemName, 0) + quantity;
            newQuantity = trainer.Inventory[itemName];
        });

        var result = new
        {
            success = true,
            itemName = itemName,
            quantityAdded = quantity,
            newTotal = newQuantity,
            reason = reason,
            message = $"Added {quantity} {itemName}(s) to inventory. Total: {newQuantity}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("remove_from_inventory")]
    [Description("Remove items from the trainer's inventory")]
    public async Task<string> RemoveFromInventory(
        [Description("Name of the item")] string itemName,
        [Description("Quantity to remove")] int quantity = 1,
        [Description("Reason for removing item")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] RemoveFromInventory called with itemName: '{itemName}', quantity: {quantity}");
        
        var success = false;
        var newQuantity = 0;
        await _repository.UpdateTrainerAsync(trainer =>
        {
            var currentQuantity = trainer.Inventory.GetValueOrDefault(itemName, 0);
            if (currentQuantity >= quantity)
            {
                trainer.Inventory[itemName] = currentQuantity - quantity;
                if (trainer.Inventory[itemName] == 0)
                    trainer.Inventory.Remove(itemName);
                newQuantity = trainer.Inventory.GetValueOrDefault(itemName, 0);
                success = true;
            }
        });

        var result = new
        {
            success = success,
            itemName = itemName,
            quantityRemoved = success ? quantity : 0,
            newTotal = newQuantity,
            reason = reason,
            message = success ? 
                $"Removed {quantity} {itemName}(s) from inventory. Total: {newQuantity}" :
                $"Insufficient {itemName} in inventory"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Pokemon Management

    [KernelFunction("add_pokemon_to_team")]
    [Description("Add a new Pokemon to the trainer's team")]
    public async Task<string> AddPokemonToTeam(
        [Description("Pokemon's nickname")] string name,
        [Description("Pokemon species (e.g., 'Pikachu', 'Charizard')")] string species,
        [Description("Pokemon level")] int level = 1,
        [Description("Primary type")] string type1 = "Normal",
        [Description("Secondary type (optional)")] string type2 = "",
        [Description("Current vigor/health")] int currentVigor = 20,
        [Description("Maximum vigor/health")] int maxVigor = 20,
        [Description("Location where caught")] string caughtLocation = "Unknown",
        [Description("Friendship level")] int friendship = 50,
        [Description("Pokemon ability")] string ability = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] AddPokemonToTeam called with name: '{name}', species: '{species}'");
        
        var pokemon = new Pokemon
        {
            Name = name,
            Species = species,
            Level = level,
            Experience = 0,
            KnownMoves = new HashSet<string>(),
            CurrentVigor = currentVigor,
            MaxVigor = maxVigor,
            Stats = new Stats(), // Default stats for now
            Type1 = type1,
            Type2 = string.IsNullOrEmpty(type2) ? null : type2,
            Ability = ability,
            CaughtLocation = caughtLocation,
            Friendship = friendship
        };

        await _repository.AddPokemonToTeamAsync(pokemon);
        
        var result = new
        {
            success = true,
            pokemon = new
            {
                name = pokemon.Name,
                species = pokemon.Species,
                level = pokemon.Level,
                type1 = pokemon.Type1,
                type2 = pokemon.Type2,
                vigor = $"{pokemon.CurrentVigor}/{pokemon.MaxVigor}",
                caughtLocation = pokemon.CaughtLocation
            },
            message = $"Added {pokemon.Name} ({pokemon.Species}) to the team!"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_pokemon_vigor")]
    [Description("Update a Pokemon's vigor (health/energy) by name")]
    public async Task<string> UpdatePokemonVigor(
        [Description("Name of the Pokemon to update")] string pokemonName,
        [Description("New current vigor value")] int currentVigor,
        [Description("Reason for vigor change")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] UpdatePokemonVigor called with pokemonName: '{pokemonName}', currentVigor: {currentVigor}");
        var found = false;
        var actualNewVigor = 0;

        var state = await _repository.LoadLatestStateAsync();
        if (state != null)
        {
            var pokemon = state.PokemonTeam.ActivePokemon.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)) ??
                         state.PokemonTeam.BoxedPokemon.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));
            
            if (pokemon != null)
            {
                pokemon.CurrentVigor = Math.Max(0, Math.Min(currentVigor, pokemon.MaxVigor));
                actualNewVigor = pokemon.CurrentVigor;
                found = true;
                await _repository.SaveStateAsync(state);
            }
        }

        var result = new
        {
            success = found,
            pokemonName = pokemonName,
            newVigor = actualNewVigor,
            reason = reason,
            message = found ? 
                $"Updated {pokemonName}'s vigor to {actualNewVigor}" : 
                "Pokemon not found"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("heal_pokemon")]
    [Description("Fully heal a Pokemon's vigor")]
    public async Task<string> HealPokemon(
        [Description("Name of the Pokemon to heal")] string pokemonName,
        [Description("Reason for healing")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] HealPokemon called with pokemonName: '{pokemonName}'");
        var found = false;
        var maxVigor = 0;

        var state = await _repository.LoadLatestStateAsync();
        if (state != null)
        {
            var pokemon = state.PokemonTeam.ActivePokemon.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)) ??
                         state.PokemonTeam.BoxedPokemon.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));
            
            if (pokemon != null)
            {
                pokemon.CurrentVigor = pokemon.MaxVigor;
                maxVigor = pokemon.MaxVigor;
                found = true;
                await _repository.SaveStateAsync(state);
            }
        }

        var result = new
        {
            success = found,
            pokemonName = pokemonName,
            newVigor = maxVigor,
            reason = reason,
            message = found ? 
                $"{pokemonName} fully healed to {maxVigor} vigor" : 
                "Pokemon not found"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region World and Location Management

    [KernelFunction("change_location")]
    [Description("Move the trainer to a new location")]
    public async Task<string> ChangeLocation(
        [Description("Name of the new location")] string newLocation,
        [Description("Region where the location is (optional)")] string region = "",
        [Description("Reason for travel")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] ChangeLocation called with newLocation: '{newLocation}', region: '{region}'");
        
        var previousLocation = "";
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            previousLocation = worldState.CurrentLocation;
            worldState.CurrentLocation = newLocation;
            if (!string.IsNullOrEmpty(region))
                worldState.CurrentRegion = region;
            
            worldState.VisitedLocations.Add(newLocation);
        });

        var result = new
        {
            success = true,
            previousLocation = previousLocation,
            newLocation = newLocation,
            region = region,
            reason = reason,
            message = $"Moved to {newLocation}" + (string.IsNullOrEmpty(region) ? "" : $" in {region}")
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("set_time_and_weather")]
    [Description("Update the time of day and weather")]
    public async Task<string> SetTimeAndWeather(
        [Description("Time of day (Morning, Afternoon, Evening, Night)")] string timeOfDay,
        [Description("Weather condition")] string weather = "Clear",
        [Description("Reason for change")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] SetTimeAndWeather called with timeOfDay: '{timeOfDay}', weather: '{weather}'");
        
        if (!Enum.TryParse<TimeOfDay>(timeOfDay, true, out var time))
            return JsonSerializer.Serialize(new { error = "Invalid time of day" }, _jsonOptions);

        await _repository.UpdateWorldStateAsync(worldState =>
        {
            worldState.TimeOfDay = time;
            worldState.WeatherCondition = weather;
        });

        var result = new
        {
            success = true,
            timeOfDay = time.ToString(),
            weather = weather,
            reason = reason,
            message = $"Time set to {time}, weather set to {weather}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_npc_relationship")]
    [Description("Update relationship with an NPC")]
    public async Task<string> UpdateNPCRelationship(
        [Description("Name or ID of the NPC")] string npcId,
        [Description("Change in relationship level (-100 to 100)")] int relationshipChange,
        [Description("Reason for relationship change")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] UpdateNPCRelationship called with npcId: '{npcId}', relationshipChange: {relationshipChange}");
        
        var newLevel = 0;
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            var currentLevel = worldState.NPCRelationships.GetValueOrDefault(npcId, 0);
            worldState.NPCRelationships[npcId] = Math.Max(-100, Math.Min(100, currentLevel + relationshipChange));
            newLevel = worldState.NPCRelationships[npcId];
        });

        var result = new
        {
            success = true,
            npcId = npcId,
            change = relationshipChange,
            newLevel = newLevel,
            reason = reason,
            message = $"Relationship with {npcId} updated by {relationshipChange:+#;-#;0}. Current level: {newLevel}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_faction_reputation")]
    [Description("Update reputation with a faction")]
    public async Task<string> UpdateFactionReputation(
        [Description("Name of the faction")] string factionName,
        [Description("Change in reputation (-100 to 100)")] int reputationChange,
        [Description("Reason for reputation change")] string reason = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] UpdateFactionReputation called with factionName: '{factionName}', reputationChange: {reputationChange}");
        
        var newRep = 0;
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            var currentRep = worldState.FactionReputations.GetValueOrDefault(factionName, 0);
            worldState.FactionReputations[factionName] = Math.Max(-100, Math.Min(100, currentRep + reputationChange));
            newRep = worldState.FactionReputations[factionName];
        });

        var result = new
        {
            success = true,
            factionName = factionName,
            change = reputationChange,
            newReputation = newRep,
            reason = reason,
            message = $"Reputation with {factionName} updated by {reputationChange:+#;-#;0}. Current reputation: {newRep}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("earn_gym_badge")]
    [Description("Award a gym badge to the trainer")]
    public async Task<string> EarnGymBadge(
        [Description("Name of the gym")] string gymName,
        [Description("Name of the gym leader")] string leaderName,
        [Description("Location of the gym")] string location,
        [Description("Badge type (Fire, Water, etc.)")] string badgeType,
        [Description("How the badge was earned")] string achievement = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] EarnGymBadge called with gymName: '{gymName}', leaderName: '{leaderName}'");
        
        var alreadyHad = false;
        var totalBadges = 0;
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            // Check if badge already exists
            if (!worldState.GymBadges.Any(b => b.GymName.Equals(gymName, StringComparison.OrdinalIgnoreCase)))
            {
                worldState.GymBadges.Add(new GymBadge
                {
                    GymName = gymName,
                    LeaderName = leaderName,
                    Location = location,
                    BadgeType = badgeType
                });
            }
            else
            {
                alreadyHad = true;
            }
            totalBadges = worldState.GymBadges.Count;
        });

        var result = new
        {
            success = !alreadyHad,
            gymName = gymName,
            leaderName = leaderName,
            location = location,
            badgeType = badgeType,
            achievement = achievement,
            totalBadges = totalBadges,
            message = alreadyHad ? 
                $"Already have the {badgeType} Badge from {gymName}" :
                $"Earned the {badgeType} Badge from {gymName} in {location}! Total badges: {totalBadges}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("discover_lore")]
    [Description("Add discovered lore to the game world")]
    public async Task<string> DiscoverLore(
        [Description("The lore entry to add")] string loreEntry,
        [Description("How the lore was discovered")] string discoveryMethod = "")
    {
        Debug.WriteLine($"[GameEnginePlugin] DiscoverLore called with loreEntry: '{loreEntry}'");
        
        var alreadyKnown = false;
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            if (worldState.DiscoveredLore.Contains(loreEntry))
            {
                alreadyKnown = true;
            }
            else
            {
                worldState.DiscoveredLore.Add(loreEntry);
            }
        });

        var result = new
        {
            success = !alreadyKnown,
            loreEntry = loreEntry,
            discoveryMethod = discoveryMethod,
            message = alreadyKnown ? 
                "This lore was already known" :
                $"Discovered new lore: {loreEntry}"
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

    private double GetTypeEffectiveness(string attackType, string defenseType)
    {
        // Simplified type effectiveness chart - in a real implementation, this would be a comprehensive lookup table
        var effectiveness = new Dictionary<string, double>();
        
        // Fire type effectiveness
        effectiveness["Fire_Grass"] = 2.0;
        effectiveness["Fire_Ice"] = 2.0;
        effectiveness["Fire_Bug"] = 2.0;
        effectiveness["Fire_Steel"] = 2.0;
        effectiveness["Fire_Water"] = 0.5;
        effectiveness["Fire_Fire"] = 0.5;
        effectiveness["Fire_Rock"] = 0.5;
        effectiveness["Fire_Dragon"] = 0.5;
        
        // Water type effectiveness
        effectiveness["Water_Fire"] = 2.0;
        effectiveness["Water_Ground"] = 2.0;
        effectiveness["Water_Rock"] = 2.0;
        effectiveness["Water_Water"] = 0.5;
        effectiveness["Water_Grass"] = 0.5;
        effectiveness["Water_Dragon"] = 0.5;
        
        // Grass type effectiveness
        effectiveness["Grass_Water"] = 2.0;
        effectiveness["Grass_Ground"] = 2.0;
        effectiveness["Grass_Rock"] = 2.0;
        effectiveness["Grass_Fire"] = 0.5;
        effectiveness["Grass_Grass"] = 0.5;
        effectiveness["Grass_Poison"] = 0.5;
        effectiveness["Grass_Flying"] = 0.5;
        effectiveness["Grass_Bug"] = 0.5;
        effectiveness["Grass_Dragon"] = 0.5;
        effectiveness["Grass_Steel"] = 0.5;
        
        // Electric type effectiveness
        effectiveness["Electric_Water"] = 2.0;
        effectiveness["Electric_Flying"] = 2.0;
        effectiveness["Electric_Electric"] = 0.5;
        effectiveness["Electric_Grass"] = 0.5;
        effectiveness["Electric_Dragon"] = 0.5;
        effectiveness["Electric_Ground"] = 0.0;
        
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