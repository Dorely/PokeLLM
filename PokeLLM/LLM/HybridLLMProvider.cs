using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.Configuration;

namespace PokeLLM.Game.LLM;

/// <summary>
/// Hybrid LLM provider that uses OpenAI for chat completion and Ollama for embeddings
/// </summary>
public class HybridLLMProvider : ILLMProvider
{
    private readonly HybridConfig _config;

    public HybridLLMProvider(IOptions<HybridConfig> options)
    {
        _config = options.Value;
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Add chat completion based on LLM provider
        switch (_config.LLM.Provider.ToLower())
        {
            case "openai":
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: _config.LLM.ModelId ?? "gpt-4o-mini",
                    apiKey: _config.LLM.ApiKey
                );
                break;
            case "ollama":
                var llmEndpoint = !string.IsNullOrEmpty(_config.LLM.Endpoint) 
                    ? new Uri(_config.LLM.Endpoint) 
                    : new Uri("http://localhost:11434");
#pragma warning disable SKEXP0070
                kernelBuilder.AddOllamaChatCompletion(
                    modelId: _config.LLM.ModelId ?? "llama3.1",
                    endpoint: llmEndpoint
                );
#pragma warning restore SKEXP0070
                break;
            default:
                throw new InvalidOperationException($"Unknown LLM provider: {_config.LLM.Provider}");
        }
        
        // Add embedding generator based on embedding provider
        switch (_config.Embedding.Provider.ToLower())
        {
            case "openai":
#pragma warning disable SKEXP0010
                kernelBuilder.AddOpenAIEmbeddingGenerator(
                    modelId: _config.Embedding.ModelId ?? "text-embedding-3-small",
                    apiKey: _config.Embedding.ApiKey
                );
#pragma warning restore SKEXP0010
                break;
            case "ollama":
                var embeddingEndpoint = !string.IsNullOrEmpty(_config.Embedding.Endpoint) 
                    ? new Uri(_config.Embedding.Endpoint) 
                    : new Uri("http://localhost:11434");
#pragma warning disable SKEXP0070
                kernelBuilder.AddOllamaEmbeddingGenerator(
                    modelId: _config.Embedding.ModelId ?? "nomic-embed-text",
                    endpoint: embeddingEndpoint
                );
#pragma warning restore SKEXP0070
                break;
            default:
                throw new InvalidOperationException($"Unknown embedding provider: {_config.Embedding.Provider}");
        }

        return kernelBuilder.Build();
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Create embedding generator based on embedding provider
        switch (_config.Embedding.Provider.ToLower())
        {
            case "openai":
#pragma warning disable SKEXP0010
                kernelBuilder.AddOpenAIEmbeddingGenerator(
                    modelId: _config.Embedding.ModelId ?? "text-embedding-3-small",
                    apiKey: _config.Embedding.ApiKey
                );
#pragma warning restore SKEXP0010
                break;
            case "ollama":
                var endpoint = !string.IsNullOrEmpty(_config.Embedding.Endpoint) 
                    ? new Uri(_config.Embedding.Endpoint) 
                    : new Uri("http://localhost:11434");
#pragma warning disable SKEXP0070
                kernelBuilder.AddOllamaEmbeddingGenerator(
                    modelId: _config.Embedding.ModelId ?? "nomic-embed-text",
                    endpoint: endpoint
                );
#pragma warning restore SKEXP0070
                break;
            default:
                throw new InvalidOperationException($"Unknown embedding provider: {_config.Embedding.Provider}");
        }
        
        var kernel = kernelBuilder.Build();
        return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        // Return settings based on LLM provider
        switch (_config.LLM.Provider.ToLower())
        {
            case "openai":
                return new OpenAIPromptExecutionSettings
                {
                    MaxTokens = maxTokens,
                    Temperature = temperature,
                    ToolCallBehavior = enableFunctionCalling ? ToolCallBehavior.AutoInvokeKernelFunctions : ToolCallBehavior.EnableKernelFunctions
                };
            case "ollama":
                return new OllamaPromptExecutionSettings
                {
                    NumPredict = maxTokens,
                    Temperature = temperature
                };
            default:
                throw new InvalidOperationException($"Unknown LLM provider: {_config.LLM.Provider}");
        }
    }

    public int GetEmbeddingDimensions()
    {
        return _config.Embedding.Dimensions > 0 ? _config.Embedding.Dimensions : 
               _config.Embedding.Provider.ToLower() == "openai" ? 1536 : 768;
    }
}