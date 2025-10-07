using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Game.Configuration;
using System.Net.Http;

namespace PokeLLM.Game.LLM;

/// <summary>
/// Ollama-specific implementation of ILLMProvider that handles Ollama library interactions
/// </summary>
public class OllamaLLMProvider : ILLMProvider
{
    private readonly ModelConfig _config;
    private readonly Uri _endpoint;

    public OllamaLLMProvider(
        IOptions<ModelConfig> options)
    {
        _config = options.Value;
        // Default to local Ollama endpoint if not specified
        _endpoint = string.IsNullOrEmpty(_config.ApiKey) 
            ? new Uri("http://localhost:11434")
            : new Uri(_config.ApiKey);
    }

    private HttpClient CreateHttpClient()
    {
        var timeoutSeconds = _config.RequestTimeoutSeconds ?? 1800; // default 30 minutes for local models
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Add Ollama chat completion
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOllamaChatCompletion(
            modelId: _config.ModelId ?? "gemma3", // Default to gemma3 if not specified
            endpoint: _endpoint
        );
        
        // Add Ollama embedding generator
        kernelBuilder.AddOllamaEmbeddingGenerator(
            modelId: _config.EmbeddingModelId ?? "nomic-embed-text", // Default to nomic-embed-text if not specified
            endpoint: _endpoint
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return kernelBuilder.Build();
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        // Create a minimal kernel just for the embedding generator
        var kernelBuilder = Kernel.CreateBuilder();
        
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOllamaEmbeddingGenerator(
            modelId: _config.EmbeddingModelId ?? "nomic-embed-text", // Default to nomic-embed-text if not specified
            endpoint: _endpoint
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        var kernel = kernelBuilder.Build();
        return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        return new OllamaPromptExecutionSettings
        {
            NumPredict = maxTokens,
            Temperature = temperature,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
    }
}