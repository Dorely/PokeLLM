using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace PokeLLM.Game.LLM;

/// <summary>
/// OpenAI-specific implementation of ILowLevelLLMProvider that only handles OpenAI library interactions
/// </summary>
public class OpenAiLLMProvider : ILLMProvider
{
    private readonly ModelConfig _config;

    public OpenAiLLMProvider(
        IOptions<ModelConfig> options)
    {
        _config = options.Value;
    }

    private HttpClient CreateHttpClient()
    {
        var timeoutSeconds = _config.RequestTimeoutSeconds ?? 600; // default 10 minutes
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        var kernelBuilder = Kernel.CreateBuilder();

        var httpClient = CreateHttpClient();
        
        // Add OpenAI chat completion
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: _config.ModelId,
            apiKey: _config.ApiKey,
            httpClient: httpClient
        );

        // Add OpenAI embedding generator
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            modelId: _config.EmbeddingModelId,
            apiKey: _config.ApiKey,
            httpClient: httpClient
        );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return kernelBuilder.Build();
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        // Create a minimal kernel just for the embedding generator
        var kernelBuilder = Kernel.CreateBuilder();
        var httpClient = CreateHttpClient();
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            modelId: _config.EmbeddingModelId,
            apiKey: _config.ApiKey,
            httpClient: httpClient
        );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        var kernel = kernelBuilder.Build();
        return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        return new OpenAIPromptExecutionSettings
        {
            //MaxTokens = maxTokens,
            //Temperature = temperature,
            ToolCallBehavior = enableFunctionCalling ? ToolCallBehavior.AutoInvokeKernelFunctions : ToolCallBehavior.EnableKernelFunctions
        };
    }
}