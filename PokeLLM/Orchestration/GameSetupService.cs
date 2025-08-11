using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState.Models;
using System.Runtime.CompilerServices;

namespace PokeLLM.Game.Orchestration;

public interface IGameSetupService
{
    IAsyncEnumerable<string> RunGameSetupAsync(string inputMessage, CancellationToken cancellationToken = default);
    Task<bool> IsSetupCompleteAsync();
}

public class GameSetupService : IGameSetupService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnifiedContextService _unifiedContextService;
    private Kernel _gameSetupKernel;
    private ChatHistory _chatHistory;

    public GameSetupService(
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
        _gameSetupKernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        _gameSetupKernel.Plugins.AddFromType<GameSetupPhasePlugin>("GameSetup", _serviceProvider);
    }

    public async IAsyncEnumerable<string> RunGameSetupAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Load system prompt if chat history is empty
        if (_chatHistory.Count == 0)
        {
            var systemPrompt = await LoadSystemPromptAsync("GameSetupPhase");
            _chatHistory.AddSystemMessage(systemPrompt);
        }

        // Add user message
        _chatHistory.AddUserMessage(inputMessage);

        var chatService = _gameSetupKernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 4000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        var fullResponse = string.Empty;

        // Stream the response
        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(_chatHistory, executionSettings, _gameSetupKernel, cancellationToken))
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
        await _unifiedContextService.RunContextManagementAsync(_chatHistory,
            $"GameSetup phase interaction completed. Update CurrentContext with setup progress and current scene.",
            cancellationToken);
    }

    public async Task<bool> IsSetupCompleteAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return !string.IsNullOrEmpty(gameState.Region) &&
               !string.IsNullOrEmpty(gameState.Player.Name) &&
               !string.IsNullOrEmpty(gameState.Player.CharacterDetails.Class);
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
                gameState.CurrentContext : "No context available - beginning of new game setup.";
            
            // Replace {{context}} placeholder with actual context
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