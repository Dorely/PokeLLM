using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeLLM.Agents;
using System.Runtime.CompilerServices;

namespace PokeLLM.Controllers;

public interface IGameController
{
    Task<GameSessionInfo> StartNewGameAsync(StartGameRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> PlayTurnAsync(PlayTurnRequest request, CancellationToken cancellationToken = default);
    Task SaveGameAsync(string sessionId, CancellationToken cancellationToken = default);
    Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default);
    Task EndGameAsync(string sessionId);
    Task<IEnumerable<GameSessionInfo>> GetActiveGamesAsync();
}

public class GameController : IGameController
{
    private readonly IGameOrchestrator _orchestrator;
    private readonly ILogger<GameController> _logger;

    public GameController(IGameOrchestrator orchestrator, ILogger<GameController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<GameSessionInfo> StartNewGameAsync(StartGameRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting new game for player {PlayerName}", request.PlayerName);

        var sessionId = await _orchestrator.StartNewGameSessionAsync(
            request.PlayerName,
            request.CharacterBackstory,
            request.PreferredSetting,
            cancellationToken);

        var session = await _orchestrator.GetSessionAsync(sessionId);
        
        return new GameSessionInfo(
            SessionId: sessionId,
            PlayerName: request.PlayerName,
            IsActive: session?.IsActive ?? false,
            CreatedAt: DateTime.UtcNow
        );
    }

    public async IAsyncEnumerable<string> PlayTurnAsync(
        PlayTurnRequest request, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing turn for session {SessionId}: {Input}", 
            request.SessionId, request.PlayerInput);

        await foreach (var result in _orchestrator.ProcessPlayerInputAsync(
            request.SessionId, 
            request.PlayerInput, 
            cancellationToken))
        {
            // Stream the response content back to the UI
            yield return result.Response;
            
            _logger.LogDebug("Turn {TurnNumber} completed for session {SessionId}", 
                result.TurnNumber, result.SessionId);
        }
    }

    public async Task SaveGameAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving game session {SessionId}", sessionId);
        await _orchestrator.SaveGameSessionAsync(sessionId, cancellationToken);
    }

    public async Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading game session {SessionId}", request.SessionId);
        await _orchestrator.LoadGameSessionAsync(request.SessionId, request.SaveData, cancellationToken);
    }

    public async Task EndGameAsync(string sessionId)
    {
        _logger.LogInformation("Ending game session {SessionId}", sessionId);
        await _orchestrator.EndGameSessionAsync(sessionId);
    }

    public async Task<IEnumerable<GameSessionInfo>> GetActiveGamesAsync()
    {
        var sessionIds = await _orchestrator.GetActiveSessionsAsync();
        var sessionInfos = new List<GameSessionInfo>();

        foreach (var sessionId in sessionIds)
        {
            var session = await _orchestrator.GetSessionAsync(sessionId);
            if (session != null)
            {
                sessionInfos.Add(new GameSessionInfo(
                    SessionId: sessionId,
                    PlayerName: session.CurrentContext.PlayerState.Name,
                    IsActive: session.IsActive,
                    CreatedAt: DateTime.UtcNow // In real implementation, would track creation time
                ));
            }
        }

        return sessionInfos;
    }
}

// Request/Response DTOs
public record StartGameRequest(
    string PlayerName,
    string CharacterBackstory,
    string PreferredSetting);

public record PlayTurnRequest(
    string SessionId,
    string PlayerInput);

public record LoadGameRequest(
    string SessionId,
    string SaveData);

public record GameSessionInfo(
    string SessionId,
    string PlayerName,
    bool IsActive,
    DateTime CreatedAt);

public static class GameControllerExtensions
{
    public static IServiceCollection AddGameController(this IServiceCollection services)
    {
        services.AddTransient<IGameController, GameController>();
        return services;
    }
}