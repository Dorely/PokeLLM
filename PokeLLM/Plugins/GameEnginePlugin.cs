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
    //TODO
    /*
     * need functions for managing every piece of state, preferably in small pieces, receiving entire json objects to overwrite the existing state for complex objects or taking individual parameters for simple changes
     * some functions can be specific and light weight, like adding a new pokemon to the players collection, healing all pokemon, changing the active location, time, or weather
     * example of functions that would take heavier json parameters would be: Create new NPC, Create new Pokemon
     * There should be an assortment of readonly functions that return the requested bit of information. None of these should return huge amounts of information
     * Some of these would be: GetWorldState - Returns Current location, region, time, weather; GetWorldNpcs - Returns a list of the names and Ids of the stored Npcs
     * The names and Ids would then go to be used by functions like: GetNpcDetails(string id = "") - Returns the full NPC object
     */

    [KernelFunction("get_condition_effects")]
    [Description("Get information about trainer condition effects and their impacts on gameplay.")]
    public async Task<string> GetConditionEffects()
    {
        await Task.Yield();

        var conditions = new Dictionary<string, string>()
        {
            {"Healthy", "No penalties, baseline state"},
            {"Tired", "-1 to most checks, needs rest" },
            {"Injured" , "-2 to Power and Speed checks" },
            {"Poisoned" , "-1 to all checks, periodic damage risk"},
            {"Inspired" , "+2 to Charm checks, increased motivation"},
            {"Focused" , "+2 to Mind checks, enhanced concentration"},
            {"Exhausted" , "-2 to all checks, severe fatigue"},
            {"Confident" , "+1 to Charm and Power checks"},
            {"Intimidated" , "-2 to Charm checks, reduced confidence"}
        };

        var result = new
        {
            conditions,
            mechanics = new
            {
                stacking = "Multiple conditions can be active simultaneously",
                removal = "Conditions can be removed through rest, items, or story events"
            }
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
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

}