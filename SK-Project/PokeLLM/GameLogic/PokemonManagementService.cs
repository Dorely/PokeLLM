using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLogic;

public interface IPokemonManagementService
{
    Task<string> CreatePokemonAsync(Pokemon pokemonData, string locationId = "");
    Task<Pokemon> CreatePokemon(string species, int level = 1, string locationId = "");
    Task<List<Pokemon>> GetPokemonAtLocation(string locationId);
    Task<string> MovePokemonToLocationAsync(string pokemonId, string locationId);
    Task<string> RemovePokemonFromLocationAsync(string pokemonId);
    Task<string> SetPokemonLevel(string pokemonId, int level);
    Task<Pokemon> GetPokemonDetails(string pokemonId);
    Task<string> EvolvePokemon(string pokemonId, string newSpecies);
    Task<string> ChangePokemonFriendship(string pokemonId, int friendshipChange);
    Task<string> LearnPokemonMove(string pokemonId, string moveName);
    Task<string> ForgetPokemonMove(string pokemonId, string moveName);
    Task<OwnedPokemon> GetOwnedPokemonDetails(string pokemonId);
    Task<string> AddPokemonExperience(string pokemonId, int experience);
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

    public async Task<string> CreatePokemonAsync(Pokemon pokemonData, string locationId = "")
    {
        if (pokemonData == null)
        {
            throw new ArgumentNullException(nameof(pokemonData));
        }

        if (string.IsNullOrWhiteSpace(pokemonData.Id))
        {
            throw new ArgumentException("Pokemon must have a valid ID", nameof(pokemonData));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Add Pokemon to world Pokemon collection
        gameState.WorldPokemon[pokemonData.Id] = pokemonData;

        // Add Pokemon to specified location if provided
        if (!string.IsNullOrWhiteSpace(locationId) && gameState.WorldLocations.ContainsKey(locationId))
        {
            if (!gameState.WorldLocations[locationId].PresentPokemonIds.Contains(pokemonData.Id))
            {
                gameState.WorldLocations[locationId].PresentPokemonIds.Add(pokemonData.Id);
            }
        }

        // Update save time and save
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Pokemon {pokemonData.Species} ({pokemonData.Id}) created successfully" + 
               (string.IsNullOrWhiteSpace(locationId) ? "" : $" at location {locationId}");
    }

    public async Task<Pokemon> CreatePokemon(string species, int level = 1, string locationId = "")
    {
        var pokemonId = $"pkmn_inst_{species.ToLower()}_{Guid.NewGuid().ToString()[..8]}";
        var pokemon = new Pokemon
        {
            Id = pokemonId,
            Species = species,
            Level = level,
            NickName = "",
            Stats = new Stats(),
            KnownMoves = new List<Move>(),
            Abilities = new List<string>(),
            StatusEffects = new List<string>(),
            Factions = new List<string>(),
            HeldItem = "",
            Type1 = PokemonType.Normal,
            Type2 = PokemonType.None
        };
        
        await CreatePokemonAsync(pokemon, locationId);
        return pokemon;
    }

    public async Task<List<Pokemon>> GetPokemonAtLocation(string locationId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (!gameState.WorldLocations.TryGetValue(locationId, out var location))
            return new List<Pokemon>();
        
        var pokemon = new List<Pokemon>();
        foreach (var pokemonId in location.PresentPokemonIds)
        {
            if (gameState.WorldPokemon.TryGetValue(pokemonId, out var pkmn))
                pokemon.Add(pkmn);
        }
        
        return pokemon;
    }

    public async Task<string> MovePokemonToLocationAsync(string pokemonId, string locationId)
    {
        if (string.IsNullOrWhiteSpace(pokemonId))
        {
            throw new ArgumentException("Pokemon ID cannot be null or empty", nameof(pokemonId));
        }

        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Verify Pokemon exists
        if (!gameState.WorldPokemon.ContainsKey(pokemonId))
        {
            throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found");
        }

        // Verify target location exists
        if (!gameState.WorldLocations.ContainsKey(locationId))
        {
            throw new InvalidOperationException($"Location with ID '{locationId}' not found");
        }

        // Remove Pokemon from all current locations
        foreach (var location in gameState.WorldLocations.Values)
        {
            location.PresentPokemonIds.Remove(pokemonId);
        }

        // Add Pokemon to new location
        gameState.WorldLocations[locationId].PresentPokemonIds.Add(pokemonId);

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Pokemon {pokemonId} moved to location {locationId}";
    }

    public async Task<string> RemovePokemonFromLocationAsync(string pokemonId)
    {
        if (string.IsNullOrWhiteSpace(pokemonId))
        {
            throw new ArgumentException("Pokemon ID cannot be null or empty", nameof(pokemonId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Remove Pokemon from all locations
        int locationsModified = 0;
        foreach (var location in gameState.WorldLocations.Values)
        {
            if (location.PresentPokemonIds.Remove(pokemonId))
            {
                locationsModified++;
            }
        }

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Pokemon {pokemonId} removed from {locationsModified} location(s)";
    }

    public async Task<string> SetPokemonLevel(string pokemonId, int level)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Check world Pokemon first
        if (gameState.WorldPokemon.TryGetValue(pokemonId, out var worldPokemon))
        {
            worldPokemon.Level = Math.Max(1, level);
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepository.SaveStateAsync(gameState);
            return $"Set {worldPokemon.Species} level to {level}";
        }
        
        // Check player Pokemon
        var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (playerPokemon != null)
        {
            playerPokemon.Pokemon.Level = Math.Max(1, level);
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepository.SaveStateAsync(gameState);
            return $"Set {playerPokemon.Pokemon.Species} level to {level}";
        }
        
        throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found");
    }

    public async Task<Pokemon> GetPokemonDetails(string pokemonId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Check world Pokemon first
        if (gameState.WorldPokemon.TryGetValue(pokemonId, out var worldPokemon))
        {
            return worldPokemon;
        }
        
        // Check player Pokemon
        var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (playerPokemon != null)
        {
            return playerPokemon.Pokemon;
        }
        
        return null;
    }

    public async Task<string> EvolvePokemon(string pokemonId, string newSpecies)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Check world Pokemon first
        if (gameState.WorldPokemon.TryGetValue(pokemonId, out var worldPokemon))
        {
            var oldSpecies = worldPokemon.Species;
            worldPokemon.Species = newSpecies;
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepository.SaveStateAsync(gameState);
            return $"{oldSpecies} evolved into {newSpecies}!";
        }
        
        // Check player Pokemon
        var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (playerPokemon != null)
        {
            var oldSpecies = playerPokemon.Pokemon.Species;
            playerPokemon.Pokemon.Species = newSpecies;
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepository.SaveStateAsync(gameState);
            return $"{oldSpecies} evolved into {newSpecies}!";
        }
        
        throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found");
    }

    public async Task<string> ChangePokemonFriendship(string pokemonId, int friendshipChange)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Only player Pokemon have friendship values
        var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (playerPokemon != null)
        {
            playerPokemon.Friendship = Math.Max(0, Math.Min(100, playerPokemon.Friendship + friendshipChange));
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepository.SaveStateAsync(gameState);
            return $"Changed {playerPokemon.Pokemon.Species} friendship by {friendshipChange:+#;-#;0} (now {playerPokemon.Friendship})";
        }
        
        throw new InvalidOperationException($"Player Pokemon with ID '{pokemonId}' not found");
    }

    public async Task<string> LearnPokemonMove(string pokemonId, string moveName)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        Pokemon pokemon = null;
        
        // Check world Pokemon first
        if (gameState.WorldPokemon.TryGetValue(pokemonId, out var worldPokemon))
        {
            pokemon = worldPokemon;
        }
        else
        {
            // Check player Pokemon
            var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
                .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
            if (playerPokemon != null)
            {
                pokemon = playerPokemon.Pokemon;
            }
        }
        
        if (pokemon == null)
        {
            throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found");
        }
        
        if (pokemon.KnownMoves.Count >= 4)
        {
            return $"{pokemon.Species} already knows 4 moves. Forget one first.";
        }
        
        var newMove = new Move { Id = $"move_{moveName.ToLower().Replace(" ", "_")}", Name = moveName };
        pokemon.KnownMoves.Add(newMove);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"{pokemon.Species} learned {moveName}!";
    }

