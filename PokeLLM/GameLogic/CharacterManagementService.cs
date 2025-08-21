using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLogic;

public interface ICharacterManagementService
{
    Task<PlayerState> GetPlayerDetails();
    Task SetPlayerName(string playerName);
    Task SetPlayerStats(int[] stats);
    Task SetPlayerClass(string classId);
    Task DamagePlayerVigor(int damage);
    Task HealPlayerVigor(int amount);
    Task HealPlayerVigorToMax();
    Task LearnPlayerAbility(string ability);
    Task AddItemPlayerInventory(ItemInstance item);
    Task RemoveItemPlayerInventory(string itemId, int quantity);
    Task ChangePlayerMoney(int deltaChange);
    Task ChangePlayerRenown(int deltaChange);
    Task ChangePlayerNotoriety(int deltaChange);
    Task SetPlayerDescription(string description);
    Task<bool> AddPlayerExperiencePoints(int exp);
    Task SetPlayerCondition(string condition);
    Task AddPokemonToTeam(string pokeId);
    Task AddPokemonToBox(string pokeId);
    Task AddPlayerNpcRelationShipPoints(string npcId, int delta);
    Task AddPlayerFactionRelationShipPoints(string factionId, int delta);
    Task AddPlayerBadge(string badge);
    
    // New methods for character creation
    Task<int[]> GenerateRandomStats();
    Task<int[]> GenerateStandardStats();
    
    // D&D 5e Character Management Methods
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

    public async Task<PlayerState> GetPlayerDetails()
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

    public async Task SetPlayerStats(int[] stats)
    {
        if (stats.Length != 6)
            throw new ArgumentException("Stats array must contain exactly 6 values: [Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma]");

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Stats.Strength = stats[0];
        gameState.Player.Stats.Dexterity = stats[1];
        gameState.Player.Stats.Constitution = stats[2];
        gameState.Player.Stats.Intelligence = stats[3];
        gameState.Player.Stats.Wisdom = stats[4];
        gameState.Player.Stats.Charisma = stats[5];
        
        // Update max vigor based on Constitution
        var constitutionModifier = (int)Math.Floor((stats[2] - 10) / 2.0);
        gameState.Player.Stats.MaxVigor = 10 + constitutionModifier + gameState.Player.Level;
        gameState.Player.Stats.CurrentVigor = gameState.Player.Stats.MaxVigor;
        
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task SetPlayerClass(string classId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.CharacterDetails.Class = classId;
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task<int[]> GenerateRandomStats()
    {
        await Task.Yield();
        var stats = new int[6];
        
        for (int i = 0; i < 6; i++)
        {
            // Roll 4d6 drop lowest (classic D&D method)
            var rolls = new List<int>();
            for (int j = 0; j < 4; j++)
            {
                rolls.Add(_random.Next(1, 7));
            }
            rolls.Sort();
            rolls.RemoveAt(0); // Remove lowest
            stats[i] = rolls.Sum();
        }
        
        return stats;
    }

    public async Task<int[]> GenerateStandardStats()
    {
        await Task.Yield();
        // Standard array from D&D 5e: 15, 14, 13, 12, 10, 8
        return new int[] { 15, 14, 13, 12, 10, 8 };
    }

    public async Task DamagePlayerVigor(int damage)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Stats.CurrentVigor = Math.Max(0, gameState.Player.Stats.CurrentVigor - damage);
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task HealPlayerVigor(int amount)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Stats.CurrentVigor = Math.Min(gameState.Player.Stats.MaxVigor, gameState.Player.Stats.CurrentVigor + amount);
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task HealPlayerVigorToMax()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.Stats.CurrentVigor = gameState.Player.Stats.MaxVigor;
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task LearnPlayerAbility(string ability)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (!gameState.Player.Abilities.Contains(ability))
        {
            gameState.Player.Abilities.Add(ability);
            await _gameStateRepository.SaveStateAsync(gameState);
        }
    }

    public async Task AddItemPlayerInventory(ItemInstance item)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var existingItem = gameState.Player.CharacterDetails.Inventory.FirstOrDefault(i => i.ItemId == item.ItemId);
        
        if (existingItem != null)
        {
            existingItem.Quantity += item.Quantity;
        }
        else
        {
            gameState.Player.CharacterDetails.Inventory.Add(item);
        }
        
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task RemoveItemPlayerInventory(string itemId, int quantity)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var existingItem = gameState.Player.CharacterDetails.Inventory.FirstOrDefault(i => i.ItemId == itemId);
        
        if (existingItem != null)
        {
            existingItem.Quantity = Math.Max(0, existingItem.Quantity - quantity);
            if (existingItem.Quantity == 0)
            {
                gameState.Player.CharacterDetails.Inventory.Remove(existingItem);
            }
            await _gameStateRepository.SaveStateAsync(gameState);
        }
    }

