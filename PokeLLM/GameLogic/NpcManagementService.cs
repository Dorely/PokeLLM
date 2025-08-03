using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLogic;

public interface INpcManagementService
{
    Task<string> CreateNpcAsync(Npc npcData, string locationId = "");
    Task<string> AssignPokemonToNpcAsync(string npcId, string pokemonId);
    Task<string> MoveNpcToLocationAsync(string npcId, string locationId);
    Task<string> RemoveNpcFromLocationAsync(string npcId);
    Task<string> UpdateNpcRelationshipWithPlayerAsync(string npcId, int relationshipValue);
    Task<string> AddNpcToFactionAsync(string npcId, string factionId);
    Task<string> RemoveNpcFromFactionAsync(string npcId, string factionId);
    Task<Npc> CreateNpc(string name, string characterClass, string locationId);
    Task<Npc> GetNpcDetails(string npcId);
    Task<List<Npc>> GetNpcsAtLocation(string locationId);
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

    public async Task<string> CreateNpcAsync(Npc npcData, string locationId = "")
    {
        if (npcData == null)
        {
            throw new ArgumentNullException(nameof(npcData));
        }

        if (string.IsNullOrWhiteSpace(npcData.Id))
        {
            throw new ArgumentException("NPC must have a valid ID", nameof(npcData));
        }

        // Load current game state
        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Add NPC to world NPCs collection
        gameState.WorldNpcs[npcData.Id] = npcData;

        // Add NPC to specified location if provided
        if (!string.IsNullOrWhiteSpace(locationId) && gameState.WorldLocations.ContainsKey(locationId))
        {
            if (!gameState.WorldLocations[locationId].PresentNpcIds.Contains(npcData.Id))
            {
                gameState.WorldLocations[locationId].PresentNpcIds.Add(npcData.Id);
            }
        }

        // Update save time and save
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcData.Name} ({npcData.Id}) created successfully" + 
               (string.IsNullOrWhiteSpace(locationId) ? "" : $" at location {locationId}");
    }

    public async Task<string> AssignPokemonToNpcAsync(string npcId, string pokemonId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        if (string.IsNullOrWhiteSpace(pokemonId))
        {
            throw new ArgumentException("Pokemon ID cannot be null or empty", nameof(pokemonId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Verify NPC exists
        if (!gameState.WorldNpcs.ContainsKey(npcId))
        {
            throw new InvalidOperationException($"NPC with ID '{npcId}' not found in game state");
        }

        // Verify Pokemon exists
        if (!gameState.WorldPokemon.ContainsKey(pokemonId))
        {
            throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found in game state");
        }

        // Add Pokemon to NPC's owned Pokemon list
        var npc = gameState.WorldNpcs[npcId];
        if (!npc.PokemonOwned.Contains(pokemonId))
        {
            npc.PokemonOwned.Add(pokemonId);
        }

        // Update save time and save
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Pokemon {pokemonId} assigned to NPC {npcId} successfully";
    }

    public async Task<string> MoveNpcToLocationAsync(string npcId, string locationId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Verify NPC exists
        if (!gameState.WorldNpcs.ContainsKey(npcId))
        {
            throw new InvalidOperationException($"NPC with ID '{npcId}' not found");
        }

        // Verify target location exists
        if (!gameState.WorldLocations.ContainsKey(locationId))
        {
            throw new InvalidOperationException($"Location with ID '{locationId}' not found");
        }

        // Remove NPC from all current locations
        foreach (var location in gameState.WorldLocations.Values)
        {
            location.PresentNpcIds.Remove(npcId);
        }

        // Add NPC to new location
        gameState.WorldLocations[locationId].PresentNpcIds.Add(npcId);

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcId} moved to location {locationId}";
    }

    public async Task<string> RemoveNpcFromLocationAsync(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Remove NPC from all locations
        int locationsModified = 0;
        foreach (var location in gameState.WorldLocations.Values)
        {
            if (location.PresentNpcIds.Remove(npcId))
            {
                locationsModified++;
            }
        }

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcId} removed from {locationsModified} location(s)";
    }

    public async Task<string> UpdateNpcRelationshipWithPlayerAsync(string npcId, int relationshipValue)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Verify NPC exists
        if (!gameState.WorldNpcs.ContainsKey(npcId))
        {
            throw new InvalidOperationException($"NPC with ID '{npcId}' not found");
        }

        // Clamp relationship value to valid range (-100 to 100)
        relationshipValue = Math.Max(-100, Math.Min(100, relationshipValue));

        // Update or add relationship
        gameState.Player.PlayerNpcRelationships[npcId] = relationshipValue;

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Player relationship with NPC {npcId} set to {relationshipValue}";
    }

    public async Task<string> AddNpcToFactionAsync(string npcId, string factionId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            throw new ArgumentException("Faction ID cannot be null or empty", nameof(factionId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Verify NPC exists
        if (!gameState.WorldNpcs.ContainsKey(npcId))
        {
            throw new InvalidOperationException($"NPC with ID '{npcId}' not found");
        }

        var npc = gameState.WorldNpcs[npcId];
        if (!npc.Factions.Contains(factionId))
        {
            npc.Factions.Add(factionId);
        }

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcId} added to faction {factionId}";
    }

    public async Task<string> RemoveNpcFromFactionAsync(string npcId, string factionId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            throw new ArgumentException("Faction ID cannot be null or empty", nameof(factionId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Verify NPC exists
        if (!gameState.WorldNpcs.ContainsKey(npcId))
        {
            throw new InvalidOperationException($"NPC with ID '{npcId}' not found");
        }

        var npc = gameState.WorldNpcs[npcId];
        npc.Factions.Remove(factionId);

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcId} removed from faction {factionId}";
    }

    public async Task<Npc> CreateNpc(string name, string characterClass, string locationId)
    {
        var npcId = $"char_{name.ToLower().Replace(" ", "_")}_{Guid.NewGuid().ToString()[..8]}";
        var npc = new Npc
        {
            Id = npcId,
            Name = name,
            CharacterDetails = new CharacterDetails { Class = characterClass },
            Stats = new Stats(),
            IsTrainer = false,
            PokemonOwned = new List<string>(),
            Factions = new List<string>()
        };
        
        await CreateNpcAsync(npc, locationId);
        return npc;
    }

    public async Task<Npc> GetNpcDetails(string npcId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (gameState.WorldNpcs.TryGetValue(npcId, out var npc))
            return npc;
        
        return null;
    }

    public async Task<List<Npc>> GetNpcsAtLocation(string locationId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (!gameState.WorldLocations.TryGetValue(locationId, out var location))
            return new List<Npc>();
        
        var npcs = new List<Npc>();
        foreach (var npcId in location.PresentNpcIds)
        {
            if (gameState.WorldNpcs.TryGetValue(npcId, out var npc))
                npcs.Add(npc);
        }
        
        return npcs;
    }
}
