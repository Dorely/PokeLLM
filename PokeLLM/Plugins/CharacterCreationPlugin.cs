using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

    // --- Game Initialization ---

    [KernelFunction("create_new_game")]
    public async Task<string> CreateNewGame(string playerName)
    {
        try
        {
            var gameState = await _repository.CreateNewGameStateAsync(playerName);
            return JsonSerializer.Serialize(new { success = true, message = $"New game created for {playerName}" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to create new game: {ex.Message}" }, _jsonOptions);
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
}
