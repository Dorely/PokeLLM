using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

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

    public async Task<GameStateModel> CreateNewGameStateAsync(string trainerName = "Trainer")
    {
        var gameState = new GameStateModel
        {
            Player = new PlayerState
            {
                Character = new Character
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = trainerName,
                    Level = 1,
                    Money = 1000, // Starting money
                    Stats = new Stats
                    {
                        Power = new Stat { Type = StatType.Power, Level = StatLevel.Novice },
                        Speed = new Stat { Type = StatType.Speed, Level = StatLevel.Novice },
                        Mind = new Stat { Type = StatType.Mind, Level = StatLevel.Novice },
                        Charm = new Stat { Type = StatType.Charm, Level = StatLevel.Novice },
                        Defense = new Stat { Type = StatType.Defense, Level = StatLevel.Novice },
                        Spirit = new Stat { Type = StatType.Spirit, Level = StatLevel.Novice }
                    },
                    Conditions = new List<ActiveCondition>(),
                    Inventory = new Dictionary<string, int>
                    {
                        ["Pokeball"] = 5,
                        ["Potion"] = 2
                    },
                    GlobalRenown = 0,
                    GlobalNotoriety = 0,
                    Faction = "Player",
                    IsTrainer = true,
                    PokemonTeam = new PokemonTeam
                    {
                        ActivePokemon = new List<OwnedPokemon>(),
                        BoxedPokemon = new List<OwnedPokemon>(),
                        MaxPartySize = 6
                    }
                },
                Experience = 0,
                AvailableStatPoints = 1, // Start with 1 free point to allocate
                CharacterCreationComplete = false // Character creation not yet complete
            },
            WorldState = new GameWorldState
            {
                CurrentLocation = "unknown",
                CurrentRegion = "unknown",
                ActiveNpcPokemon = new List<Pokemon>(),
                ActiveNpcs = new List<Character>(),
                VisitedLocations = new HashSet<string>(),
                GymBadges = new List<GymBadge>(),
                WorldFlags = new Dictionary<string, object>(),
                NPCRelationships = new Dictionary<string, int>(),
                FactionReputations = new Dictionary<string, int>(),
                DiscoveredLore = new HashSet<string>(),
                TimeOfDay = TimeOfDay.Morning,
                WeatherCondition = "Clear"
            },
            BattleState = null // No active battle initially
        };

        await SaveStateAsync(gameState);
        return gameState;
    }

    public async Task SaveStateAsync(GameStateModel gameState)
    {
        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

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
                if (gameState?.GetHashCode().ToString() == stateId) // Simple ID based on hash
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
                if (gameState != null)
                    states.Add(gameState);
            }
            catch (JsonException)
            {
                // Skip corrupted files
                continue;
            }
        }

        return states.Take(limit).ToList();
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
                if (gameState?.GetHashCode().ToString() == stateId)
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
                var backupFileName = $"gamestate_{currentState.GetHashCode()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
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
    public async Task UpdatePlayerAsync(Action<PlayerState> updateAction)
    {
        var state = await LoadLatestStateAsync();
        if (state != null)
        {
            updateAction(state.Player);
            await SaveStateAsync(state);
        }
    }

    public async Task UpdateWorldStateAsync(Action<GameWorldState> updateAction)
    {
        var state = await LoadLatestStateAsync();
        if (state != null)
        {
            updateAction(state.WorldState);
            await SaveStateAsync(state);
        }
    }

    public async Task AddPokemonToTeamAsync(OwnedPokemon pokemon)
    {
        var state = await LoadLatestStateAsync();
        if (state != null)
        {
            if (state.Player.Character.PokemonTeam.ActivePokemon.Count < state.Player.Character.PokemonTeam.MaxPartySize)
            {
                state.Player.Character.PokemonTeam.ActivePokemon.Add(pokemon);
            }
            else
            {
                state.Player.Character.PokemonTeam.BoxedPokemon.Add(pokemon);
            }
            await SaveStateAsync(state);
        }
    }

    public async Task<bool> HasGameStateAsync()
    {
        return File.Exists(_currentStateFile);
    }

    #region Battle State Management

    public async Task UpdateBattleStateAsync(Action<BattleState> updateAction)
    {
        var gameState = await LoadLatestStateAsync();
        if (gameState?.BattleState != null)
        {
            updateAction(gameState.BattleState);
            await SaveStateAsync(gameState);
        }
    }

    public async Task<bool> HasActiveBattleAsync()
    {
        var gameState = await LoadLatestStateAsync();
        return gameState?.BattleState?.IsActive == true;
    }

    public async Task StartBattleAsync(BattleState battleState)
    {
        var gameState = await LoadLatestStateAsync();
        if (gameState != null)
        {
            battleState.IsActive = true;
            gameState.BattleState = battleState;
            await SaveStateAsync(gameState);
        }
    }

    public async Task EndBattleAsync()
    {
        var gameState = await LoadLatestStateAsync();
        if (gameState?.BattleState != null)
        {
            gameState.BattleState.IsActive = false;
            // Optionally clear the battle state or keep it for reference
            // gameState.BattleState = null;
            await SaveStateAsync(gameState);
        }
    }

    #endregion
}