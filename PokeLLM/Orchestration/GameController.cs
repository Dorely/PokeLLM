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
                }
                break;
                
            case GameStatus.WorldGenerationNeeded:
                // Run world generation in a continuous loop until completion
                string currentInput = input == "Begin world generation based on setup." || string.IsNullOrEmpty(input) ? 
                    "Begin comprehensive world generation based on the completed setup. Create a rich, detailed world with multiple locations, NPCs, wild Pokemon, and lore. Provide engaging updates about your progress." : 
                    input;
                
                while (!await _worldGenerationService.IsWorldGenerationCompleteAsync())
                {
                    await foreach (var chunk in _worldGenerationService.RunWorldGenerationAsync(currentInput, cancellationToken))
                        yield return chunk;
                    
                    // If still not complete, continue with next iteration
                    if (!await _worldGenerationService.IsWorldGenerationCompleteAsync())
                    {
                        yield return "\n\n";
                        currentInput = "Continue with world generation. Create more content or finalize when you have created a complete world ready for adventure.";
                    }
                }
                
                yield return "\n\n--- World Generation Complete! Adventure Begins ---\n\n";
                
                // Auto-transition to gameplay if world generation complete
                var gameState = await _gameStateRepository.LoadLatestStateAsync();
                gameState.CurrentPhase = GamePhase.Exploration;
                await _gameStateRepository.SaveStateAsync(gameState);
                
                await foreach (var chunk in _orchestrationService.OrchestrateAsync(
                    "Describe the opening scene and begin the adventure.", cancellationToken))
                    yield return chunk;
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