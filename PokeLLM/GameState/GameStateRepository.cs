using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Models;
using PokeLLM.GameRules.Interfaces;

namespace PokeLLM.GameState;
public interface IGameStateRepository
{
    Task<GameStateModel> CreateNewGameStateAsync();
    Task<GameStateModel> CreateNewGameStateAsync(string rulesetId);
    Task SaveStateAsync(GameStateModel gameState);
    Task<GameStateModel> LoadLatestStateAsync();
    Task<bool> HasGameStateAsync();
}
public class GameStateRepository : IGameStateRepository
{
    private readonly string _gameStateDirectory;
    private readonly string _currentStateFile;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IRulesetManager _rulesetManager;

    public GameStateRepository(IRulesetManager rulesetManager, string dataDirectory = "GameData")
    {
        _rulesetManager = rulesetManager;
        _gameStateDirectory = dataDirectory;
        _currentStateFile = Path.Combine(_gameStateDirectory, "game_current_state.json");
        
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
    }

    public async Task<GameStateModel> CreateNewGameStateAsync()
    {
        // Create a new game state with no specific ruleset
        var state = new GameStateModel();
        state.AdventureSummary = "New Game - Ruleset selection and character creation pending.";
        state.CurrentContext = "Beginning of a new adventure. The player needs to select a ruleset and create their character.";

        await SaveStateAsync(state);
        return state;
    }

    public async Task<GameStateModel> CreateNewGameStateAsync(string rulesetId)
    {
        var state = new GameStateModel();
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
            }
        }

        await SaveStateAsync(state);
        return state;
    }

    public async Task SaveStateAsync(GameStateModel gameState)
    {
        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

        var json = JsonSerializer.Serialize(gameState, _jsonOptions);
        await File.WriteAllTextAsync(_currentStateFile, json);
    }

    public async Task<GameStateModel> LoadLatestStateAsync()
    {
        if(!File.Exists(_currentStateFile))
        {
            return await CreateNewGameStateAsync();
        }

        var json = await File.ReadAllTextAsync(_currentStateFile);
        var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
        
        if (gameState != null && !string.IsNullOrEmpty(gameState.ActiveRulesetId))
        {
            // Set the active ruleset based on the loaded game state
            await _rulesetManager.SetActiveRulesetAsync(gameState.ActiveRulesetId);
        }
        
        return gameState ?? await CreateNewGameStateAsync();
    }

    public async Task<bool> HasGameStateAsync()
    {
        await Task.Yield();
        return File.Exists(_currentStateFile);
    }
}