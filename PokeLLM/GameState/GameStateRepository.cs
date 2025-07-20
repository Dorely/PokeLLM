using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Models;
using PokeLLM.GameState.Interfaces;

namespace PokeLLM.GameState;

public class GameStateRepository : IGameStateRepository
{
    private readonly string _gameStateDirectory;
    private readonly string _currentStateFile;
    private readonly string _backupDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameStateRepository(string dataDirectory = "GameData")
    {
        _gameStateDirectory = dataDirectory;
        _currentStateFile = Path.Combine(_gameStateDirectory, "current_state.json");
        _backupDirectory = Path.Combine(_gameStateDirectory, "backups");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        InitializeDirectories();
    }

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_gameStateDirectory);
        Directory.CreateDirectory(_backupDirectory);
    }

    public async Task<GameStateModel> CreateNewGameStateAsync(string characterName = "Trainer")
    {
        var gameState = new GameStateModel
        {
            Character = new Character
            {
                Name = characterName,
                CurrentHealth = 100,
                MaxHealth = 100
            },
            Adventure = new Adventure
            {
                CurrentLocation = "Pallet Town",
                CurrentRegion = "Kanto"
            }
        };

        await SaveStateAsync(gameState);
        return gameState;
    }

    public async Task SaveStateAsync(GameStateModel gameState)
    {
        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

        gameState.LastUpdated = DateTime.UtcNow;

        // Create backup of current state if it exists
        if (File.Exists(_currentStateFile))
        {
            await CreateBackupAsync();
        }

        var json = JsonSerializer.Serialize(gameState, _jsonOptions);
        await File.WriteAllTextAsync(_currentStateFile, json);
    }

    public async Task<GameStateModel?> LoadLatestStateAsync()
    {
        if (!File.Exists(_currentStateFile))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_currentStateFile);
            var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
            return gameState;
        }
        catch (JsonException ex)
        {
            // Try to load from most recent backup
            Console.WriteLine($"Error loading current state: {ex.Message}. Attempting to load from backup.");
            return await LoadLatestBackupAsync();
        }
    }

    public async Task<GameStateModel?> LoadStateByIdAsync(string stateId)
    {
        var backupFiles = Directory.GetFiles(_backupDirectory, "*.json")
            .OrderByDescending(f => File.GetCreationTime(f));

        foreach (var file in backupFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
                if (gameState?.Id == stateId)
                    return gameState;
            }
            catch (JsonException)
            {
                // Skip corrupted files
                continue;
            }
        }

        return null;
    }

    public async Task<List<GameStateModel>> GetAllStatesAsync(int limit = 50)
    {
        var states = new List<GameStateModel>();

        // Add current state if it exists
        var currentState = await LoadLatestStateAsync();
        if (currentState != null)
            states.Add(currentState);

        // Add backup states
        var backupFiles = Directory.GetFiles(_backupDirectory, "*.json")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Take(limit - (currentState != null ? 1 : 0));

        foreach (var file in backupFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
                if (gameState != null && (currentState == null || gameState.Id != currentState.Id))
                    states.Add(gameState);
            }
            catch (JsonException)
            {
                // Skip corrupted files
                continue;
            }
        }

        return states.OrderByDescending(s => s.LastUpdated).ToList();
    }

    public async Task DeleteStateAsync(string stateId)
    {
        // Don't delete current state directly, just remove backups
        var backupFiles = Directory.GetFiles(_backupDirectory, "*.json");

        foreach (var file in backupFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
                if (gameState?.Id == stateId)
                {
                    File.Delete(file);
                    break;
                }
            }
            catch (JsonException)
            {
                // Skip corrupted files
                continue;
            }
        }
    }

    private async Task CreateBackupAsync()
    {
        if (!File.Exists(_currentStateFile))
            return;

        try
        {
            var currentJson = await File.ReadAllTextAsync(_currentStateFile);
            var currentState = JsonSerializer.Deserialize<GameStateModel>(currentJson, _jsonOptions);
            
            if (currentState != null)
            {
                var backupFileName = $"gamestate_{currentState.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);
                await File.WriteAllTextAsync(backupPath, currentJson);

                // Clean old backups (keep only last 100)
                await CleanOldBackupsAsync();
            }
        }
        catch (JsonException)
        {
            // If current state is corrupted, just skip backup creation
        }
    }

    private async Task<GameStateModel?> LoadLatestBackupAsync()
    {
        var backupFiles = Directory.GetFiles(_backupDirectory, "*.json")
            .OrderByDescending(f => File.GetCreationTime(f));

        foreach (var file in backupFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
                if (gameState != null)
                    return gameState;
            }
            catch (JsonException)
            {
                // Skip corrupted files
                continue;
            }
        }

        return null;
    }

    private async Task CleanOldBackupsAsync()
    {
        await Task.Run(() =>
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Skip(100); // Keep the 100 most recent backups

            foreach (var file in backupFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        });
    }

    // Helper methods for common operations
    public async Task UpdateCharacterAsync(Action<Character> updateAction)
    {
        var state = await LoadLatestStateAsync();
        if (state != null)
        {
            updateAction(state.Character);
            await SaveStateAsync(state);
        }
    }

    public async Task UpdateAdventureAsync(Action<Adventure> updateAction)
    {
        var state = await LoadLatestStateAsync();
        if (state != null)
        {
            updateAction(state.Adventure);
            await SaveStateAsync(state);
        }
    }

    public async Task AddPokemonToTeamAsync(Pokemon pokemon)
    {
        await UpdateCharacterAsync(character =>
        {
            if (character.PokemonTeam.Count < 6)
            {
                character.PokemonTeam.Add(pokemon);
            }
            else
            {
                character.StoredPokemon.Add(pokemon);
            }
        });
    }

    public async Task AddEventToHistoryAsync(GameEvent gameEvent)
    {
        await UpdateAdventureAsync(adventure =>
        {
            adventure.EventHistory.Add(gameEvent);
            
            // Keep only the last 1000 events to prevent excessive memory usage
            if (adventure.EventHistory.Count > 1000)
            {
                adventure.EventHistory = adventure.EventHistory
                    .OrderByDescending(e => e.Timestamp)
                    .Take(1000)
                    .ToList();
            }
        });
    }

    public async Task<bool> HasGameStateAsync()
    {
        return File.Exists(_currentStateFile);
    }
}