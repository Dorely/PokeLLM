using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Logging;

namespace PokeLLM.Game.LLM;

/// <summary>
/// OpenAI-specific implementation of ILowLevelLLMProvider that only handles OpenAI library interactions
/// </summary>
public class OpenAiLLMProvider : ILLMProvider
{
    private readonly ModelConfig _config;
    private readonly IDebugLogger _debugLogger;

    public OpenAiLLMProvider(
        IOptions<ModelConfig> options,
        IDebugLogger debugLogger)
    {
        _config = options.Value;
        _debugLogger = debugLogger;
        
        _debugLogger.LogDebug("[OpenAiLLMProvider] OpenAI LLM Provider initialized");
        _debugLogger.LogDebug($"[OpenAiLLMProvider] Configuration - Model: {_config.ModelId}, Embedding Model: {_config.EmbeddingModelId}, API Key Length: {_config.ApiKey?.Length ?? 0}");
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        _debugLogger.LogDebug("[OpenAiLLMProvider] Creating OpenAI kernel...");
        
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Add OpenAI chat completion
        _debugLogger.LogDebug($"[OpenAiLLMProvider] Adding OpenAI chat completion service with model: {_config.ModelId}");
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: _config.ModelId,
            apiKey: _config.ApiKey
        );

        // Add OpenAI embedding generator
        _debugLogger.LogDebug($"[OpenAiLLMProvider] Adding OpenAI embedding generator with model: {_config.EmbeddingModelId}");
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            modelId: _config.EmbeddingModelId,
            apiKey: _config.ApiKey
        );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var kernel = kernelBuilder.Build();
        _debugLogger.LogDebug("[OpenAiLLMProvider] OpenAI kernel created successfully");
        
        return kernel;
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        _debugLogger.LogDebug("[OpenAiLLMProvider] Creating OpenAI embedding generator...");
        
        // Create a minimal kernel just for the embedding generator
        var kernelBuilder = Kernel.CreateBuilder();
        
        _debugLogger.LogDebug($"[OpenAiLLMProvider] Configuring embedding generator with model: {_config.EmbeddingModelId}");
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            modelId: _config.EmbeddingModelId,
            apiKey: _config.ApiKey
        );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        var kernel = kernelBuilder.Build();
        var generator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        
        _debugLogger.LogDebug("[OpenAiLLMProvider] OpenAI embedding generator created successfully");
        return generator;
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        _debugLogger.LogDebug($"[OpenAiLLMProvider] Creating execution settings - MaxTokens: {maxTokens}, Temperature: {temperature}, Function Calling: {enableFunctionCalling}");
        
        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = maxTokens,
            Temperature = temperature,
            ToolCallBehavior = enableFunctionCalling ? ToolCallBehavior.AutoInvokeKernelFunctions : ToolCallBehavior.EnableKernelFunctions
        };
        
        _debugLogger.LogDebug($"[OpenAiLLMProvider] Execution settings created with ToolCallBehavior: {settings.ToolCallBehavior}");
        return settings;
    }
}