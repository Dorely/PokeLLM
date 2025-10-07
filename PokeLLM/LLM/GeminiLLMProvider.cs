using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;

namespace PokeLLM.Game.LLM;

/// <summary>
/// Google Gemini implementation of ILLMProvider
/// </summary>
public class GeminiLLMProvider : ILLMProvider
{
    private readonly ModelConfig _config;

    public GeminiLLMProvider(
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
        
        // Add Gemini chat completion
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddGoogleAIGeminiChatCompletion(
            modelId: _config.ModelId ?? "gemini-2.5-flash",
            apiKey: _config.ApiKey,
            httpClient: httpClient
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return kernelBuilder.Build();
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        // Gemini doesn't have embedding support in Semantic Kernel yet
        // Return null to indicate embedding should be handled by separate provider
        return null;
    }

    public PromptExecutionSettings GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false)
    {
        return new GeminiPromptExecutionSettings
        {
            MaxTokens = maxTokens,
            Temperature = temperature,
            ToolCallBehavior = enableFunctionCalling ? GeminiToolCallBehavior.AutoInvokeKernelFunctions : GeminiToolCallBehavior.EnableKernelFunctions
        };
    }
}