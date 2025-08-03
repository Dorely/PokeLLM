using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLogic;

public interface IWorldManagementService
{
    Task<string> SetRegionAsync(string regionName);
    Task<string> CreateLocationAsync(Location locationData);
    Task<string> AddNpcToLocationAsync(string locationId, string npcId);
    Task<string> AddPokemonToLocationAsync(string locationId, string pokemonId);
    Task<string> RemoveNpcFromLocationAsync(string locationId, string npcId);
    Task<string> RemovePokemonFromLocationAsync(string locationId, string pokemonId);
    Task<string> UpdateAdventureSummaryAsync(string newSummary);
    Task<string> AddRecentEventAsync(string eventDescription);
    Task<string> SetTimeOfDayAsync(TimeOfDay timeOfDay);
    Task<string> SetWeatherAsync(Weather weather);
    Task<string> AdvanceTimeOfDayAsync();
    Task<Location> GetPlayerCurrentLocationAsync();
    Task<Location> GetLocationDetailsAsync(string locationId);
    Task MovePlayerToLocationAsync(string locationId);
    Task ChangeWeatherAsync(Weather weather);
}

/// <summary>
/// This service contains methods for managing world state, locations, and environmental aspects of the game
/// </summary>
public class WorldManagementService : IWorldManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    
    public WorldManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<string> SetRegionAsync(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName))
        {
            throw new ArgumentException("Region name cannot be null or empty", nameof(regionName));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Region = regionName.Trim();
        
        // Update the save time
        gameState.LastSaveTime = DateTime.UtcNow;
        
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Region successfully set to: {regionName}";
    }

    public async Task<string> CreateLocationAsync(Location locationData)
    {
        if (locationData == null)
        {
            throw new ArgumentNullException(nameof(locationData));
        }

        if (string.IsNullOrWhiteSpace(locationData.Id))
        {
            throw new ArgumentException("Location must have a valid ID", nameof(locationData));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // Add or update the location in the world locations dictionary
        gameState.WorldLocations[locationData.Id] = locationData;
        
        // Update the save time
        gameState.LastSaveTime = DateTime.UtcNow;
        
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Location {locationData.Name} ({locationData.Id}) created successfully";
    }

    public async Task<string> AddNpcToLocationAsync(string locationId, string npcId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));
        }

        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (!gameState.WorldLocations.ContainsKey(locationId))
        {
            throw new InvalidOperationException($"Location with ID '{locationId}' not found");
        }

        if (!gameState.WorldNpcs.ContainsKey(npcId))
        {
            throw new InvalidOperationException($"NPC with ID '{npcId}' not found");
        }

        var location = gameState.WorldLocations[locationId];
        if (!location.PresentNpcIds.Contains(npcId))
        {
            location.PresentNpcIds.Add(npcId);
        }

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcId} added to location {locationId}";
    }

    public async Task<string> AddPokemonToLocationAsync(string locationId, string pokemonId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));
        }

        if (string.IsNullOrWhiteSpace(pokemonId))
        {
            throw new ArgumentException("Pokemon ID cannot be null or empty", nameof(pokemonId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (!gameState.WorldLocations.ContainsKey(locationId))
        {
            throw new InvalidOperationException($"Location with ID '{locationId}' not found");
        }

        if (!gameState.WorldPokemon.ContainsKey(pokemonId))
        {
            throw new InvalidOperationException($"Pokemon with ID '{pokemonId}' not found");
        }

        var location = gameState.WorldLocations[locationId];
        if (!location.PresentPokemonIds.Contains(pokemonId))
        {
            location.PresentPokemonIds.Add(pokemonId);
        }

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Pokemon {pokemonId} added to location {locationId}";
    }

    public async Task<string> RemoveNpcFromLocationAsync(string locationId, string npcId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));
        }

        if (string.IsNullOrWhiteSpace(npcId))
        {
            throw new ArgumentException("NPC ID cannot be null or empty", nameof(npcId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (!gameState.WorldLocations.ContainsKey(locationId))
        {
            throw new InvalidOperationException($"Location with ID '{locationId}' not found");
        }

        var location = gameState.WorldLocations[locationId];
        location.PresentNpcIds.Remove(npcId);

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"NPC {npcId} removed from location {locationId}";
    }

    public async Task<string> RemovePokemonFromLocationAsync(string locationId, string pokemonId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));
        }

        if (string.IsNullOrWhiteSpace(pokemonId))
        {
            throw new ArgumentException("Pokemon ID cannot be null or empty", nameof(pokemonId));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        if (!gameState.WorldLocations.ContainsKey(locationId))
        {
            throw new InvalidOperationException($"Location with ID '{locationId}' not found");
        }

        var location = gameState.WorldLocations[locationId];
        location.PresentPokemonIds.Remove(pokemonId);

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);

        return $"Pokemon {pokemonId} removed from location {locationId}";
    }

    public async Task<string> UpdateAdventureSummaryAsync(string newSummary)
    {
        if (string.IsNullOrWhiteSpace(newSummary))
        {
            throw new ArgumentException("Adventure summary cannot be null or empty", nameof(newSummary));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.AdventureSummary = newSummary;
        gameState.LastSaveTime = DateTime.UtcNow;
        
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return "Adventure summary updated successfully";
    }

    public async Task<string> AddRecentEventAsync(string eventDescription)
    {
        if (string.IsNullOrWhiteSpace(eventDescription))
        {
            throw new ArgumentException("Event description cannot be null or empty", nameof(eventDescription));
        }

        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        gameState.RecentEvents.Add(new EventLog
        {
            TurnNumber = gameState.GameTurnNumber,
            EventDescription = eventDescription
        });

        // Keep only the most recent 20 events
        if (gameState.RecentEvents.Count > 20)
        {
            gameState.RecentEvents = gameState.RecentEvents
                .OrderByDescending(e => e.TurnNumber)
                .Take(10)
                .ToList();
        }

        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return "Recent event added successfully";
    }

    public async Task<string> SetTimeOfDayAsync(TimeOfDay timeOfDay)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.TimeOfDay = timeOfDay;
        gameState.LastSaveTime = DateTime.UtcNow;
        
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Time of day set to: {timeOfDay}";
    }

    public async Task<string> SetWeatherAsync(Weather weather)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.Weather = weather;
        gameState.LastSaveTime = DateTime.UtcNow;
        
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Weather set to: {weather}";
    }

    public async Task<string> AdvanceTimeOfDayAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var currentTime = gameState.TimeOfDay ?? TimeOfDay.Morning;
        
        var nextTime = currentTime switch
        {
            TimeOfDay.Dawn => TimeOfDay.Morning,
            TimeOfDay.Morning => TimeOfDay.Day,
            TimeOfDay.Day => TimeOfDay.Afternoon,
            TimeOfDay.Afternoon => TimeOfDay.Dusk,
            TimeOfDay.Dusk => TimeOfDay.Night,
            TimeOfDay.Night => TimeOfDay.Dawn,
            _ => TimeOfDay.Morning
        };
        
        gameState.TimeOfDay = nextTime;
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
        
        return $"Time advanced from {currentTime} to {nextTime}";
    }

    public async Task<Location> GetPlayerCurrentLocationAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (string.IsNullOrEmpty(gameState.CurrentLocationId))
            return null;
        
        if (gameState.WorldLocations.TryGetValue(gameState.CurrentLocationId, out var location))
            return location;
        
        return null;
    }

    public async Task<Location> GetLocationDetailsAsync(string locationId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        if (gameState.WorldLocations.TryGetValue(locationId, out var location))
            return location;
        
        return null;
    }

    public async Task MovePlayerToLocationAsync(string locationId)
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.CurrentLocationId = locationId;
        gameState.LastSaveTime = DateTime.UtcNow;
        await _gameStateRepository.SaveStateAsync(gameState);
    }

    public async Task ChangeWeatherAsync(Weather weather)
    {
        await SetWeatherAsync(weather);
    }
}
