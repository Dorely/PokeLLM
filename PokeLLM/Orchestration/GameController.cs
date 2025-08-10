using PokeLLM.GameState.Models;
using System.Runtime.CompilerServices;
using PokeLLM.Game.Orchestration;

namespace PokeLLM.Game.GameLogic;

public interface IGameController
{
    IAsyncEnumerable<string> ProcessInputAsync(string input, CancellationToken cancellationToken = default);
    Task<GameStatus> GetGameStatusAsync();
}

public class GameController : IGameController
{
    private readonly IGameSetupService _gameSetupService;
    private readonly IWorldGenerationService _worldGenerationService;
    private readonly IOrchestrationService _orchestrationService;
    private readonly IGameStateRepository _gameStateRepository;

    public GameController(
        IGameSetupService gameSetupService,
        IWorldGenerationService worldGenerationService,
        IOrchestrationService orchestrationService,
        IGameStateRepository gameStateRepository)
    {
        _gameSetupService = gameSetupService;
        _worldGenerationService = worldGenerationService;
        _orchestrationService = orchestrationService;
        _gameStateRepository = gameStateRepository;
    }

    public async IAsyncEnumerable<string> ProcessInputAsync(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var status = await GetGameStatusAsync();
        
        switch (status)
        {
            case GameStatus.SetupNeeded:
                await foreach (var chunk in _gameSetupService.RunGameSetupAsync(input, cancellationToken))
                    yield return chunk;
                
                // Auto-transition to world generation if setup complete
                if (await _gameSetupService.IsSetupCompleteAsync())
                {
                    yield return "\n\n--- Setup Complete! Starting World Generation ---\n\n";
                    await foreach (var chunk in _worldGenerationService.RunWorldGenerationAsync(
                        "Begin world generation based on setup.", cancellationToken))
                        yield return chunk;
                }
                break;
                
            case GameStatus.WorldGenerationNeeded:
                await foreach (var chunk in _worldGenerationService.RunWorldGenerationAsync(input, cancellationToken))
                    yield return chunk;
                
                // Auto-transition to gameplay if world generation complete
                if (await _worldGenerationService.IsWorldGenerationCompleteAsync())
                {
                    var gameState = await _gameStateRepository.LoadLatestStateAsync();
                    gameState.CurrentPhase = GamePhase.Exploration;
                    await _gameStateRepository.SaveStateAsync(gameState);
                    
                    yield return "\n\n--- World Complete! Adventure Begins ---\n\n";
                    await foreach (var chunk in _orchestrationService.OrchestrateAsync(
                        "Describe the opening scene.", cancellationToken))
                        yield return chunk;
                }
                break;
                
            case GameStatus.GameplayActive:
                await foreach (var chunk in _orchestrationService.OrchestrateAsync(input, cancellationToken))
                    yield return chunk;
                break;
        }
    }

    public async Task<GameStatus> GetGameStatusAsync()
    {
        if (!await _gameSetupService.IsSetupCompleteAsync())
            return GameStatus.SetupNeeded;
        
        if (!await _worldGenerationService.IsWorldGenerationCompleteAsync())
            return GameStatus.WorldGenerationNeeded;
        
        return GameStatus.GameplayActive;
    }
}

public enum GameStatus
{
    SetupNeeded,
    WorldGenerationNeeded, 
    GameplayActive
}