    public async Task ChangePlayerMoney(int deltaChange)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.CharacterDetails.Money = Math.Max(0, gameState.Player.CharacterDetails.Money + deltaChange);
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task ChangePlayerRenown(int deltaChange)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.CharacterDetails.GlobalRenown = Math.Max(0, Math.Min(100, gameState.Player.CharacterDetails.GlobalRenown + deltaChange));
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task ChangePlayerNotoriety(int deltaChange)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Player.CharacterDetails.GlobalNotoriety = Math.Max(0, Math.Min(100, gameState.Player.CharacterDetails.GlobalNotoriety + deltaChange));
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

    public async Task AddPokemonToTeam(string pokeId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (gameState.Player.TeamPokemon.Count < 6)
        {
            var pokemon = gameState.WorldPokemon.GetValueOrDefault(pokeId);
            if (pokemon != null)
            {
                var ownedPokemon = new OwnedPokemon
                {
                    Pokemon = pokemon,
                    Experience = 0,
                    CaughtLocationId = gameState.CurrentLocationId,
                    Friendship = 50
                };
                gameState.Player.TeamPokemon.Add(ownedPokemon);
                gameState.WorldPokemon.Remove(pokeId);
                await _gameStateRepository.SaveStateAsync(gameState);
            }
        }
    }

    public async Task AddPokemonToBox(string pokeId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var pokemon = gameState.WorldPokemon.GetValueOrDefault(pokeId);
        
        if (pokemon != null)
        {
            var ownedPokemon = new OwnedPokemon
            {
                Pokemon = pokemon,
                Experience = 0,
                CaughtLocationId = gameState.CurrentLocationId,
                Friendship = 50
            };
            gameState.Player.BoxedPokemon.Add(ownedPokemon);
            gameState.WorldPokemon.Remove(pokeId);
            await _gameStateRepository.SaveStateAsync(gameState);
        }
    }

    public async Task AddPlayerNpcRelationShipPoints(string npcId, int delta)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var currentValue = gameState.Player.PlayerNpcRelationships.GetValueOrDefault(npcId, 0);
        gameState.Player.PlayerNpcRelationships[npcId] = Math.Max(-100, Math.Min(100, currentValue + delta));
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task AddPlayerFactionRelationShipPoints(string factionId, int delta)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var currentValue = gameState.Player.PlayerFactionRelationships.GetValueOrDefault(factionId, 0);
        gameState.Player.PlayerFactionRelationships[factionId] = Math.Max(-100, Math.Min(100, currentValue + delta));
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task AddPlayerBadge(string badge)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (!gameState.Player.GymBadges.Contains(badge))
        {
            gameState.Player.GymBadges.Add(badge);
            await _gameStateRepository.SaveStateAsync(gameState);
        }
    }

    #region D&D 5e Character Management

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

    
    // D&D-specific character creation logic has been removed
    // Character creation is now handled generically via ruleset configuration

    #endregion
}
