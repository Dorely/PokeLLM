using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}

/// <summary>
/// This service contains methods for managing character data within the gamestate
/// </summary>
public class CharacterManagementService : ICharacterManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    public CharacterManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<PlayerState> GetPlayerDetails()
    {
        throw new NotImplementedException();
    }

    public async Task SetPlayerName(string playerName)
    {
        throw new NotImplementedException();
    }

    public async Task SetPlayerStats(int[] stats)
    {
        throw new NotImplementedException();
    }

    public async Task SetPlayerClass(string classId)
    {
        throw new NotImplementedException();
    }

    public async Task DamagePlayerVigor(int damage)
    {
        throw new NotImplementedException();
    }

    public async Task HealPlayerVigor(int amount)
    {
        throw new NotImplementedException();
    }

    public async Task HealPlayerVigorToMax()
    {
        throw new NotImplementedException();
    }

    public async Task LearnPlayerAbility(string ability)
    {
        throw new NotImplementedException();
    }

    public async Task AddItemPlayerInventory(ItemInstance item)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveItemPlayerInventory(string itemId, int quantity)
    {
        throw new NotImplementedException();
    }

    public async Task ChangePlayerMoney(int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task ChangePlayerRenown(int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task ChangePlayerNotoriety(int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task SetPlayerDescription(string description)
    {
        throw new NotImplementedException();
    }

    //This method will increment experience and then calculate if they can level up.
    //Returning true if the character is ready for level up
    public async Task<bool> AddPlayerExperiencePoints(int exp)
    {
        throw new NotImplementedException();
    }

    public async Task SetPlayerCondition(string condition)
    {
        throw new NotImplementedException();
    }

    public async Task AddPokemonToTeam(string pokeId)
    {
        throw new NotImplementedException();
    }

    public async Task AddPokemonToBox(string pokeId)
    {
        throw new NotImplementedException();
    }

    public async Task AddPlayerNpcRelationShipPoints(string npcId, int delta)
    {
        throw new NotImplementedException();
    }

    public async Task AddPlayerFactionRelationShipPoints(string factionId, int delta)
    {
        throw new NotImplementedException();
    }

    public async Task AddPlayerBadge(string badge)
    {
        throw new NotImplementedException();
    }

}
