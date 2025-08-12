using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState.Models;
using System.Runtime.CompilerServices;

namespace PokeLLM.Game.Orchestration;

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
        // Create fresh ChatHistory with updated system prompt
        var freshHistory = new ChatHistory();
        var systemPrompt = await LoadSystemPromptAsync("WorldGenerationPhase");

        // Inject CurrentContext into prompt using {{context}} variable
        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        var region = !string.IsNullOrEmpty(gameState.Region) ?
            gameState.Region : "Selected Region is missing. Please Return an error message.";

        //Replace {{region}} placeholder with actual region
        systemPrompt = systemPrompt.Replace("{{region}}", region);

        freshHistory.AddSystemMessage(systemPrompt);
        
        // Transfer existing conversation history (skip old system message if exists)
        var messagesToTransfer = _chatHistory.Where(msg => msg.Role != AuthorRole.System);
        foreach (var message in messagesToTransfer)
        {
            freshHistory.Add(message);
        }
        
        // Add new user message
        freshHistory.AddUserMessage(inputMessage);
        
        // Update the stored history
        _chatHistory = freshHistory;

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
        gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.GameTurnNumber++;
        await _gameStateRepository.SaveStateAsync(gameState);

        // Run Unified Context Management after turn
        await _unifiedContextService.RunContextManagementAsync(_chatHistory,
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