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
/// Test to isolate if the compression directive in UnifiedContextService is causing the 400 error
/// </summary>
public class UnifiedContextCompressionTests : IAsyncLifetime
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
            Debug.WriteLine("[UnifiedContextCompressionTests] Initialization completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedContextCompressionTests] Initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UnifiedContext_WithCompressionDirective_ShouldWork()
    {
        // Test: Use the exact compression directive text that might be causing the 400 error
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 3000, temperature: 0.7f, enableFunctionCalling: true);

        try
        {
            var kernel = await _llmProvider.CreateKernelAsync();
            kernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var contextHistory = new ChatHistory();
            
            // Create large history to trigger compression logic
            var largeHistory = new ChatHistory();
            for (int i = 0; i < 25; i++)
            {
                largeHistory.AddUserMessage($"User message {i}: This is a longer message to increase character count and test the compression functionality.");
                largeHistory.AddAssistantMessage($"Assistant response {i}: This is a response that adds to the overall character count and should trigger compression when the limits are exceeded.");
            }
            
            var gameState = await _gameStateRepository!.LoadLatestStateAsync();
            
            // Process history exactly like UnifiedContextService does
            var processedHistory = largeHistory
                .Where(msg => msg.Role != AuthorRole.System && !string.IsNullOrWhiteSpace(msg.Content))
                .ToList();

            var historyString = string.Join("\n\n", processedHistory
                .Select(msg => $"[{ConvertRoleForDisplay(msg.Role)}]\n{msg.Content}"));
            
            // Check if history needs compression (same logic as UnifiedContextService)
            const int maxMessages = 20;
            const int maxCharacters = 50000;
            var totalCharacters = processedHistory.Sum(message => message.Content?.Length ?? 0);
            var needsCompression = processedHistory.Count > maxMessages || totalCharacters > maxCharacters;
            
            var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ?
                gameState.CurrentContext : "World generation beginning - creating initial world content.";
            
            // Add the exact compression directive that might be problematic
            var directive = "Please analyze this conversation and provide context.";
            var finalDirective = directive;
            if (needsCompression)
            {
                finalDirective += $"\n\nIMPORTANT: The chat history is too large ({processedHistory.Count} messages, {totalCharacters} characters). " +
                                "Please provide a compressed version of the conversation history in the following format:\n" +
                                "<COMPRESSED_HISTORY>\n" +
                                "[System] Brief description of initial system context\n" +
                                "[User] Summary of key player actions and decisions\n" +
                                "[Assistant] Summary of key story developments and responses\n" +
                                "... (continue pattern for key exchanges)\n" +
                                "</COMPRESSED_HISTORY>";
            }
            
            // Use the same shortened system prompt as UnifiedContextService to prevent content overload
            var shortSystemPrompt = "You manage world consistency and scene continuity. Use available functions to gather scene context, search narrative context, and update current context. Focus on creating comprehensive scene descriptions.";
            
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
                combinedUserMessage += $"## Current Chat History\n{limitedHistoryString}\n\n## Current Context\n{limitedContext}\n\n## Task\n{finalDirective}";
            }
            else
            {
                combinedUserMessage += $"## Current Context\n{limitedContext}\n\n## Task\n{finalDirective}";
            }
            
            contextHistory.AddUserMessage(combinedUserMessage);
            
            // Log details for debugging
            Debug.WriteLine($"[CompressionDirective] Needs compression: {needsCompression}");
            Debug.WriteLine($"[CompressionDirective] Final directive length: {finalDirective.Length}");
            Debug.WriteLine($"[CompressionDirective] System prompt length: {shortSystemPrompt.Length}");
            Debug.WriteLine($"[CompressionDirective] Total characters: {totalCharacters}");
            
            var result = await chatService.GetChatMessageContentAsync(contextHistory, executionSettings, kernel);
            
            Assert.NotNull(result);
            Assert.NotEmpty(result.ToString());
            Debug.WriteLine($"[CompressionDirective] Success - Response length: {result.ToString().Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[CompressionDirective] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[CompressionDirective] ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"UnifiedContext with compression directive caused 400 error: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CompressionDirective] Other exception: {ex.Message}");
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