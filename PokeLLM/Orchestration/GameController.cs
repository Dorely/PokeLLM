using PokeLLM.GameState.Models;
using System.Runtime.CompilerServices;
using PokeLLM.Game.Orchestration;
using System.Diagnostics;
using PokeLLM.Game.Orchestration.Interfaces;

namespace PokeLLM.Game.GameLogic;



public class GameController : IGameController
{
    private readonly IPhaseServiceProvider _phaseServiceProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IUnifiedContextService _unifiedContextService;

    public GameController(
        IPhaseServiceProvider phaseServiceProvider,
        IGameStateRepository gameStateRepository,
        IUnifiedContextService unifiedContextService)
    {
        _phaseServiceProvider = phaseServiceProvider;
        _gameStateRepository = gameStateRepository;
        _unifiedContextService = unifiedContextService;
    }

    public async IAsyncEnumerable<string> ProcessInputAsync(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var currentPhase = await GetCurrentPhaseAsync();
        var initialPhase = currentPhase;
        
        Debug.WriteLine($"[GameController] Processing input for phase: {currentPhase}");
        
        // Handle different phases - all use PhaseService now
        switch (currentPhase)
        {
            case GamePhase.GameSetup:
                var setupPhaseService = _phaseServiceProvider.GetPhaseService(GamePhase.GameSetup);
                await foreach (var chunk in setupPhaseService.ProcessPhaseAsync(input, cancellationToken))
                    yield return chunk;
                break;
                
            case GamePhase.WorldGeneration:
                // World generation runs autonomously - adjust input for continuation
                string worldGenInput = input == "Begin world generation based on setup." || string.IsNullOrEmpty(input) ? 
                    "Begin comprehensive world generation based on the completed setup. Create a rich, detailed world with multiple locations, NPCs, wild Pokemon, and lore. Provide engaging updates about your progress." : 
                    input;
                    
                var worldGenPhaseService = _phaseServiceProvider.GetPhaseService(GamePhase.WorldGeneration);
                await foreach (var chunk in worldGenPhaseService.ProcessPhaseAsync(worldGenInput, cancellationToken))
                    yield return chunk;
                
                // If still in WorldGeneration phase, continue autonomously
                var gameState = await _gameStateRepository.LoadLatestStateAsync();
                if (gameState.CurrentPhase == GamePhase.WorldGeneration)
                {
                    yield return "\n\n";
                    await foreach (var chunk in ProcessInputAsync("Continue with world generation. Create more content or finalize when you have created a complete world ready for adventure.", cancellationToken))
                        yield return chunk;
                    yield break;
                }
                break;
                
            case GamePhase.Exploration:
            case GamePhase.Combat:
            case GamePhase.LevelUp:
                // Use the phase service for gameplay phases
                var phaseService = _phaseServiceProvider.GetPhaseService(currentPhase);
                await foreach (var chunk in phaseService.ProcessPhaseAsync(input, cancellationToken))
                    yield return chunk;
                break;
                
            default:
                yield return "Unknown game phase. Please restart the game.";
                yield break;
        }
        
        // Universal phase change detection and handling
        var finalGameState = await _gameStateRepository.LoadLatestStateAsync();
        if (finalGameState.CurrentPhase != initialPhase)
        {
            Debug.WriteLine($"[GameController] Phase transition detected: {initialPhase} -> {finalGameState.CurrentPhase}");
            
            // Run context management before phase transition
            await _unifiedContextService.RunContextManagementAsync(null, 
                $"Phase transition from {initialPhase} to {finalGameState.CurrentPhase}. Update context accordingly.", 
                cancellationToken);
            
            yield return $"\n\n--- Phase Transition: {initialPhase} → {finalGameState.CurrentPhase} ---\n\n";
            
            // Create appropriate transition message for the new phase
            var transitionMessage = CreatePhaseTransitionMessage(initialPhase, finalGameState.CurrentPhase, finalGameState.PhaseChangeSummary);
            
            // Recursively process the new phase
            await foreach (var chunk in ProcessInputAsync(transitionMessage, cancellationToken))
                yield return chunk;
            yield break;
        }
        else if (currentPhase == GamePhase.Exploration || currentPhase == GamePhase.Combat || currentPhase == GamePhase.LevelUp)
        {
            await _unifiedContextService.RunContextManagementAsync(null, 
                $"Post-turn context update for {currentPhase} phase. Update CurrentContext field and maintain consistency.", 
                cancellationToken);
        }
    }


    public async Task<GamePhase> GetCurrentPhaseAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // If CurrentPhase is not set, start with GameSetup
        if (gameState.CurrentPhase == default(GamePhase))
        {
            gameState.CurrentPhase = GamePhase.GameSetup;
            await _gameStateRepository.SaveStateAsync(gameState);
        }
        
        return gameState.CurrentPhase;
    }
    
    private async Task<bool> IsGameSetupCompleteAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return !string.IsNullOrEmpty(gameState.Region) &&
               !string.IsNullOrEmpty(gameState.Player.Name) &&
               !string.IsNullOrEmpty(gameState.Player.CharacterDetails.Class);
    }
    
    private string CreatePhaseTransitionMessage(GamePhase fromPhase, GamePhase toPhase, string phaseChangeSummary)
    {
        var baseMessage = $"The adventure has just transitioned from {fromPhase} to {toPhase} phase.";
        
        if (!string.IsNullOrEmpty(phaseChangeSummary))
        {
            baseMessage += $" {phaseChangeSummary}";
        }

        return toPhase switch
        {
            GamePhase.GameSetup => $"{baseMessage} Please continue with game setup. Help the player configure their character and choose their region.",
            
            GamePhase.WorldGeneration => $"{baseMessage} Please begin world generation based on the completed setup. Create a rich, detailed world with multiple locations, NPCs, wild Pokemon, and lore.",
            
            GamePhase.Exploration => $"{baseMessage} Please continue the adventure in exploration mode. The player can now explore the world, interact with NPCs, and discover new locations.",
            
            GamePhase.Combat => $"{baseMessage} Please continue managing the combat encounter that has just begun. Describe the battle situation and guide the player through their combat options.",
            
            GamePhase.LevelUp => $"{baseMessage} Please guide the player through the level up process, allowing them to improve their abilities and grow stronger.",
            
            _ => $"{baseMessage} Please continue the adventure in the new {toPhase} phase."
        };
    }
}