using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Game.Configuration;
using PokeLLM.Logging;

namespace PokeLLM.Game.LLM;

/// <summary>
/// Ollama-specific implementation of ILLMProvider that handles Ollama library interactions
/// </summary>
public class OllamaLLMProvider : ILLMProvider
{
    private readonly ModelConfig _config;
    private readonly Uri _endpoint;
    private readonly IDebugLogger _debugLogger;

    public OllamaLLMProvider(
        IOptions<ModelConfig> options,
        IDebugLogger debugLogger)
    {
        _config = options.Value;
        _debugLogger = debugLogger;
        
        // Default to local Ollama endpoint if not specified
        _endpoint = string.IsNullOrEmpty(_config.ApiKey) 
            ? new Uri("http://localhost:11434")
            : new Uri(_config.ApiKey);
            
        _debugLogger.LogDebug("[OllamaLLMProvider] Ollama LLM Provider initialized");
        _debugLogger.LogDebug($"[OllamaLLMProvider] Configuration - Model: {_config.ModelId ?? "gemma3"}, Embedding Model: {_config.EmbeddingModelId ?? "nomic-embed-text"}, Endpoint: {_endpoint}");
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        _debugLogger.LogDebug("[OllamaLLMProvider] Creating Ollama kernel...");
        
        var kernelBuilder = Kernel.CreateBuilder();
        
        var chatModelId = _config.ModelId ?? "gemma3";
        var embeddingModelId = _config.EmbeddingModelId ?? "nomic-embed-text";
        
        // Add Ollama chat completion
        _debugLogger.LogDebug($"[OllamaLLMProvider] Adding Ollama chat completion service with model: {chatModelId} at endpoint: {_endpoint}");
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOllamaChatCompletion(
            modelId: chatModelId,
            endpoint: _endpoint
        );
        
        // Add Ollama embedding generator
        _debugLogger.LogDebug($"[OllamaLLMProvider] Adding Ollama embedding generator with model: {embeddingModelId} at endpoint: {_endpoint}");
        kernelBuilder.AddOllamaEmbeddingGenerator(
            modelId: embeddingModelId,
            endpoint: _endpoint
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var kernel = kernelBuilder.Build();
        _debugLogger.LogDebug("[OllamaLLMProvider] Ollama kernel created successfully");
        
        return kernel;
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        _debugLogger.LogDebug("[OllamaLLMProvider] Creating Ollama embedding generator...");
        
        var embeddingModelId = _config.EmbeddingModelId ?? "nomic-embed-text";
        
        // Create a minimal kernel just for the embedding generator
        var kernelBuilder = Kernel.CreateBuilder();
        
        _debugLogger.LogDebug($"[OllamaLLMProvider] Configuring embedding generator with model: {embeddingModelId} at endpoint: {_endpoint}");
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOllamaEmbeddingGenerator(
            modelId: embeddingModelId,
            endpoint: _endpoint
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        var kernel = kernelBuilder.Build();
        var generator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        
        _debugLogger.LogDebug("[OllamaLLMProvider] Ollama embedding generator created successfully");
        return generator;
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        _debugLogger.LogDebug($"[OllamaLLMProvider] Creating execution settings - MaxTokens: {maxTokens}, Temperature: {temperature}, Function Calling: {enableFunctionCalling}");
        
        var settings = new OllamaPromptExecutionSettings
        {
            NumPredict = maxTokens,
            Temperature = temperature,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
        
        _debugLogger.LogDebug($"[OllamaLLMProvider] Execution settings created with FunctionChoiceBehavior: {settings.FunctionChoiceBehavior}");
        return settings;
    }
}