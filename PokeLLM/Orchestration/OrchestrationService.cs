using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.GameState.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PokeLLM.Game.Orchestration;

public class OrchestrationService : IOrchestrationService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IConversationHistoryManager _historyManager;
    private readonly IPluginManager _pluginManager;
    private readonly IPromptManager _promptManager;
    
    private GamePhase _currentPhase;

    public OrchestrationService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IConversationHistoryManager historyManager,
        IPluginManager pluginManager,
        IPromptManager promptManager)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _historyManager = historyManager;
        _pluginManager = pluginManager;
        _promptManager = promptManager;
    }

    public async Task<string> ExecutePromptAsync(string prompt, GamePhase? phase = null, CancellationToken cancellationToken = default)
    {
        var gamePhase = phase ?? await GetCurrentPhaseAsync();
        
        // Check for phase changes before processing
        await RefreshPhaseAsync();
        
        // Get conversation history and add user message
        await _historyManager.AddUserMessageAsync(gamePhase, prompt);
        var history = await _historyManager.GetHistoryAsync(gamePhase);
        
        // Create kernel and load plugins
        var kernel = await _llmProvider.CreateKernelAsync();
        await _pluginManager.LoadPluginsForPhaseAsync(kernel, gamePhase);
        
        // Execute the prompt
        var executionSettings = (OpenAIPromptExecutionSettings)_llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);
            
        var promptTemplate = string.Join("\n", history.Select(msg => $"{msg.Role}: {msg.Content}"));
        
        var result = await kernel.InvokePromptAsync(
            promptTemplate,
            new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );
        
        var response = result.ToString();
        await _historyManager.AddAssistantMessageAsync(gamePhase, response);
        
        return response;
    }

    public async IAsyncEnumerable<string> ExecutePromptStreamingAsync(string prompt, GamePhase? phase = null, CancellationToken cancellationToken = default)
    {
        var gamePhase = phase ?? await GetCurrentPhaseAsync();
        
        // Check for phase changes before processing
        await RefreshPhaseAsync();
        
        // Get conversation history and add user message
        await _historyManager.AddUserMessageAsync(gamePhase, prompt);
        var history = await _historyManager.GetHistoryAsync(gamePhase);
        
        // Create kernel and load plugins
        var kernel = await _llmProvider.CreateKernelAsync();
        await _pluginManager.LoadPluginsForPhaseAsync(kernel, gamePhase);
        
        // Execute the prompt with streaming
        var executionSettings = (OpenAIPromptExecutionSettings)_llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);
            
        var promptTemplate = string.Join("\n", history.Select(msg => $"{msg.Role}: {msg.Content}"));
        
        var responseBuilder = new StringBuilder();
        var result = kernel.InvokePromptStreamingAsync(
            promptTemplate,
            new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );
        
        await foreach (var chunk in result)
        {
            var chunkText = chunk.ToString();
            responseBuilder.Append(chunkText);
            yield return chunkText;
        }
        
        // Add complete response to history
        await _historyManager.AddAssistantMessageAsync(gamePhase, responseBuilder.ToString());
    }

    public async Task<GameContext> ExecuteContextGatheringAsync(string playerInput, string adventureSummary, List<string> recentHistory, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create kernel and load context gathering plugins
            var kernel = await _llmProvider.CreateKernelAsync();
            await _pluginManager.LoadContextGatheringPluginsAsync(kernel);
            
            // Create the context gathering prompt
            var contextGatheringPrompt = await _promptManager.LoadContextGatheringPromptAsync();
            var prompt = _promptManager.CreateContextGatheringPrompt(contextGatheringPrompt, playerInput, adventureSummary, recentHistory);
            
            // Execute context gathering with specialized settings
            var executionSettings = (OpenAIPromptExecutionSettings)_llmProvider.GetExecutionSettings(
                maxTokens: 4000,
                temperature: 0.1f,
                enableFunctionCalling: true);

            var result = await kernel.InvokePromptAsync(
                prompt,
                new KernelArguments(executionSettings),
                cancellationToken: cancellationToken
            );

            var responseText = result.ToString();
            
            // Try to parse the JSON response
            var gameContext = TryParseGameContext(responseText);
            
            Debug.WriteLine($"Context gathering completed for input: {playerInput}");
            Debug.WriteLine($"Found {gameContext.RelevantEntities.Count} relevant entities");
            Debug.WriteLine($"Found {gameContext.VectorStoreData.Count} vector store results");
            
            return gameContext;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in context gathering: {ex.Message}");
            
            // Return a minimal context object on error
            return new GameContext
            {
                ContextSummary = $"Error gathering context: {ex.Message}",
                RecommendedActions = ["Proceed with available information"]
            };
        }
    }

    public async Task<GamePhase> GetCurrentPhaseAsync()
    {
        if (_currentPhase == default(GamePhase))
        {
            await RefreshPhaseAsync();
        }
        return _currentPhase;
    }

    public async Task RefreshPhaseAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var newPhase = gameState?.CurrentPhase ?? GamePhase.GameCreation;
        
        var oldPhase = _currentPhase;
        var phaseChanged = _currentPhase != newPhase;
        
        // Handle phase transition if needed
        if (phaseChanged && _currentPhase != default(GamePhase))
        {
            await _historyManager.HandlePhaseTransitionAsync(oldPhase, newPhase);
            Debug.WriteLine($"Phase changed from {oldPhase} to: {newPhase}");
        }
        
        _currentPhase = newPhase;
    }

    private GameContext TryParseGameContext(string responseText)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var gameContext = JsonSerializer.Deserialize<GameContext>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return gameContext ?? new GameContext();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse GameContext JSON: {ex.Message}");
        }

        // Fallback: create a basic context object
        return new GameContext
        {
            ContextSummary = "Failed to parse context response",
            RecommendedActions = ["Proceed with minimal context"]
        };
    }
}