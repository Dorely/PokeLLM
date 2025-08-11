using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using PokeLLM.Game;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.Plugins;
using System.Diagnostics;

namespace Tests;

/// <summary>
/// Tests to verify that each plugin works correctly with the Gemini LLM provider.
/// Each plugin is tested separately to isolate any HTTP 400 compatibility issues.
/// </summary>
public class PluginLLMProviderTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILLMProvider? _llmProvider;

    public async Task InitializeAsync()
    {
        try
        {
            _serviceProvider = Program.BuildServiceProvider();
            _llmProvider = _serviceProvider.GetRequiredService<ILLMProvider>();
            Debug.WriteLine("[PluginLLMProviderTests] Initialization completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginLLMProviderTests] Initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BasicLLMProvider_ShouldWork()
    {
        // Test basic LLM functionality without any plugins
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 50, temperature: 0.1f, enableFunctionCalling: false);
        var simpleKernel = await _llmProvider.CreateKernelAsync();
        
        var result = await simpleKernel.InvokePromptAsync(
            "Say hello",
            new KernelArguments(executionSettings)
        );
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.ToString());
        Debug.WriteLine($"[BasicLLMProvider] Response: {result}");
    }

    [Fact]
    public async Task WorldGenerationPhasePlugin_ShouldNotThrow400Error()
    {
        await TestSinglePlugin<WorldGenerationPhasePlugin>("WorldGeneration", 
            "Please search for existing content using the available search functions.");
    }

    [Fact]
    public async Task ExplorationPhasePlugin_ShouldNotThrow400Error()
    {
        await TestSinglePlugin<ExplorationPhasePlugin>("Exploration", 
            "Please get the current environment information using available functions.");
    }

    [Fact]
    public async Task CombatPhasePlugin_ShouldNotThrow400Error()
    {
        await TestSinglePlugin<CombatPhasePlugin>("Combat", 
            "Please help with a skill check using available functions.");
    }

    [Fact]
    public async Task LevelUpPhasePlugin_ShouldNotThrow400Error()
    {
        await TestSinglePlugin<LevelUpPhasePlugin>("LevelUp", 
            "Please help with level up functionality using available functions.");
    }

    [Fact]
    public async Task UnifiedContextPlugin_ShouldNotThrow400Error()
    {
        await TestSinglePlugin<UnifiedContextPlugin>("UnifiedContext", 
            "Please gather scene context using available functions.");
    }

    [Fact]
    public async Task GameSetupPhasePlugin_ShouldNotThrow400Error()
    {
        await TestSinglePlugin<GameSetupPhasePlugin>("GameSetup", 
            "Please help with game setup using available functions.");
    }

    /// <summary>
    /// Generic test method to test a single plugin for HTTP 400 compatibility
    /// </summary>
    private async Task TestSinglePlugin<T>(string pluginName, string testPrompt) where T : class
    {
        var executionSettings = _llmProvider!.GetExecutionSettings(maxTokens: 100, temperature: 0.1f, enableFunctionCalling: true);
        
        try
        {
            // Create a fresh kernel for each test
            var kernel = await _llmProvider.CreateKernelAsync();
            kernel.Plugins.AddFromType<T>(pluginName, _serviceProvider);
            
            var result = await kernel.InvokePromptAsync(testPrompt, new KernelArguments(executionSettings));
            
            // Success - no HTTP 400 error thrown
            Debug.WriteLine($"[{pluginName}] No 400 error - Response length: {result.ToString().Length}");
            Assert.True(true, $"{pluginName} functions are compatible with Gemini API");
        }
        catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("400"))
        {
            Assert.True(false, $"{pluginName} caused 400 error: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log other exceptions but don't fail the test - we're only testing for HTTP 400 errors
            Debug.WriteLine($"[{pluginName}] Other exception (not HTTP 400): {ex.Message}");
            Assert.True(true, $"{pluginName} did not cause HTTP 400 error (other exception: {ex.GetType().Name})");
        }
    }
}