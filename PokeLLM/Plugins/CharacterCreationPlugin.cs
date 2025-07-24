using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// This Plugin is to be used during initial character creation or during level up subroutines
/// </summary>
public class CharacterCreationPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public CharacterCreationPlugin(IGameStateRepository repository)
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

    [KernelFunction("set_player_name")]
    public async Task<string> SetPlayerName(string playerName)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            gameState.Player.Character.Name = playerName;
            return JsonSerializer.Serialize(new { success = true, message = $"Player name set: {playerName}" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to update player name: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("complete_character_creation")]
    public async Task<string> CompleteCharacterCreation()
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            gameState.Player.CharacterCreationComplete = true;
            gameState.Player.AvailableStatPoints = 0;
            gameState.LastSaveTime = DateTime.UtcNow;

            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { success = true, message = "Character creation completed" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to complete character creation: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("get_current_stats")]
    public async Task<string> GetCurrentStats()
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var stats = gameState.Player.Character.Stats;

            return JsonSerializer.Serialize(new
            {
                success = true,
                stats,
                remainingPoints = gameState.Player.AvailableStatPoints
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to read stats: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("apply_stat_point")]
    public async Task<string> ApplyStatPoint(string statToIncrease)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.Player.AvailableStatPoints <= 0)
                return JsonSerializer.Serialize(new { error = "No available stat points" }, _jsonOptions);

            var stats = gameState.Player.Character.Stats;
            var statName = statToIncrease.ToLower();

            var currentLevel = statName switch
            {
                "power" => stats.Power,
                "speed" => stats.Speed,
                "mind" => stats.Mind,
                "charm" => stats.Charm,
                "defense" => stats.Defense,
                "spirit" => stats.Spirit,
                _ => throw new ArgumentException($"Invalid stat name: {statToIncrease}")
            };

            if (currentLevel >= StatLevel.Legendary)
                return JsonSerializer.Serialize(new { error = "Stat is already at maximum level" }, _jsonOptions);

            var newLevel = (StatLevel)((int)currentLevel + 1);

            switch (statName)
            {
                case "power": stats.Power = newLevel; break;
                case "speed": stats.Speed = newLevel; break;
                case "mind": stats.Mind = newLevel; break;
                case "charm": stats.Charm = newLevel; break;
                case "defense": stats.Defense = newLevel; break;
                case "spirit": stats.Spirit = newLevel; break;
            }

            gameState.Player.AvailableStatPoints--;
            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"{statToIncrease} increased to {newLevel}",
                remainingPoints = gameState.Player.AvailableStatPoints
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to apply level up: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("reduce_stat_point")]
    [Description("Reduce a specified stat by one level to gain an additional stat point. Can only be used during character creation and cannot reduce below Hopeless (-2).")]
    public async Task<string> ReduceStatPoint([Description("The stat to reduce: Power, Speed, Mind, Charm, Defense, or Spirit")] string statToDecrease)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Check if character creation is still in progress
            if (gameState.Player.CharacterCreationComplete)
                return JsonSerializer.Serialize(new { error = "Stat reduction can only be done during character creation" }, _jsonOptions);

            var stats = gameState.Player.Character.Stats;
            var statName = statToDecrease.ToLower();

            var currentLevel = statName switch
            {
                "power" => stats.Power,
                "speed" => stats.Speed,
                "mind" => stats.Mind,
                "charm" => stats.Charm,
                "defense" => stats.Defense,
                "spirit" => stats.Spirit,
                _ => throw new ArgumentException($"Invalid stat name: {statToDecrease}")
            };

            // Check if stat is already at minimum level (Hopeless = -2)
            if (currentLevel <= StatLevel.Hopeless)
                return JsonSerializer.Serialize(new { error = "Stat is already at minimum level (Hopeless)" }, _jsonOptions);

            var newLevel = (StatLevel)((int)currentLevel - 1);

            switch (statName)
            {
                case "power": stats.Power = newLevel; break;
                case "speed": stats.Speed = newLevel; break;
                case "mind": stats.Mind = newLevel; break;
                case "charm": stats.Charm = newLevel; break;
                case "defense": stats.Defense = newLevel; break;
                case "spirit": stats.Spirit = newLevel; break;
            }

            gameState.Player.AvailableStatPoints++;
            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"{statToDecrease} reduced to {newLevel}",
                remainingPoints = gameState.Player.AvailableStatPoints
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to reduce stat: {ex.Message}" }, _jsonOptions);
        }
    }
}
