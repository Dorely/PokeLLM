using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Models;
using PokeLLM.GameRules.Interfaces;

namespace PokeLLM.GameState;

public static class GameStorageHelper
{
    public static string GetUserGamesDirectory()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "PokeLLM", "Games");
    }

    public static string GenerateGameId(string rulesetId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var cleanRulesetId = string.Join("", rulesetId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        return $"{cleanRulesetId}_{timestamp}";
    }

    public static string GetGameDirectory(string gameId)
    {
        return Path.Combine(GetUserGamesDirectory(), gameId);
    }
}

public interface IGameStateRepository
{
    Task<GameStateModel> CreateNewGameStateAsync();
    Task<GameStateModel> CreateNewGameStateAsync(string rulesetId);
    Task SaveStateAsync(GameStateModel gameState);
    Task<GameStateModel> LoadLatestStateAsync();
    Task<bool> HasGameStateAsync();
    Task CopyRulesetToGameDirectoryAsync(string rulesetId, string gameDirectory);
    Task SaveRulesetChangesAsync(string gameDirectory, string rulesetJson);
}
public class GameStateRepository : IGameStateRepository
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IRulesetManager _rulesetManager;
    private string _currentGameDirectory;
    private string _currentGameStateFile;

    public GameStateRepository(IRulesetManager rulesetManager)
    {
        _rulesetManager = rulesetManager;
        
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
        var userGamesDir = GameStorageHelper.GetUserGamesDirectory();
        Directory.CreateDirectory(userGamesDir);
    }

    private void SetCurrentGame(string gameDirectory)
    {
        _currentGameDirectory = gameDirectory;
        _currentGameStateFile = Path.Combine(gameDirectory, "game_state.json");
        Directory.CreateDirectory(gameDirectory);
    }

    public async Task<GameStateModel> CreateNewGameStateAsync()
    {
        // Create a new game state with no specific ruleset
        var gameId = GameStorageHelper.GenerateGameId("unspecified");
        var gameDirectory = GameStorageHelper.GetGameDirectory(gameId);
        SetCurrentGame(gameDirectory);

        var state = new GameStateModel();
        state.GameId = gameId;
        state.AdventureSummary = "New Game - Ruleset selection and character creation pending.";
        state.CurrentContext = "Beginning of a new adventure. The player needs to select a ruleset and create their character.";

        await SaveStateAsync(state);
        return state;
    }

    public async Task<GameStateModel> CreateNewGameStateAsync(string rulesetId)
    {
        var gameId = GameStorageHelper.GenerateGameId(rulesetId);
        var gameDirectory = GameStorageHelper.GetGameDirectory(gameId);
        SetCurrentGame(gameDirectory);

        var state = new GameStateModel();
        state.GameId = gameId;
        state.AdventureSummary = "New Game - Character creation and adventure setup beginning.";
        state.CurrentContext = "Beginning of a new adventure. The player is setting up their character and starting their journey.";

        // Set ruleset and initialize schema
        if (!string.IsNullOrEmpty(rulesetId))
        {
            await _rulesetManager.SetActiveRulesetAsync(rulesetId);
            var ruleset = _rulesetManager.GetActiveRuleset();
            if (ruleset != null)
            {
                _rulesetManager.InitializeGameStateFromRuleset(state, ruleset);
                
                // Copy the ruleset to the game directory
                await CopyRulesetToGameDirectoryAsync(rulesetId, gameDirectory);
            }
        }

        await SaveStateAsync(state);
        return state;
    }

    public async Task SaveStateAsync(GameStateModel gameState)
    {
        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

        // If no current game directory is set, find or create one based on the game state
        if (string.IsNullOrEmpty(_currentGameDirectory))
        {
            if (!string.IsNullOrEmpty(gameState.GameId))
            {
                var gameDirectory = GameStorageHelper.GetGameDirectory(gameState.GameId);
                SetCurrentGame(gameDirectory);
            }
            else
            {
                // Legacy game state without GameId - create a new one
                var gameId = GameStorageHelper.GenerateGameId(gameState.ActiveRulesetId ?? "legacy");
                gameState.GameId = gameId;
                var gameDirectory = GameStorageHelper.GetGameDirectory(gameId);
                SetCurrentGame(gameDirectory);
            }
        }

        var json = JsonSerializer.Serialize(gameState, _jsonOptions);
        await File.WriteAllTextAsync(_currentGameStateFile, json);
    }

    public async Task<GameStateModel> LoadLatestStateAsync()
    {
        // Look for the most recent game directory
        var userGamesDir = GameStorageHelper.GetUserGamesDirectory();
        
        if (!Directory.Exists(userGamesDir))
        {
            return await CreateNewGameStateAsync();
        }

        var gameDirectories = Directory.GetDirectories(userGamesDir)
            .OrderByDescending(Directory.GetCreationTime)
            .ToArray();

        foreach (var gameDirectory in gameDirectories)
        {
            var gameStateFile = Path.Combine(gameDirectory, "game_state.json");
            if (File.Exists(gameStateFile))
            {
                try
                {
                    SetCurrentGame(gameDirectory);
                    var json = await File.ReadAllTextAsync(gameStateFile);
                    var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
                    
                    if (gameState != null)
                    {
                        // Set the game ID if it's missing (legacy compatibility)
                        if (string.IsNullOrEmpty(gameState.GameId))
                        {
                            gameState.GameId = Path.GetFileName(gameDirectory);
                        }

                        // Load the game's ruleset (prefer the local copy)
                        if (!string.IsNullOrEmpty(gameState.ActiveRulesetId))
                        {
                            await LoadGameRulesetAsync(gameDirectory, gameState.ActiveRulesetId);
                        }
                        
                        return gameState;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load game state from {gameDirectory}: {ex.Message}");
                    continue;
                }
            }
        }
        
        return await CreateNewGameStateAsync();
    }

    public async Task<bool> HasGameStateAsync()
    {
        await Task.Yield();
        
        var userGamesDir = GameStorageHelper.GetUserGamesDirectory();
        if (!Directory.Exists(userGamesDir))
            return false;

        var gameDirectories = Directory.GetDirectories(userGamesDir);
        return gameDirectories.Any(dir => File.Exists(Path.Combine(dir, "game_state.json")));
    }

    public async Task CopyRulesetToGameDirectoryAsync(string rulesetId, string gameDirectory)
    {
        // Find the original ruleset file
        var candidatePaths = new[]
        {
            Path.Combine("Rulesets", $"{rulesetId}.json"),
            Path.Combine("PokeLLM", "Rulesets", $"{rulesetId}.json"),
            Path.Combine(AppContext.BaseDirectory, "Rulesets", $"{rulesetId}.json"),
            Path.Combine(AppContext.BaseDirectory, "PokeLLM", "Rulesets", $"{rulesetId}.json")
        };

        string sourceRulesetPath = null;
        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                sourceRulesetPath = path;
                break;
            }
        }

        if (sourceRulesetPath == null)
        {
            throw new FileNotFoundException($"Could not find ruleset file for {rulesetId}");
        }

        var targetRulesetPath = Path.Combine(gameDirectory, "active_ruleset.json");
        File.Copy(sourceRulesetPath, targetRulesetPath, overwrite: true);
    }

    public async Task SaveRulesetChangesAsync(string gameDirectory, string rulesetJson)
    {
        var rulesetPath = Path.Combine(gameDirectory, "active_ruleset.json");
        await File.WriteAllTextAsync(rulesetPath, rulesetJson);
    }

    private async Task LoadGameRulesetAsync(string gameDirectory, string rulesetId)
    {
        // Try to load the local ruleset copy first
        var localRulesetPath = Path.Combine(gameDirectory, "active_ruleset.json");
        
        if (File.Exists(localRulesetPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(localRulesetPath);
                var document = JsonDocument.Parse(json);
                
                // Set this as the active ruleset in the manager
                await _rulesetManager.SetActiveRulesetFromDocumentAsync(document, rulesetId);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load local ruleset copy, falling back to original: {ex.Message}");
            }
        }

        // Fall back to loading from the original location
        await _rulesetManager.SetActiveRulesetAsync(rulesetId);
    }
}