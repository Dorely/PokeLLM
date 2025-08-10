using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Plugins;
using System.Runtime.CompilerServices;

namespace PokeLLM.Game.Orchestration;

public interface IUnifiedContextService
{
    Task<string> RunContextManagementAsync(string directive, CancellationToken cancellationToken = default);
}

public class UnifiedContextService : IUnifiedContextService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IServiceProvider _serviceProvider;
    private Kernel _contextKernel;

    public UnifiedContextService(
        ILLMProvider llmProvider,
        IServiceProvider serviceProvider)
    {
        _llmProvider = llmProvider;
        _serviceProvider = serviceProvider;
        InitializeKernel();
    }

    private void InitializeKernel()
    {
        _contextKernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        _contextKernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
    }

    public async Task<string> RunContextManagementAsync(string directive, CancellationToken cancellationToken = default)
    {
        var contextHistory = new ChatHistory();
        var systemPrompt = await LoadSystemPromptAsync("UnifiedContextSubroutine");
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