    public async Task<string> ForgetPokemonMove(string pokemonId, string moveName)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        Pokemon pokemon = null;
        
        // Check world Pokemon first
        if (gameState.WorldPokemon.TryGetValue(pokemonId, out var worldPokemon))
        {
            pokemon = worldPokemon;
        }
        else
        {
            // Check player Pokemon
            var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
                .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
            if (playerPokemon != null)
            {
                pokemon = playerPokemon.Pokemon;
            }
        }
        
        if (pokemon == null)
        {
            throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found");
        }
        
        var moveToForget = pokemon.KnownMoves.FirstOrDefault(m => m.Name.Equals(moveName, StringComparison.OrdinalIgnoreCase));
        if (moveToForget == null)
        {
            return $"{pokemon.Species} doesn't know {moveName}";
        }
        
        pokemon.KnownMoves.Remove(moveToForget);
        
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"{pokemon.Species} forgot {moveName}";
    }

    public async Task<OwnedPokemon> GetOwnedPokemonDetails(string pokemonId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Only check player Pokemon for OwnedPokemon
        var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        
        return playerPokemon;
    }

    public async Task<string> AddPokemonExperience(string pokemonId, int experience)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Only player Pokemon have experience values
        var playerPokemon = gameState.Player.TeamPokemon.Concat(gameState.Player.BoxedPokemon)
            .FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (playerPokemon != null)
        {
            playerPokemon.Experience = Math.Max(0, playerPokemon.Experience + experience);
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepository.SaveStateAsync(gameState);
            return $"Added {experience} experience to {playerPokemon.Pokemon.Species} (now {playerPokemon.Experience} total)";
        }
        
        throw new InvalidOperationException($"Player Pokemon with ID '{pokemonId}' not found");
    }
}
