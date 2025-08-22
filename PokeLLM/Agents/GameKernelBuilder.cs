using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.VectorStore;
using PokeLLM.Memory;

namespace PokeLLM.Agents;

public class GameKernelBuilder
{
    private readonly IServiceCollection _services;

    public GameKernelBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public Kernel Build()
    {
        var kernelBuilder = Kernel.CreateBuilder();

        // Configure AI services
        ConfigureAIServices(kernelBuilder);
        
        // Configure plugins
        ConfigurePlugins(kernelBuilder);
        
        // Configure logging
        ConfigureLogging(kernelBuilder);

        // Build the kernel
        var kernel = kernelBuilder.Build();

        // Register the kernel in the service collection for DI
        _services.AddSingleton(kernel);
        
        // Register memory components
        _services.AddMemoryComponents();
        
        // Register memory-enabled thread factory
        _services.AddSingleton<MemoryEnabledAgentThreadFactory>();

        return kernel;
    }

    private void ConfigureAIServices(IKernelBuilder kernelBuilder)
    {
        // For now, use a simple OpenAI configuration
        // In a full implementation, this would use the FlexibleProviderConfig
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "test-key";
        
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "gpt-4o-mini",
            apiKey: apiKey);

        // Configure embedding service for memory operations
        kernelBuilder.AddOpenAITextEmbeddingGeneration(
            modelId: "text-embedding-ada-002",
            apiKey: apiKey);
    }

    private void ConfigurePlugins(IKernelBuilder kernelBuilder)
    {
        // Register game-specific plugins that will be shared across agents
        // These will be configured later when we implement the rules engine
        
        // Example placeholder for future plugins:
        // kernelBuilder.Plugins.AddFromType<RulesPlugin>();
        // kernelBuilder.Plugins.AddFromType<ModulePlugin>();
        // kernelBuilder.Plugins.AddFromType<NarrativeStylePlugin>();
    }

    private void ConfigureLogging(IKernelBuilder kernelBuilder)
    {
        // Configure Semantic Kernel logging
        kernelBuilder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }
}