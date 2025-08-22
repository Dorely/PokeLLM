using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.State;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PokeLLM.Agents;

public interface IGameOrchestrator
{
    Task<string> StartNewGameSessionAsync(string playerName, string characterBackstory, string preferredSetting, CancellationToken cancellationToken = default);
    IAsyncEnumerable<GameTurnResult> ProcessPlayerInputAsync(string sessionId, string userInput, CancellationToken cancellationToken = default);
    Task SaveGameSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task LoadGameSessionAsync(string sessionId, string saveData, CancellationToken cancellationToken = default);
    Task EndGameSessionAsync(string sessionId);
    Task<IEnumerable<string>> GetActiveSessionsAsync();
    Task<GameSession?> GetSessionAsync(string sessionId);
}

public class GameOrchestrator : IGameOrchestrator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, GameSession> _activeSessions;
    private readonly IGameAgentManager _agentManager;

    public GameOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<GameOrchestrator> logger,
        IGameAgentManager agentManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _agentManager = agentManager;
        _activeSessions = new ConcurrentDictionary<string, GameSession>();
    }

    public async Task<string> StartNewGameSessionAsync(
        string playerName, 
        string characterBackstory, 
        string preferredSetting, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new game session for player {PlayerName}", playerName);

        // Create a new game session
        var gameSession = new GameSession(
            _agentManager,
            _serviceProvider.GetRequiredService<IEventLog>(),
            _serviceProvider.GetRequiredService<ILogger<GameSession>>()
        );

        // Start the game
        await gameSession.StartNewGameAsync(playerName, characterBackstory, preferredSetting, cancellationToken);

        // Register the session
        _activeSessions.TryAdd(gameSession.SessionId, gameSession);

        _logger.LogInformation("New game session {SessionId} created for player {PlayerName}", 
            gameSession.SessionId, playerName);

        return gameSession.SessionId;
    }

    public async IAsyncEnumerable<GameTurnResult> ProcessPlayerInputAsync(
        string sessionId, 
        string userInput, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Attempted to process input for non-existent session {SessionId}", sessionId);
            yield break;
        }

        _logger.LogInformation("Processing input for session {SessionId}: {Input}", sessionId, userInput);

        await foreach (var result in session.ProcessPlayerInputAsync(userInput, cancellationToken))
        {
            yield return result;
        }
    }

    public async Task SaveGameSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        await session.SaveGameAsync(cancellationToken);
        _logger.LogInformation("Game session {SessionId} saved", sessionId);
    }

    public async Task LoadGameSessionAsync(string sessionId, string saveData, CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            // Create a new session for loading
            session = new GameSession(
                _agentManager,
                _serviceProvider.GetRequiredService<IEventLog>(),
                _serviceProvider.GetRequiredService<ILogger<GameSession>>()
            );
            
            _activeSessions.TryAdd(sessionId, session);
        }

        await session.LoadGameAsync(saveData, cancellationToken);
        _logger.LogInformation("Game session {SessionId} loaded", sessionId);
    }

    public async Task EndGameSessionAsync(string sessionId)
    {
        if (_activeSessions.TryRemove(sessionId, out var session))
        {
            await session.EndSessionAsync();
            _logger.LogInformation("Game session {SessionId} ended", sessionId);
        }
        else
        {
            _logger.LogWarning("Attempted to end non-existent session {SessionId}", sessionId);
        }
    }

    public Task<IEnumerable<string>> GetActiveSessionsAsync()
    {
        var activeSessions = _activeSessions.Keys.ToList();
        return Task.FromResult<IEnumerable<string>>(activeSessions);
    }

    public Task<GameSession?> GetSessionAsync(string sessionId)
    {
        _activeSessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing game orchestrator with {Count} active sessions", _activeSessions.Count);

        // End all active sessions
        var endTasks = _activeSessions.Values.Select(session => session.EndSessionAsync());
        Task.WhenAll(endTasks).GetAwaiter().GetResult();

        _activeSessions.Clear();
    }
}

public static class GameOrchestratorExtensions
{
    public static IServiceCollection AddGameOrchestration(this IServiceCollection services)
    {
        services.AddSingleton<IGameOrchestrator, GameOrchestrator>();
        services.AddTransient<GameSession>();
        
        return services;
    }
}