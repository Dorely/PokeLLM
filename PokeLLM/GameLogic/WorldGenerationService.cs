using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState.Models;
using System.Runtime.CompilerServices;
using PokeLLM.Game.Orchestration;

namespace PokeLLM.Game.GameLogic;

public interface IWorldGenerationService
{
    IAsyncEnumerable<string> RunWorldGenerationAsync(string inputMessage, CancellationToken cancellationToken = default);
    Task<bool> IsWorldGenerationCompleteAsync();
}

public class WorldGenerationService : IWorldGenerationService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnifiedContextService _unifiedContextService;
    private Kernel _worldGenerationKernel;
    private ChatHistory _chatHistory;

    public WorldGenerationService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider,
        IUnifiedContextService unifiedContextService)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
        _unifiedContextService = unifiedContextService;
        
        InitializeKernel();
        _chatHistory = new ChatHistory();
    }

    private void InitializeKernel()
    {
        _worldGenerationKernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        _worldGenerationKernel.Plugins.AddFromType<WorldGenerationPhasePlugin>("WorldGeneration", _serviceProvider);
    }

    public async IAsyncEnumerable<string> RunWorldGenerationAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Load system prompt if chat history is empty
        if (_chatHistory.Count == 0)
        {
            var systemPrompt = await LoadSystemPromptAsync("WorldGenerationPhase");
            _chatHistory.AddSystemMessage(systemPrompt);
        }

        // Add user message
        _chatHistory.AddUserMessage(inputMessage);

        var chatService = _worldGenerationKernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 4000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        var fullResponse = string.Empty;

        // Stream the response
        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(_chatHistory, executionSettings, _worldGenerationKernel, cancellationToken))
        {
            var content = chunk.Content ?? string.Empty;
            fullResponse += content;
            yield return content;
        }

        // Add assistant response to history
        _chatHistory.AddAssistantMessage(fullResponse);

        // Increment turn number
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.GameTurnNumber++;
        await _gameStateRepository.SaveStateAsync(gameState);

        // Run Unified Context Management after turn
        await _unifiedContextService.RunContextManagementAsync(
            $"WorldGeneration phase interaction completed. Update CurrentContext with world generation progress and current scene.",
            cancellationToken);
    }

    public async Task<bool> IsWorldGenerationCompleteAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        
        // World generation is complete if we have at least one location with a current location set
        return gameState.WorldLocations.Any() && !string.IsNullOrEmpty(gameState.CurrentLocationId);
    }

    private async Task<string> LoadSystemPromptAsync(string promptName)
    {
        var promptPath = GetPromptPath(promptName);
        if (File.Exists(promptPath))
        {
            var systemPrompt = await File.ReadAllTextAsync(promptPath);
            
            // Inject CurrentContext into prompt using {{context}} variable
            var gameState = await _gameStateRepository.LoadLatestStateAsync();
            var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ? 
                gameState.CurrentContext : "World generation beginning - creating initial world content.";
            
            // Replace {{context}} placeholder with actual context
            //TODO replace this using correct semantic kernel syntax
            systemPrompt = systemPrompt.Replace("{{context}}", currentContext);
            
            return systemPrompt;
        }
        return $"System prompt for {promptName} not found.";
    }

    private string GetPromptPath(string promptName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDirectory, "Prompts", $"{promptName}.md");
    }
}