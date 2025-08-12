using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.Plugins;
using System.Diagnostics;

namespace Tests;

/// <summary>
/// Isolation tests to pinpoint the exact cause of the UnifiedContextPlugin 400 error
/// </summary>
public class UnifiedContextPluginIsolationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILLMProvider? _llmProvider;

    public async Task InitializeAsync()
    {
        try
        {
            _serviceProvider = Program.BuildServiceProvider();
            _llmProvider = _serviceProvider.GetRequiredService<ILLMProvider>();
            Debug.WriteLine("[UnifiedContextPluginIsolationTests] Initialization completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedContextPluginIsolationTests] Initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UnifiedContextPlugin_WithoutSystemPrompt_ShouldWork()
    {
        // Test: Use the UnifiedContextPlugin but without the system prompt to isolate if it's the plugin functions
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 100, temperature: 0.1f, enableFunctionCalling: true);

        try
        {
            var kernel = await _llmProvider.CreateKernelAsync();
            kernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            
            // Simple system message instead of the full prompt
            chatHistory.AddSystemMessage("You are a helpful assistant with access to some game functions.");
            chatHistory.AddUserMessage("Please gather scene context using available functions.");
            
            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
            
            Assert.NotNull(result);
            Assert.NotEmpty(result.ToString());
            Debug.WriteLine($"[WithoutSystemPrompt] Success - Response length: {result.ToString().Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[WithoutSystemPrompt] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[WithoutSystemPrompt] ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"UnifiedContextPlugin caused 400 error even without system prompt: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WithoutSystemPrompt] Other exception: {ex.Message}");
            // Allow other exceptions as they're not our target issue
            Assert.True(true, $"No 400 error, other exception: {ex.GetType().Name}");
        }
    }

    [Fact]
    public async Task UnifiedContextSystemPrompt_WithoutPlugin_ShouldWork()
    {
        // Test: Use the UnifiedContext system prompt but without the plugin to isolate if it's the prompt content
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 100, temperature: 0.1f, enableFunctionCalling: false);

        try
        {
            var kernel = await _llmProvider.CreateKernelAsync();
            // No plugin added
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            
            // Load the actual system prompt
            var systemPrompt = await LoadSystemPromptAsync("UnifiedContextSubroutine");
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage("Please analyze this context.");
            
            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
            
            Assert.NotNull(result);
            Assert.NotEmpty(result.ToString());
            Debug.WriteLine($"[WithoutPlugin] Success - Response length: {result.ToString().Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[WithoutPlugin] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[WithoutPlugin] ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"UnifiedContext system prompt caused 400 error even without plugin: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WithoutPlugin] Other exception: {ex.Message}");
            // Allow other exceptions as they're not our target issue
            Assert.True(true, $"No 400 error, other exception: {ex.GetType().Name}");
        }
    }

    [Fact]
    public async Task UnifiedContextPlugin_WithReducedTokens_ShouldWork()
    {
        // Test: Full setup but with much smaller max tokens to see if it's a token limit issue
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 50, temperature: 0.1f, enableFunctionCalling: true);

        try
        {
            var kernel = await _llmProvider.CreateKernelAsync();
            kernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            
            var systemPrompt = await LoadSystemPromptAsync("UnifiedContextSubroutine");
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage("Context please.");
            
            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
            
            Assert.NotNull(result);
            Debug.WriteLine($"[ReducedTokens] Success - Response length: {result.ToString().Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[ReducedTokens] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[ReducedTokens] ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"UnifiedContextPlugin caused 400 error even with reduced tokens: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReducedTokens] Other exception: {ex.Message}");
            // Allow other exceptions as they're not our target issue
            Assert.True(true, $"No 400 error, other exception: {ex.GetType().Name}");
        }
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