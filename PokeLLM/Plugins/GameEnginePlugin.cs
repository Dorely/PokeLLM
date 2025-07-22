using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;
using PokeLLM.Game.Helpers;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Minimal GameEnginePlugin that focuses only on remaining utility functions not moved to specialized plugins.
/// Most functionality has been moved to CharacterManagementPlugin, PokemonManagementPlugin, 
/// WorldManagementPlugin, DiceAndSkillPlugin, and BattleCalculationPlugin.
/// </summary>
public class GameEnginePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameEnginePlugin(IGameStateRepository repository)
    {
        _repository = repository;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Game State Utilities

    [KernelFunction("get_current_game_state")]
    [Description("Get the complete current game state including trainer, Pokemon team, world state, and progress. Useful for context awareness and debugging.")]
    public async Task<string> GetCurrentGameState()
    {
        Debug.WriteLine($"[GameEnginePlugin] GetCurrentGameState called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var result = new
        {
            session = new
            {
                sessionId = gameState.SessionId,
                sessionStartTime = gameState.SessionStartTime,
                lastSaveTime = gameState.LastSaveTime
            },
            player = new
            {
                character = new
                {
                    id = gameState.Player.Character.Id,
                    name = gameState.Player.Character.Name,
                    level = gameState.Player.Character.Level,
                    faction = gameState.Player.Character.Faction,
                    money = gameState.Player.Character.Money,
                    stats = new
                    {
                        power = gameState.Player.Character.Stats.Power.Level.ToString(),
                        speed = gameState.Player.Character.Stats.Speed.Level.ToString(),
                        mind = gameState.Player.Character.Stats.Mind.Level.ToString(),
                        charm = gameState.Player.Character.Stats.Charm.Level.ToString(),
                        defense = gameState.Player.Character.Stats.Defense.Level.ToString(),
                        spirit = gameState.Player.Character.Stats.Spirit.Level.ToString()
                    },
                    conditions = gameState.Player.Character.Conditions.Select(c => new
                    {
                        type = c.Type.ToString(),
                        duration = c.Duration,
                        severity = c.Severity
                    }),
                    inventoryItemCount = gameState.Player.Character.Inventory.Count
                },
                progress = new
                {
                    experience = gameState.Player.Experience,
                    availableStatPoints = gameState.Player.AvailableStatPoints,
                    characterCreationComplete = gameState.Player.CharacterCreationComplete,
                    gymBadgeCount = gameState.Player.GymBadges.Count,
                    loreEntriesCount = gameState.Player.DiscoveredLore.Count,
                    npcRelationshipsCount = gameState.Player.NpcRelationships.Count,
                    factionRelationshipsCount = gameState.Player.FactionRelationships.Count
                }
            },
            pokemonTeam = new
            {
                activeTeamSize = gameState.Player.Character.PokemonTeam.ActivePokemon.Count,
                boxedPokemonCount = gameState.Player.Character.PokemonTeam.BoxedPokemon.Count,
                activeTeam = gameState.Player.Character.PokemonTeam.ActivePokemon.Select(p => new
                {
                    id = p.Pokemon.Id,
                    name = p.Pokemon.Name,
                    species = p.Pokemon.Species,
                    level = p.Pokemon.Level,
                    currentVigor = p.Pokemon.CurrentVigor,
                    maxVigor = p.Pokemon.MaxVigor,
                    type1 = p.Pokemon.Type1,
                    type2 = p.Pokemon.Type2,
                    friendship = p.Friendship
                })
            },
            environment = new
            {
                currentLocation = gameState.Environment.CurrentLocation,
                region = gameState.Environment.Region,
                timeOfDay = gameState.Environment.TimeOfDay.ToString(),
                weather = gameState.Environment.Weather.ToString()
            },
            worldState = new
            {
                visitedLocationsCount = gameState.WorldState.VisitedLocations.Count,
                gymBadges = gameState.WorldState.GymBadges.Count,
                npcRelationships = gameState.WorldState.NPCRelationships.Count,
                factionReputations = gameState.WorldState.FactionReputations.Count,
                discoveredLore = gameState.WorldState.DiscoveredLore.Count
            }
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_game_state_summary")]
    [Description("Get a concise summary of the current game state for quick reference. Includes key progress indicators and current status.")]
    public async Task<string> GetGameStateSummary()
    {
        Debug.WriteLine($"[GameEnginePlugin] GetGameStateSummary called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Calculate pending level ups
        var currentLevel = gameState.Player.Character.Level;
        var experience = gameState.Player.Experience;
        var expectedLevel = CalculateLevelFromExperience(experience);
        var pendingLevelUps = Math.Max(0, expectedLevel - currentLevel);

        // Check team health
        var activeTeam = gameState.Player.Character.PokemonTeam.ActivePokemon;
        var healthyPokemon = activeTeam.Count(p => p.Pokemon.CurrentVigor > 0);
        var faintedPokemon = activeTeam.Count(p => p.Pokemon.CurrentVigor == 0);

        // Active conditions
        var activeConditions = gameState.Player.Character.Conditions
            .Where(c => c.Duration != 0)
            .Select(c => c.Type.ToString())
            .ToList();

        var result = new
        {
            trainer = new
            {
                name = gameState.Player.Character.Name,
                level = currentLevel,
                experience = experience,
                pendingLevelUps = pendingLevelUps,
                availableStatPoints = gameState.Player.AvailableStatPoints,
                money = gameState.Player.Character.Money,
                activeConditions = activeConditions
            },
            location = new
            {
                current = gameState.Environment.CurrentLocation,
                region = gameState.Environment.Region,
                time = gameState.Environment.TimeOfDay.ToString(),
                weather = gameState.Environment.Weather.ToString()
            },
            pokemon = new
            {
                teamSize = activeTeam.Count,
                healthy = healthyPokemon,
                fainted = faintedPokemon,
                averageLevel = activeTeam.Count > 0 ? activeTeam.Average(p => p.Pokemon.Level) : 0
            },
            progress = new
            {
                gymBadges = gameState.Player.GymBadges.Count,
                characterCreationComplete = gameState.Player.CharacterCreationComplete,
                discoveredLore = gameState.Player.DiscoveredLore.Count
            },
            needsAttention = new
            {
                pendingLevelUps = pendingLevelUps > 0,
                availableStatPoints = gameState.Player.AvailableStatPoints > 0,
                faintedPokemon = faintedPokemon > 0,
                characterCreationIncomplete = !gameState.Player.CharacterCreationComplete
            }
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Data Validation and Utilities

    [KernelFunction("validate_stat_name")]
    [Description("Validate if a stat name is valid and return information about it. Useful for parameter validation.")]
    public async Task<string> ValidateStatName(
        [Description("The stat name to validate")] string statName)
    {
        await Task.Yield();
        
        var isValid = IsValidStatName(statName);
        var normalizedName = statName.ToLower() switch
        {
            "power" => "Power",
            "speed" => "Speed", 
            "mind" => "Mind",
            "charm" => "Charm",
            "defense" => "Defense",
            "spirit" => "Spirit",
            _ => statName
        };

        var result = new
        {
            isValid = isValid,
            originalName = statName,
            normalizedName = isValid ? normalizedName : null,
            description = isValid ? GetStatDescription(normalizedName) : "Invalid stat name",
            validStats = new[] { "Power", "Speed", "Mind", "Charm", "Defense", "Spirit" }
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_stat_level_info")]
    [Description("Get detailed information about stat levels and their effects. Useful for understanding character progression.")]
    public async Task<string> GetStatLevelInfo()
    {
        await Task.Yield();
        
        var statLevels = Enum.GetValues<StatLevel>()
            .Select(level => new
            {
                name = level.ToString(),
                value = (int)level,
                modifier = (int)level,
                description = level switch
                {
                    StatLevel.Hopeless => "Completely unable, major penalty",
                    StatLevel.Incompetent => "Well below average, penalty",
                    StatLevel.Novice => "Slightly below average, no modifier", 
                    StatLevel.Trained => "Average ability, small bonus",
                    StatLevel.Experienced => "Above average, bonus",
                    StatLevel.Expert => "Significantly above average, good bonus",
                    StatLevel.Veteran => "Professional level, major bonus",
                    StatLevel.Master => "Expert level, excellent bonus",
                    StatLevel.Grandmaster => "World-class ability, exceptional bonus",
                    StatLevel.Legendary => "Legendary ability, maximum bonus",
                    _ => "Unknown level"
                }
            })
            .ToList();

        var result = new
        {
            statLevels = statLevels,
            modifierRange = new
            {
                minimum = (int)StatLevel.Hopeless,
                maximum = (int)StatLevel.Legendary,
                average = (int)StatLevel.Novice
            },
            note = "Stat modifiers are added to d20 rolls for skill checks and other actions"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_condition_effects")]
    [Description("Get information about trainer condition effects and their impacts on gameplay.")]
    public async Task<string> GetConditionEffects()
    {
        await Task.Yield();
        
        var conditions = Enum.GetValues<TrainerCondition>()
            .Select(condition => new
            {
                name = condition.ToString(),
                effects = condition switch
                {
                    TrainerCondition.Healthy => "No penalties, baseline state",
                    TrainerCondition.Tired => "-1 to most checks, needs rest",
                    TrainerCondition.Injured => "-2 to Power and Speed checks",
                    TrainerCondition.Poisoned => "-1 to all checks, periodic damage risk",
                    TrainerCondition.Inspired => "+2 to Charm checks, increased motivation",
                    TrainerCondition.Focused => "+2 to Mind checks, enhanced concentration",
                    TrainerCondition.Exhausted => "-2 to all checks, severe fatigue",
                    TrainerCondition.Confident => "+1 to Charm and Power checks",
                    TrainerCondition.Intimidated => "-2 to Charm checks, reduced confidence",
                    _ => "Unknown condition"
                },
                severity = "Conditions can have severity levels 1-10 affecting magnitude",
                duration = "Duration can be temporary (turns/scenes) or permanent (-1)"
            })
            .ToList();

        var result = new
        {
            conditions = conditions,
            mechanics = new
            {
                stacking = "Multiple conditions can be active simultaneously",
                removal = "Conditions can be removed through rest, items, or story events",
                severity = "Higher severity increases the magnitude of effects"
            }
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Helper Methods

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

    private int CalculateLevelFromExperience(int experience)
    {
        var level = 1;
        while (CalculateExperienceForLevel(level + 1) <= experience)
        {
            level++;
        }
        return level;
    }

    private int CalculateExperienceForLevel(int level)
    {
        // Experience curve: 1000 * (level - 1)^1.5
        return (int)(1000 * Math.Pow(level - 1, 1.5));
    }

    #endregion
}