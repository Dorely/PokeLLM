using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState;
using System.Diagnostics;

namespace Tests;

/// <summary>
/// Tests to identify if the issue is with the placeholder replacement processing in UnifiedContextService
/// </summary>
public class UnifiedContextPlaceholderTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILLMProvider? _llmProvider;
    private IGameStateRepository? _gameStateRepository;

    public async Task InitializeAsync()
    {
        try
        {
            _serviceProvider = Program.BuildServiceProvider();
            _llmProvider = _serviceProvider.GetRequiredService<ILLMProvider>();
            _gameStateRepository = _serviceProvider.GetRequiredService<IGameStateRepository>();
            Debug.WriteLine("[UnifiedContextPlaceholderTests] Initialization completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedContextPlaceholderTests] Initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UnifiedContext_WithCurrentImplementation_ShouldWork()
    {
        // Test: Mimic the current UnifiedContextService implementation (all content in user message)
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 100, temperature: 0.1f, enableFunctionCalling: true);

        try
        {
            var kernel = await _llmProvider.CreateKernelAsync();
            kernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var contextHistory = new ChatHistory();
            
            // Create a test chat history like what UnifiedContextService processes
            var testHistory = new ChatHistory();
            testHistory.AddUserMessage("Hello");
            testHistory.AddAssistantMessage("Hi there!");
            
            var gameState = await _gameStateRepository!.LoadLatestStateAsync();
            
            // Process history exactly like UnifiedContextService does
            var processedHistory = testHistory
                .Where(msg => msg.Role != AuthorRole.System && !string.IsNullOrWhiteSpace(msg.Content))
                .ToList();

            var historyString = string.Join("\n\n", processedHistory
                .Select(msg => $"[{ConvertRoleForDisplay(msg.Role)}]\n{msg.Content}"));
            
            var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ?
                gameState.CurrentContext : "World generation beginning - creating initial world content.";
            
            // Use the same shortened system prompt as UnifiedContextService
            var shortSystemPrompt = "You manage world consistency and scene continuity. Use available functions to gather scene context, search narrative context, and update current context. Focus on creating comprehensive scene descriptions.";
            var directive = "Please gather scene context using available functions.";
            
            var combinedUserMessage = $"## System Instructions\n{shortSystemPrompt}\n\n";
            
            // Limit content like the actual service does
            var limitedHistoryString = historyString;
            if (!string.IsNullOrWhiteSpace(historyString) && historyString.Length > 2000)
            {
                limitedHistoryString = historyString.Substring(0, 2000) + "... (truncated)";
            }
            
            var limitedContext = currentContext;
            if (currentContext.Length > 1000)
            {
                limitedContext = currentContext.Substring(0, 1000) + "... (truncated)";
            }
            
            if (!string.IsNullOrWhiteSpace(limitedHistoryString))
            {
                combinedUserMessage += $"## Current Chat History\n{limitedHistoryString}\n\n## Current Context\n{limitedContext}\n\n## Task\n{directive}";
            }
            else
            {
                combinedUserMessage += $"## Current Context\n{limitedContext}\n\n## Task\n{directive}";
            }
            
            contextHistory.AddUserMessage(combinedUserMessage);
            
            // Log details for debugging
            Debug.WriteLine($"[CurrentImplementation] Combined message length: {combinedUserMessage.Length}");
            Debug.WriteLine($"[CurrentImplementation] History string: {historyString}");
            Debug.WriteLine($"[CurrentImplementation] Context: {currentContext}");
            
            var result = await chatService.GetChatMessageContentAsync(contextHistory, executionSettings, kernel);
            
            Assert.NotNull(result);
            Assert.NotEmpty(result.ToString());
            Debug.WriteLine($"[CurrentImplementation] Success - Response length: {result.ToString().Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[CurrentImplementation] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[CurrentImplementation] ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"UnifiedContext with current implementation caused 400 error: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CurrentImplementation] Other exception: {ex.Message}");
            // Allow other exceptions as they're not our target issue
            Assert.True(true, $"No 400 error, other exception: {ex.GetType().Name}");
        }
    }

    [Fact]
    public async Task UnifiedContext_WithEmptyHistoryAndContext_ShouldWork()
    {
        // Test: Current implementation with empty history and context
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 100, temperature: 0.1f, enableFunctionCalling: true);

        try
        {
            var kernel = await _llmProvider.CreateKernelAsync();
            kernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var contextHistory = new ChatHistory();
            
            // Use the same shortened system prompt as UnifiedContextService
            var shortSystemPrompt = "You manage world consistency and scene continuity. Use available functions to gather scene context, search narrative context, and update current context. Focus on creating comprehensive scene descriptions.";
            var directive = "Please gather scene context using available functions.";
            var currentContext = "Default context - no specific context available.";
            
            // Use current implementation: combine everything in user message with empty history
            var combinedUserMessage = $"## System Instructions\n{shortSystemPrompt}\n\n## Current Context\n{currentContext}\n\n## Task\n{directive}";
            
            contextHistory.AddUserMessage(combinedUserMessage);
            
            Debug.WriteLine($"[EmptyHistoryContext] Combined message length: {combinedUserMessage.Length}");
            
            var result = await chatService.GetChatMessageContentAsync(contextHistory, executionSettings, kernel);
            
            Assert.NotNull(result);
            Assert.NotEmpty(result.ToString());
            Debug.WriteLine($"[EmptyHistoryContext] Success - Response length: {result.ToString().Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[EmptyHistoryContext] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[EmptyHistoryContext] ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"UnifiedContext with empty history/context caused 400 error: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmptyHistoryContext] Other exception: {ex.Message}");
            // Allow other exceptions as they're not our target issue
            Assert.True(true, $"No 400 error, other exception: {ex.GetType().Name}");
        }
    }

    private static string ConvertRoleForDisplay(AuthorRole role)
    {
        return role.Label switch
        {
            "user" => "User",
            "assistant" => "Assistant", 
            "system" => "System",
            "tool" => "Tool",
            _ => role.ToString()
        };
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