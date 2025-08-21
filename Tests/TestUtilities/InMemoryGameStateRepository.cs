using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Tests.TestUtilities;

/// <summary>
/// In-memory implementation of IGameStateRepository for testing purposes
/// </summary>
public class InMemoryGameStateRepository : IGameStateRepository
{
    private GameStateModel? _currentState;
    private readonly Dictionary<string, string> _rulesetCache = new();

    public Task<GameStateModel> CreateNewGameStateAsync()
    {
        _currentState = new GameStateModel
        {
            SessionId = Guid.NewGuid().ToString(),
            GameId = Guid.NewGuid().ToString(),
            GameTurnNumber = 0,
            SessionStartTime = DateTime.UtcNow,
            LastSaveTime = DateTime.UtcNow,
            CurrentPhase = GamePhase.GameSetup,
            ActiveRulesetId = "pokemon-adventure"
        };
        return Task.FromResult(_currentState);
    }

    public Task<GameStateModel> CreateNewGameStateAsync(string rulesetId)
    {
        _currentState = new GameStateModel
        {
            SessionId = Guid.NewGuid().ToString(),
            GameId = Guid.NewGuid().ToString(),
            GameTurnNumber = 0,
            SessionStartTime = DateTime.UtcNow,
            LastSaveTime = DateTime.UtcNow,
            CurrentPhase = GamePhase.GameSetup,
            ActiveRulesetId = rulesetId
        };
        return Task.FromResult(_currentState);
    }

    public Task SaveStateAsync(GameStateModel gameState)
    {
        _currentState = gameState;
        _currentState.LastSaveTime = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<GameStateModel> LoadLatestStateAsync()
    {
        if (_currentState == null)
        {
            throw new InvalidOperationException("No game state has been created yet.");
        }
        return Task.FromResult(_currentState);
    }

    public Task<bool> HasGameStateAsync()
    {
        return Task.FromResult(_currentState != null);
    }

    public Task CopyRulesetToGameDirectoryAsync(string rulesetId, string gameDirectory)
    {
        // In-memory implementation - just cache the rulesetId
        _rulesetCache[gameDirectory] = rulesetId;
        return Task.CompletedTask;
    }

    public Task SaveRulesetChangesAsync(string gameDirectory, string rulesetJson)
    {
        // In-memory implementation - just store in cache
        _rulesetCache[gameDirectory + "_json"] = rulesetJson;
        return Task.CompletedTask;
    }

    // Test helper methods
    public GameStateModel? GetCurrentState() => _currentState;
    
    public void Reset()
    {
        _currentState = null;
        _rulesetCache.Clear();
    }
}