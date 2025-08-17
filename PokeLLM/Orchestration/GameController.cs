using PokeLLM.GameState.Models;
using System.Runtime.CompilerServices;
using PokeLLM.Game.Orchestration;
using System.Diagnostics;
using PokeLLM.Logging;

namespace PokeLLM.Game.GameLogic;

public interface IGameController
{
    IAsyncEnumerable<string> ProcessInputAsync(string input, CancellationToken cancellationToken = default);
    Task<GamePhase> GetCurrentPhaseAsync();
}

public class GameController : IGameController
{
    private readonly IPhaseServiceProvider _phaseServiceProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IUnifiedContextService _unifiedContextService;
    private readonly IDebugLogger _debugLogger;

    public GameController(
        IPhaseServiceProvider phaseServiceProvider,
        IGameStateRepository gameStateRepository,
        IUnifiedContextService unifiedContextService,
        IDebugLogger debugLogger)
    {
        _phaseServiceProvider = phaseServiceProvider;
        _gameStateRepository = gameStateRepository;
        _unifiedContextService = unifiedContextService;
        _debugLogger = debugLogger;
        
        _debugLogger.LogDebug("[GameController] GameController initialized");
    }

    public async IAsyncEnumerable<string> ProcessInputAsync(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _debugLogger.LogDebug($"[GameController] Starting to process input: {input}");
        
        var currentPhase = await GetCurrentPhaseAsync();
        var initialPhase = currentPhase;
        
        _debugLogger.LogDebug($"[GameController] Current phase: {currentPhase}");
        Debug.WriteLine($"[GameController] Processing input for phase: {currentPhase}");
        
        // Handle different phases - all use PhaseService now
        switch (currentPhase)
        {
            case GamePhase.GameSetup:
                _debugLogger.LogDebug("[GameController] Processing GameSetup phase");
                var setupPhaseService = _phaseServiceProvider.GetPhaseService(GamePhase.GameSetup);
                await foreach (var chunk in setupPhaseService.ProcessPhaseAsync(input, cancellationToken))
                    yield return chunk;
                break;
                
            case GamePhase.WorldGeneration:
                // World generation runs autonomously - adjust input for continuation
                string worldGenInput = input == "Begin world generation based on setup." || string.IsNullOrEmpty(input) ? 
                    "Begin comprehensive world generation based on the completed setup. Create a rich, detailed world with multiple locations, NPCs, and entities appropriate for the active ruleset. Provide engaging updates about your progress." : 
                    input;
                    
                _debugLogger.LogDebug($"[GameController] Processing WorldGeneration phase with input: {worldGenInput}");
                    
                var worldGenPhaseService = _phaseServiceProvider.GetPhaseService(GamePhase.WorldGeneration);
                await foreach (var chunk in worldGenPhaseService.ProcessPhaseAsync(worldGenInput, cancellationToken))
                    yield return chunk;
                
                // If still in WorldGeneration phase, continue autonomously
                var gameState = await _gameStateRepository.LoadLatestStateAsync();
                if (gameState.CurrentPhase == GamePhase.WorldGeneration)
                {
                    _debugLogger.LogDebug("[GameController] Still in WorldGeneration phase, continuing autonomously");
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
                _debugLogger.LogDebug($"[GameController] Processing {currentPhase} phase");
                var phaseService = _phaseServiceProvider.GetPhaseService(currentPhase);
                await foreach (var chunk in phaseService.ProcessPhaseAsync(input, cancellationToken))
                    yield return chunk;
                break;
                
            default:
                var errorMsg = $"Unknown game phase: {currentPhase}";
                _debugLogger.LogError($"[GameController] {errorMsg}");
                yield return "Unknown game phase. Please restart the game.";
                yield break;
        }
        
        // Universal phase change detection and handling
        var finalGameState = await _gameStateRepository.LoadLatestStateAsync();
        if (finalGameState.CurrentPhase != initialPhase)
        {
            _debugLogger.LogPhaseTransition(initialPhase.ToString(), finalGameState.CurrentPhase.ToString(), 
                finalGameState.PhaseChangeSummary ?? "No summary provided");
            
            Debug.WriteLine($"[GameController] Phase transition detected: {initialPhase} -> {finalGameState.CurrentPhase}");
            
            // Run context management before phase transition
            _debugLogger.LogDebug("[GameController] Running context management for phase transition");
            await _unifiedContextService.RunContextManagementAsync(null, 
                $"Phase transition from {initialPhase} to {finalGameState.CurrentPhase}. Update context accordingly.", 
                cancellationToken);
            
            yield return $"\n\n--- Phase Transition: {initialPhase} → {finalGameState.CurrentPhase} ---\n\n";
            
            // Create appropriate transition message for the new phase
            var transitionMessage = CreatePhaseTransitionMessage(initialPhase, finalGameState.CurrentPhase, finalGameState.PhaseChangeSummary);
            _debugLogger.LogDebug($"[GameController] Created phase transition message: {transitionMessage}");
            
            // Recursively process the new phase
            await foreach (var chunk in ProcessInputAsync(transitionMessage, cancellationToken))
                yield return chunk;
            yield break;
        }
        else if (currentPhase == GamePhase.Exploration || currentPhase == GamePhase.Combat || currentPhase == GamePhase.LevelUp)
        {
            _debugLogger.LogDebug($"[GameController] Running post-turn context update for {currentPhase} phase");
            await _unifiedContextService.RunContextManagementAsync(null, 
                $"Post-turn context update for {currentPhase} phase. Update CurrentContext field and maintain consistency.", 
                cancellationToken);
        }
        
        _debugLogger.LogDebug("[GameController] Input processing completed");
    }


    public async Task<GamePhase> GetCurrentPhaseAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // If CurrentPhase is not set, start with GameSetup
        if (gameState.CurrentPhase == default(GamePhase))
        {
            _debugLogger.LogDebug("[GameController] CurrentPhase not set, initializing to GameSetup");
            gameState.CurrentPhase = GamePhase.GameSetup;
            await _gameStateRepository.SaveStateAsync(gameState);
        }
        
        _debugLogger.LogDebug($"[GameController] Current phase retrieved: {gameState.CurrentPhase}");
        return gameState.CurrentPhase;
    }
    
    private async Task<bool> IsGameSetupCompleteAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        // For now, just check basic requirements since CharacterDetails is removed
        // In a full implementation, this would check ruleset-specific setup requirements
        var isComplete = !string.IsNullOrEmpty(gameState.Region) &&
                        !string.IsNullOrEmpty(gameState.Player.Name);
        
        _debugLogger.LogDebug($"[GameController] Game setup complete check: {isComplete} (Region: {!string.IsNullOrEmpty(gameState.Region)}, Player Name: {!string.IsNullOrEmpty(gameState.Player.Name)})");
        
        return isComplete;
    }
    
    private string CreatePhaseTransitionMessage(GamePhase fromPhase, GamePhase toPhase, string phaseChangeSummary)
    {
        var baseMessage = $"The adventure has just transitioned from {fromPhase} to {toPhase} phase.";
        
        if (!string.IsNullOrEmpty(phaseChangeSummary))
        {
            baseMessage += $" {phaseChangeSummary}";
        }

        var message = toPhase switch
        {
            GamePhase.GameSetup => $"{baseMessage} Please continue with game setup. Help the player configure their character and choose their region.",
            
            GamePhase.WorldGeneration => $"{baseMessage} Please begin world generation based on the completed setup. Create a rich, detailed world with multiple locations, NPCs, and entities appropriate for the active ruleset.",
            
            GamePhase.Exploration => $"{baseMessage} Please continue the adventure in exploration mode. The player can now explore the world, interact with NPCs, and discover new locations.",
            
            GamePhase.Combat => $"{baseMessage} Please continue managing the combat encounter that has just begun. Describe the battle situation and guide the player through their combat options.",
            
            GamePhase.LevelUp => $"{baseMessage} Please guide the player through the level up process, allowing them to improve their abilities and grow stronger.",
            
            _ => $"{baseMessage} Please continue the adventure in the new {toPhase} phase."
        };
        
        _debugLogger.LogDebug($"[GameController] Phase transition message created for {fromPhase} -> {toPhase}: {message}");
        return message;
    }
}