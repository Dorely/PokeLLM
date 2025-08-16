using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLogic;

public interface ICharacterManagementService
{
    Task<BasicPlayerState> GetPlayerDetails();
    Task SetPlayerName(string playerName);
    Task SetPlayerDescription(string description);
    Task<bool> AddPlayerExperiencePoints(int exp);
    Task SetPlayerCondition(string condition);
    Task AddPlayerRelationshipPoints(string targetId, int delta);
    Task RemovePlayerCondition(string condition);
    
    // Generic Character Management Methods
    Task<object> CreateCharacter(string name, string race, string characterClass);
    Task<object?> GetCurrentCharacter();
    Task SaveCharacter(object character);
    Task AddExperiencePoints(int xp);
}

/// <summary>
/// This service contains methods for managing character data within the gamestate
/// </summary>
public class CharacterManagementService : ICharacterManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly Random _random;
    private object? _currentCharacter;

    public CharacterManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
        _random = new Random();
    }

    public async Task<BasicPlayerState> GetPlayerDetails()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return gameState.Player;
    }

    public async Task SetPlayerName(string playerName)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Name = playerName;
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task SetPlayerDescription(string description)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Description = description;
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    //This method will increment experience and then calculate if they can level up.
    //Returning true if the character is ready for level up
    public async Task<bool> AddPlayerExperiencePoints(int exp)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Experience += exp;
        
        // Simple level up calculation: 1000 XP per level
        var newLevel = (gameState.Player.Experience / 1000) + 1;
        var canLevelUp = newLevel > gameState.Player.Level;
        
        if (canLevelUp)
        {
            gameState.Player.Level = newLevel;
        }
        
        await _gameStateRepository.SaveStateAsync(gameState);
        return canLevelUp;
    }

    public async Task SetPlayerCondition(string condition)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (!gameState.Player.Conditions.Contains(condition))
        {
            gameState.Player.Conditions.Add(condition);
            await _gameStateRepository.SaveStateAsync(gameState);
        }
    }

    public async Task RemovePlayerCondition(string condition)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (gameState.Player.Conditions.Contains(condition))
        {
            gameState.Player.Conditions.Remove(condition);
            await _gameStateRepository.SaveStateAsync(gameState);
        }
    }

    public async Task AddPlayerRelationshipPoints(string targetId, int delta)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var currentValue = gameState.Player.Relationships.GetValueOrDefault(targetId, 0);
        gameState.Player.Relationships[targetId] = Math.Max(-100, Math.Min(100, currentValue + delta));
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    #region Generic Character Management

    public async Task<object> CreateCharacter(string name, string race, string characterClass)
    {
        await Task.Yield();
        
        // Generic character creation - specific implementation would be provided by ruleset
        var character = new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Race"] = race,
            ["CharacterClass"] = characterClass,
            ["Level"] = 1
        };

        _currentCharacter = character;
        return character;
    }

    public async Task<object?> GetCurrentCharacter()
    {
        await Task.Yield();
        return _currentCharacter;
    }

    public async Task SaveCharacter(object character)
    {
        await Task.Yield();
        _currentCharacter = character;
        // In a full implementation, this would persist to the game state
    }

    public async Task AddExperiencePoints(int xp)
    {
        await Task.Yield();
        // Generic experience point handling - specific implementation would be provided by ruleset
        if (_currentCharacter is Dictionary<string, object> character)
        {
            var currentXp = character.ContainsKey("ExperiencePoints") ? (int)character["ExperiencePoints"] : 0;
            character["ExperiencePoints"] = currentXp + xp;
        }
    }

    #endregion
}
