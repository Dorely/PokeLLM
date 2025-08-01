using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface IPokemonManagementService
{
    Task<Pokemon> GetPokemonDetails(string pokemonInstanceId);
    Task<OwnedPokemon> GetOwnedPokemonDetails(string pokemonInstanceId);
    Task<List<Pokemon>> GetWildPokemonAtLocation(string locationId);
    Task<Pokemon> CreateWildPokemon(string species, int level, string locationId);
    Task<Pokemon> CreateOwnedPokemon(string species, int level, string ownerId);
    Task SetPokemonNickname(string pokemonInstanceId, string nickname);
    Task SetPokemonStats(string pokemonInstanceId, Stats stats);
    Task DamagePokemonVigor(string pokemonInstanceId, int damage);
    Task HealPokemonVigor(string pokemonInstanceId, int amount);
    Task HealPokemonVigorToMax(string pokemonInstanceId);
    Task SetPokemonLevel(string pokemonInstanceId, int level);
    Task AddPokemonExperience(string pokemonInstanceId, int exp);
    Task LearnPokemonMove(string pokemonInstanceId, Move move);
    Task ForgetPokemonMove(string pokemonInstanceId, string moveId);
    Task SetPokemonHeldItem(string pokemonInstanceId, string itemId);
    Task RemovePokemonHeldItem(string pokemonInstanceId);
    Task AddPokemonStatusEffect(string pokemonInstanceId, string statusEffect);
    Task RemovePokemonStatusEffect(string pokemonInstanceId, string statusEffect);
    Task ClearAllPokemonStatusEffects(string pokemonInstanceId);
    Task SetPokemonFriendship(string pokemonInstanceId, int friendship);
    Task ChangePokemonFriendship(string pokemonInstanceId, int deltaChange);
    Task EvolvePokemon(string pokemonInstanceId, string newSpecies);
    Task MovePokemonToLocation(string pokemonInstanceId, string locationId);
    Task RemovePokemonFromWorld(string pokemonInstanceId);
    Task AddPokemonToFaction(string pokemonInstanceId, string factionId);
    Task RemovePokemonFromFaction(string pokemonInstanceId, string factionId);
}

/// <summary>
/// This service contains methods for managing pokemon within the game state
/// </summary>
public class PokemonManagementService : IPokemonManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    public PokemonManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<Pokemon> GetPokemonDetails(string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task<OwnedPokemon> GetOwnedPokemonDetails(string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<Pokemon>> GetWildPokemonAtLocation(string locationId)
    {
        throw new NotImplementedException();
    }

    public async Task<Pokemon> CreateWildPokemon(string species, int level, string locationId)
    {
        throw new NotImplementedException();
    }

    public async Task<Pokemon> CreateOwnedPokemon(string species, int level, string ownerId)
    {
        throw new NotImplementedException();
    }

    public async Task SetPokemonNickname(string pokemonInstanceId, string nickname)
    {
        throw new NotImplementedException();
    }

    public async Task SetPokemonStats(string pokemonInstanceId, Stats stats)
    {
        throw new NotImplementedException();
    }

    public async Task DamagePokemonVigor(string pokemonInstanceId, int damage)
    {
        throw new NotImplementedException();
    }

    public async Task HealPokemonVigor(string pokemonInstanceId, int amount)
    {
        throw new NotImplementedException();
    }

    public async Task HealPokemonVigorToMax(string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task SetPokemonLevel(string pokemonInstanceId, int level)
    {
        throw new NotImplementedException();
    }

    public async Task AddPokemonExperience(string pokemonInstanceId, int exp)
    {
        throw new NotImplementedException();
    }

    public async Task LearnPokemonMove(string pokemonInstanceId, Move move)
    {
        throw new NotImplementedException();
    }

    public async Task ForgetPokemonMove(string pokemonInstanceId, string moveId)
    {
        throw new NotImplementedException();
    }

    public async Task SetPokemonHeldItem(string pokemonInstanceId, string itemId)
    {
        throw new NotImplementedException();
    }

    public async Task RemovePokemonHeldItem(string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task AddPokemonStatusEffect(string pokemonInstanceId, string statusEffect)
    {
        throw new NotImplementedException();
    }

    public async Task RemovePokemonStatusEffect(string pokemonInstanceId, string statusEffect)
    {
        throw new NotImplementedException();
    }

    public async Task ClearAllPokemonStatusEffects(string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task SetPokemonFriendship(string pokemonInstanceId, int friendship)
    {
        throw new NotImplementedException();
    }

    public async Task ChangePokemonFriendship(string pokemonInstanceId, int deltaChange)
    {
        throw new NotImplementedException();
    }

    public async Task EvolvePokemon(string pokemonInstanceId, string newSpecies)
    {
        throw new NotImplementedException();
    }

    public async Task MovePokemonToLocation(string pokemonInstanceId, string locationId)
    {
        throw new NotImplementedException();
    }

    public async Task RemovePokemonFromWorld(string pokemonInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task AddPokemonToFaction(string pokemonInstanceId, string factionId)
    {
        throw new NotImplementedException();
    }

    public async Task RemovePokemonFromFaction(string pokemonInstanceId, string factionId)
    {
        throw new NotImplementedException();
    }
}
