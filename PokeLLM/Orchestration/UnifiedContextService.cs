using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.GameState;

namespace PokeLLM.Game.Orchestration;

public interface IUnifiedContextService
{
    Task<string> RunContextManagementAsync(ChatHistory history, string directive, CancellationToken cancellationToken = default);
}

public class UnifiedContextService : IUnifiedContextService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private Kernel _contextKernel;

    public UnifiedContextService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider)
    {
        _llmProvider = llmProvider;
        _serviceProvider = serviceProvider;
        _gameStateRepository = gameStateRepository;
        InitializeKernel();
    }

    private void InitializeKernel()
    {
        _contextKernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        _contextKernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
    }

    public async Task<string> RunContextManagementAsync(ChatHistory history, string directive, CancellationToken cancellationToken = default)
    {
        var contextHistory = new ChatHistory();
        var systemPrompt = await LoadSystemPromptAsync("UnifiedContextSubroutine");

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        var historyString = string.Join("\n\n", history.Select(msg => $"[{msg.Role}]\n{msg.Content}"));

        var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ?
            gameState.CurrentContext : "World generation beginning - creating initial world content.";

        //Replace {{context}} placeholder with actual context
        //TODO replace this using correct semantic kernel syntax
        systemPrompt = systemPrompt.Replace("{{history}}", historyString);
        systemPrompt = systemPrompt.Replace("{{context}}", currentContext);

        contextHistory.AddSystemMessage(systemPrompt);
        contextHistory.AddUserMessage(directive);

        var chatService = _contextKernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 3000,
            temperature: 0.3f,
            enableFunctionCalling: true);

        var result = await chatService.GetChatMessageContentAsync(
            contextHistory, executionSettings, _contextKernel, cancellationToken);

        return result.ToString();
    }

    private async Task<string> LoadSystemPromptAsync(string promptName)
    {
        var promptPath = GetPromptPath(promptName);
        if (File.Exists(promptPath))
        {
            return await File.ReadAllTextAsync(promptPath);
        }
        return $"System prompt for {promptName} not found.";
    }

    private string GetPromptPath(string promptName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDirectory, "Prompts", $"{promptName}.md");
    }
}