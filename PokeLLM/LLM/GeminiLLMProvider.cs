using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Logging;

namespace PokeLLM.Game.LLM;

/// <summary>
/// Google Gemini implementation of ILLMProvider
/// </summary>
public class GeminiLLMProvider : ILLMProvider
{
    private readonly ModelConfig _config;
    private readonly IDebugLogger _debugLogger;

    public GeminiLLMProvider(
        IOptions<ModelConfig> options,
        IDebugLogger debugLogger)
    {
        _config = options.Value;
        _debugLogger = debugLogger;
        
        _debugLogger.LogDebug("[GeminiLLMProvider] Gemini LLM Provider initialized");
        _debugLogger.LogDebug($"[GeminiLLMProvider] Configuration - Model: {_config.ModelId ?? "gemini-2.5-flash"}, API Key Length: {_config.ApiKey?.Length ?? 0}");
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        _debugLogger.LogDebug("[GeminiLLMProvider] Creating Gemini kernel...");
        
        var kernelBuilder = Kernel.CreateBuilder();
        
        var modelId = _config.ModelId ?? "gemini-2.5-flash";
        
        // Add Gemini chat completion
        _debugLogger.LogDebug($"[GeminiLLMProvider] Adding Gemini chat completion service with model: {modelId}");
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddGoogleAIGeminiChatCompletion(
            modelId: modelId,
            apiKey: _config.ApiKey
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var kernel = kernelBuilder.Build();
        _debugLogger.LogDebug("[GeminiLLMProvider] Gemini kernel created successfully");
        
        return kernel;
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        _debugLogger.LogDebug("[GeminiLLMProvider] Embedding generator requested - Gemini doesn't support embeddings, returning null");
        
        // Gemini doesn't have embedding support in Semantic Kernel yet
        // Return null to indicate embedding should be handled by separate provider
        return null;
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        _debugLogger.LogDebug($"[GeminiLLMProvider] Creating execution settings - MaxTokens: {maxTokens}, Temperature: {temperature}, Function Calling: {enableFunctionCalling}");
        
        var settings = new GeminiPromptExecutionSettings
        {
            MaxTokens = maxTokens,
            Temperature = temperature,
            ToolCallBehavior = enableFunctionCalling ? GeminiToolCallBehavior.AutoInvokeKernelFunctions : GeminiToolCallBehavior.EnableKernelFunctions
        };
        
        _debugLogger.LogDebug($"[GeminiLLMProvider] Execution settings created with ToolCallBehavior: {settings.ToolCallBehavior}");
        return settings;
    }
}