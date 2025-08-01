using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface INpcManagementService
{
    Task<Npc> GetNpcDetails(string npcId);
    Task<List<Npc>> GetNpcsAtLocation(string locationId);
    Task<Npc> CreateNpc(string name, string characterClass, string locationId);
    Task SetNpcName(string npcId, string name);
    Task SetNpcStats(string npcId, Stats stats);
    Task SetNpcClass(string npcId, string characterClass);
    Task SetNpcDescription(string npcId, string description);
    Task SetNpcTrainerStatus(string npcId, bool isTrainer);
    Task AddNpcPokemon(string npcId, string pokemonInstanceId);
    Task RemoveNpcPokemon(string npcId, string pokemonInstanceId);
    Task AddItemToNpcInventory(string npcId, ItemInstance item);
    Task RemoveItemFromNpcInventory(string npcId, string itemId, int quantity);
    Task ChangeNpcMoney(string npcId, int deltaChange);
    Task ChangeNpcRenown(string npcId, int deltaChange);
    Task ChangeNpcNotoriety(string npcId, int deltaChange);
    Task MoveNpcToLocation(string npcId, string locationId);
    Task RemoveNpcFromLocation(string npcId);
    Task AddNpcToFaction(string npcId, string factionId);
    Task RemoveNpcFromFaction(string npcId, string factionId);
    Task SetNpcDialogue(string npcId, string dialogueKey);
    Task SetNpcBehavior(string npcId, string behaviorPattern);
    Task UpdateNpcRelationshipWithPlayer(string npcId, int deltaChange);
    Task SetNpcHostility(string npcId, bool isHostile);
    Task DamageNpcVigor(string npcId, int damage);
    Task HealNpcVigor(string npcId, int amount);
    Task HealNpcVigorToMax(string npcId);
}

/// <summary>
/// This service contains methods for managing NPCs within the game state
/// </summary>
public class NpcManagementService : INpcManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    public NpcManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<Npc> GetNpcDetails(string npcId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<Npc>> GetNpcsAtLocation(string locationId)
    {
        throw new NotImplementedException();
    }

    public async Task<Npc> CreateNpc(string name, string characterClass, string locationId)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcName(string npcId, string name)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcStats(string npcId, Stats stats)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcClass(string npcId, string characterClass)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcDescription(string npcId, string description)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcTrainerStatus(string npcId, bool isTrainer)
    {
        throw new NotImplementedException();
    }

    public async Task AddNpcPokemon(string npcId, string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveNpcPokemon(string npcId, string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task AddItemToNpcInventory(string npcId, ItemInstance item)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveItemFromNpcInventory(string npcId, string itemId, int quantity)
    {
        throw new NotImplementedException();
    }

    public async Task ChangeNpcMoney(string npcId, int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task ChangeNpcRenown(string npcId, int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task ChangeNpcNotoriety(string npcId, int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task MoveNpcToLocation(string npcId, string locationId)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveNpcFromLocation(string npcId)
    {
        throw new NotImplementedException();
    }

    public async Task AddNpcToFaction(string npcId, string factionId)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveNpcFromFaction(string npcId, string factionId)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcDialogue(string npcId, string dialogueKey)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcBehavior(string npcId, string behaviorPattern)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateNpcRelationshipWithPlayer(string npcId, int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task SetNpcHostility(string npcId, bool isHostile)
    {
        throw new NotImplementedException();
    }

    public async Task DamageNpcVigor(string npcId, int damage)
    {
        throw new NotImplementedException();
    }

    public async Task HealNpcVigor(string npcId, int amount)
    {
        throw new NotImplementedException();
    }

    public async Task HealNpcVigorToMax(string npcId)
    {
        throw new NotImplementedException();
    }
}
