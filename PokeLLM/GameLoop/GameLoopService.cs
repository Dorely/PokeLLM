using PokeLLM.Game.GameLoop.Interfaces;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.GameState.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;
using PokeLLM.Game.Orchestration.Interfaces;

namespace PokeLLM.Game.GameLoop;

public class GameLoopService : IGameLoopService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IOrchestrationService _orchestrationService;
    private readonly IGameStateRepository _gameStateRepository;

    public GameLoopService(
        ILLMProvider llmProvider,
        IOrchestrationService orchestrationService,
        IGameStateRepository gameStateRepository)
    {
        _llmProvider = llmProvider;
        _orchestrationService = orchestrationService;
        _gameStateRepository = gameStateRepository;
    }

    public async IAsyncEnumerable<string> ProcessPlayerInputAsync(string playerInput, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"Processing player input: {playerInput}");

        // Step 1: Gather context using the Context Gathering Subroutine
        var gameContext = await GatherContextForInputAsync(playerInput, cancellationToken);
        
        // Step 2: Process the gathered context (placeholder for future use)
        var processedContext = await ProcessGatheredContextAsync(gameContext, cancellationToken);
        
        // Step 3: Pass context and input to the main game chat
        var enhancedInput = CreateEnhancedInput(playerInput, processedContext);
        
        // Stream the response from the main game chat
        await foreach (var chunk in _orchestrationService.ExecutePromptStreamingAsync(enhancedInput, null, cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<string> ProcessPlayerInputCompleteAsync(string playerInput, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"Processing player input (complete): {playerInput}");

        // Step 1: Gather context using the Context Gathering Subroutine
        var gameContext = await GatherContextForInputAsync(playerInput, cancellationToken);
        
        // Step 2: Process the gathered context (placeholder for future use)
        var processedContext = await ProcessGatheredContextAsync(gameContext, cancellationToken);
        
        // Step 3: Pass context and input to the main game chat
        var enhancedInput = CreateEnhancedInput(playerInput, processedContext);
        
        // Get complete response from the main game chat
        return await _orchestrationService.ExecutePromptAsync(enhancedInput, null, cancellationToken);
    }

    public async IAsyncEnumerable<string> GetWelcomeMessageAsync(CancellationToken cancellationToken = default)
    {
        var welcomeMessage = "The program has finished loading. Introduce yourself to the player.";
        
        await foreach (var chunk in _orchestrationService.ExecutePromptStreamingAsync(welcomeMessage, GamePhase.GameCreation, cancellationToken))
        {
            yield return chunk;
        }
    }

    private async Task<GameContext> GatherContextForInputAsync(string playerInput, CancellationToken cancellationToken)
    {
        try
        {
            Debug.WriteLine("Starting context gathering...");
            
            // Get current game state for adventure summary and recent events
            var gameState = await _gameStateRepository.LoadLatestStateAsync();
            var adventureSummary = gameState?.AdventureSummary ?? string.Empty;
            var recentHistory = gameState?.RecentEvents ?? new List<string>();

            // Call the Context Gathering Subroutine
//TODO make sure adventureSummary and recentHistory are being populated correctly
            var gameContext = await _orchestrationService.ExecuteContextGatheringAsync(
                playerInput, 
                adventureSummary, 
                recentHistory, 
                cancellationToken);

            Debug.WriteLine($"Context gathering completed: {gameContext.ContextSummary}");
            
            return gameContext;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during context gathering: {ex.Message}");
            
            // Return minimal context on error
            return new GameContext
            {
                ContextSummary = "Context gathering failed, proceeding with minimal information",
                RecommendedActions = ["Proceed with available game state"]
            };
        }
    }

    private async Task<GameContext> ProcessGatheredContextAsync(GameContext gameContext, CancellationToken cancellationToken)
    {
        // Placeholder for future context processing logic
        // This is where you could implement additional processing of the gathered context
        // such as:
        // - Additional validation
        // - Context transformation
        // - Priority sorting
        // - Context caching
        // - Performance optimizations
        
        Debug.WriteLine("Processing gathered context (placeholder)");
        
        return gameContext;
    }

    private string CreateEnhancedInput(string originalInput, GameContext gameContext)
    {
        // Create an enhanced input that includes both the original player input
        // and the structured context gathered by the Context Gathering Subroutine
        
        var enhancedInput = $@"## Player Input
{originalInput}

## Gathered Context
{gameContext.ContextSummary}

## Relevant Entities Found
{(gameContext.RelevantEntities.Any() ? string.Join(", ", gameContext.RelevantEntities.Keys) : "None")}

## Missing Entities
{(gameContext.MissingEntities.Any() ? string.Join(", ", gameContext.MissingEntities) : "None")}

## Game State Updates Made
{(gameContext.GameStateUpdates.Any() ? string.Join("\n", gameContext.GameStateUpdates) : "None")}

## Vector Store Information
{(gameContext.VectorStoreData.Any() ? string.Join("\n", gameContext.VectorStoreData.Select(v => $"- {v.Content}")) : "None")}

## Recommended Actions
{(gameContext.RecommendedActions.Any() ? string.Join("\n", gameContext.RecommendedActions.Select(a => $"- {a}")) : "None")}

Please respond to the player's input using this gathered context.";

        Debug.WriteLine("Enhanced input created with gathered context");
        
        return enhancedInput;
    }
}