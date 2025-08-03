using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLogic;

public interface IPlayerPokemonManagementService
{
    Task<string> AddPokemonToTeamAsync(string pokemonId);
    Task<string> MovePokemonToBoxAsync(string pokemonId);
    Task<string> SwapPokemonPositionsAsync(string pokemonId, int teamSlot);
    Task<string> ReleasePokemonAsync(string pokemonId);
    Task<string> UpdatePokemonFriendshipAsync(string pokemonId, int amount);
    Task<string> UpdatePokemonExperienceAsync(string pokemonId, int amount);
    Task<string> EvolvePokemonAsync(string pokemonId, string evolutionSpecies);
    Task<string> TeachPokemonMoveAsync(string pokemonId, string moveName);
    Task<string> ForgetPokemonMoveAsync(string pokemonId, string moveName);
    Task<string> ChangePokemonNicknameAsync(string pokemonId, string nickname);
    Task<List<OwnedPokemon>> GetTeamPokemonAsync();
    Task<List<OwnedPokemon>> GetBoxedPokemonAsync();
    Task<OwnedPokemon> GetPokemonDetailsAsync(string pokemonId);
}

/// <summary>
/// This service contains methods for managing the player's Pokemon team and PC box
/// </summary>
public class PlayerPokemonManagementService : IPlayerPokemonManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    
    public PlayerPokemonManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<string> AddPokemonToTeamAsync(string pokemonId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (gameState.Player.TeamPokemon.Count >= 6)
        {
            return "Team is full (6 Pokemon maximum)";
        }
        
        // Find Pokemon in boxed collection
        var boxedPokemon = gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (boxedPokemon == null)
        {
            return $"Pokemon {pokemonId} not found in PC box";
        }
        
        gameState.Player.BoxedPokemon.Remove(boxedPokemon);
        gameState.Player.TeamPokemon.Add(boxedPokemon);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Added {boxedPokemon.Pokemon.Species} to team";
    }

    public async Task<string> MovePokemonToBoxAsync(string pokemonId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var teamPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (teamPokemon == null)
        {
            return $"Pokemon {pokemonId} not found in team";
        }
        
        gameState.Player.TeamPokemon.Remove(teamPokemon);
        gameState.Player.BoxedPokemon.Add(teamPokemon);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Moved {teamPokemon.Pokemon.Species} to PC box";
    }

    public async Task<string> SwapPokemonPositionsAsync(string pokemonId, int teamSlot)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (teamSlot < 0 || teamSlot >= gameState.Player.TeamPokemon.Count)
        {
            return "Invalid team slot";
        }
        
        var currentPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (currentPokemon == null)
        {
            return $"Pokemon {pokemonId} not found in team";
        }
        
        var currentIndex = gameState.Player.TeamPokemon.IndexOf(currentPokemon);
        var targetPokemon = gameState.Player.TeamPokemon[teamSlot];
        
        gameState.Player.TeamPokemon[currentIndex] = targetPokemon;
        gameState.Player.TeamPokemon[teamSlot] = currentPokemon;
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Swapped positions: {currentPokemon.Pokemon.Species} ? {targetPokemon.Pokemon.Species}";
    }

    public async Task<string> ReleasePokemonAsync(string pokemonId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var allPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon).ToList();
        var pokemonToRelease = allPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemonToRelease == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        gameState.Player.TeamPokemon.Remove(pokemonToRelease);
        gameState.Player.BoxedPokemon.Remove(pokemonToRelease);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Released {pokemonToRelease.Pokemon.Species}";
    }

    public async Task<string> UpdatePokemonFriendshipAsync(string pokemonId, int amount)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var pokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemon == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        pokemon.Friendship = Math.Max(0, Math.Min(100, pokemon.Friendship + amount));
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Updated {pokemon.Pokemon.Species} friendship by {amount:+#;-#;0} (now {pokemon.Friendship})";
    }

    public async Task<string> UpdatePokemonExperienceAsync(string pokemonId, int amount)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var pokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemon == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        pokemon.Experience = Math.Max(0, pokemon.Experience + amount);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Added {amount} experience to {pokemon.Pokemon.Species} (now {pokemon.Experience} total)";
    }

    public async Task<string> EvolvePokemonAsync(string pokemonId, string evolutionSpecies)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var pokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemon == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        var oldSpecies = pokemon.Pokemon.Species;
        pokemon.Pokemon.Species = evolutionSpecies;
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"{oldSpecies} evolved into {evolutionSpecies}!";
    }

    public async Task<string> TeachPokemonMoveAsync(string pokemonId, string moveName)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var pokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemon == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        if (pokemon.Pokemon.KnownMoves.Count >= 4)
        {
            return "Pokemon already knows 4 moves. Forget one first.";
        }
        
        var newMove = new Move { Id = $"move_{moveName.ToLower().Replace(" ", "_")}", Name = moveName };
        pokemon.Pokemon.KnownMoves.Add(newMove);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"{pokemon.Pokemon.Species} learned {moveName}!";
    }

    public async Task<string> ForgetPokemonMoveAsync(string pokemonId, string moveName)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var pokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemon == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        var moveToForget = pokemon.Pokemon.KnownMoves.FirstOrDefault(m => m.Name.Equals(moveName, StringComparison.OrdinalIgnoreCase));
        if (moveToForget == null)
        {
            return $"Pokemon doesn't know {moveName}";
        }
        
        pokemon.Pokemon.KnownMoves.Remove(moveToForget);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"{pokemon.Pokemon.Species} forgot {moveName}";
    }

    public async Task<string> ChangePokemonNicknameAsync(string pokemonId, string nickname)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        var pokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (pokemon == null)
        {
            return $"Pokemon {pokemonId} not found";
        }
        
        pokemon.Pokemon.NickName = nickname;
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Changed {pokemon.Pokemon.Species} nickname to {nickname}";
    }

    public async Task<List<OwnedPokemon>> GetTeamPokemonAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return gameState.Player.TeamPokemon;
    }

    public async Task<List<OwnedPokemon>> GetBoxedPokemonAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return gameState.Player.BoxedPokemon;
    }

    public async Task<OwnedPokemon> GetPokemonDetailsAsync(string pokemonId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
    }
}