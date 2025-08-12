using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game;
using PokeLLM.GameState;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.Orchestration;
using System.Diagnostics;

namespace Tests;

/// <summary>
/// Tests specifically for UnifiedContextService to reproduce and diagnose Gemini 400 errors
/// </summary>
public class UnifiedContextServiceTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private IUnifiedContextService? _unifiedContextService;
    private IGameStateRepository? _gameStateRepository;

    public async Task InitializeAsync()
    {
        try
        {
            _serviceProvider = Program.BuildServiceProvider();
            _unifiedContextService = _serviceProvider.GetRequiredService<IUnifiedContextService>();
            _gameStateRepository = _serviceProvider.GetRequiredService<IGameStateRepository>();
            Debug.WriteLine("[UnifiedContextServiceTests] Initialization completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedContextServiceTests] Initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task RunContextManagementAsync_WithMinimalHistory_ShouldNotThrow400Error()
    {
        // Arrange - Create minimal chat history to test basic functionality
        var history = new ChatHistory();
        history.AddUserMessage("Hello");
        history.AddAssistantMessage("Hi there!");
        
        var directive = "Please analyze this conversation and provide context.";

        try
        {
            // Act - This should reproduce the 400 error
            var result = await _unifiedContextService!.RunContextManagementAsync(history, directive);
            
            // Assert - If we get here, no 400 error occurred
            Assert.NotNull(result);
            Assert.NotEmpty(result.Response);
            Debug.WriteLine($"[MinimalHistory] Success - Response length: {result.Response.Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[MinimalHistory] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[MinimalHistory] 400 Error ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"Gemini returned 400 error: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MinimalHistory] Other exception: {ex.Message}");
            throw; // Re-throw other exceptions for investigation
        }
    }

    [Fact]
    public async Task RunContextManagementAsync_WithLargeHistory_ShouldTriggerCompression()
    {
        // Arrange - Create large history to trigger compression
        var history = new ChatHistory();
        
        // Add many messages to trigger compression (more than 20 messages)
        for (int i = 0; i < 25; i++)
        {
            history.AddUserMessage($"User message {i}: This is a longer message to increase character count and test the compression functionality.");
            history.AddAssistantMessage($"Assistant response {i}: This is a response that adds to the overall character count and should trigger compression when the limits are exceeded.");
        }
        
        var directive = "Please compress this conversation history.";

        try
        {
            // Act - This should test the compression path that might be causing 400 errors
            var result = await _unifiedContextService!.RunContextManagementAsync(history, directive);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Response);
            Assert.True(result.HistoryWasCompressed, "History should have been compressed due to size");
            Debug.WriteLine($"[LargeHistory] Success - Response length: {result.Response.Length}");
            Debug.WriteLine($"[LargeHistory] Compression occurred: {result.HistoryWasCompressed}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[LargeHistory] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[LargeHistory] 400 Error ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"Gemini returned 400 error during compression: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LargeHistory] Other exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunContextManagementAsync_WithEmptyHistory_ShouldNotThrow400Error()
    {
        // Arrange - Test with empty history
        var history = new ChatHistory();
        var directive = "Please analyze this empty conversation.";

        try
        {
            // Act
            var result = await _unifiedContextService!.RunContextManagementAsync(history, directive);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Response);
            Debug.WriteLine($"[EmptyHistory] Success - Response length: {result.Response.Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[EmptyHistory] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[EmptyHistory] 400 Error ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"Gemini returned 400 error with empty history: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmptyHistory] Other exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunContextManagementAsync_WithSpecialCharacters_ShouldNotThrow400Error()
    {
        // Arrange - Test with special characters that might cause encoding issues
        var history = new ChatHistory();
        history.AddUserMessage("Test with special chars: éñüñ©®™€£¥§¶•‰¿¡«»");
        history.AddAssistantMessage("Response with emojis: 🎮🔥💯✨🌟⭐🎯🚀💪🎊");
        
        var directive = "Please analyze this conversation with special characters and emojis.";

        try
        {
            // Act
            var result = await _unifiedContextService!.RunContextManagementAsync(history, directive);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Response);
            Debug.WriteLine($"[SpecialChars] Success - Response length: {result.Response.Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[SpecialChars] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[SpecialChars] 400 Error ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"Gemini returned 400 error with special characters: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpecialChars] Other exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunContextManagementAsync_WithLongDirective_ShouldNotThrow400Error()
    {
        // Arrange - Test with a very long directive
        var history = new ChatHistory();
        history.AddUserMessage("Simple test message");
        history.AddAssistantMessage("Simple response");
        
        var longDirective = new string('x', 5000) + " Please analyze this conversation with this extremely long directive that contains many repeated characters to test if the Gemini API has issues with very long system prompts or user messages.";

        try
        {
            // Act
            var result = await _unifiedContextService!.RunContextManagementAsync(history, longDirective);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Response);
            Debug.WriteLine($"[LongDirective] Success - Response length: {result.Response.Length}");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Debug.WriteLine($"[LongDirective] 400 Error Details: {ex.Message}");
            Debug.WriteLine($"[LongDirective] 400 Error ResponseContent: {ex.ResponseContent}");
            Assert.Fail($"Gemini returned 400 error with long directive: {ex.Message}. ResponseContent: {ex.ResponseContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LongDirective] Other exception: {ex.Message}");
            throw;
        }
    }
}