using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.LLM;
using PokeLLM.Game.LLM.Interfaces;

namespace Tests;

public class HybridConfigurationTests
{
    [Fact]
    public void HybridConfiguration_ShouldResolveServices()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("Hybrid:LLM:Provider", "OpenAI"),
                new KeyValuePair<string, string>("Hybrid:LLM:ModelId", "gpt-4o-mini"),
                new KeyValuePair<string, string>("Hybrid:LLM:ApiKey", "test-key"),
                new KeyValuePair<string, string>("Hybrid:Embedding:Provider", "Ollama"),
                new KeyValuePair<string, string>("Hybrid:Embedding:ModelId", "nomic-embed-text"),
                new KeyValuePair<string, string>("Hybrid:Embedding:Endpoint", "http://localhost:11434"),
                new KeyValuePair<string, string>("Hybrid:Embedding:Dimensions", "768"),
                new KeyValuePair<string, string>("Qdrant:Host", "localhost"),
                new KeyValuePair<string, string>("Qdrant:Port", "6334")
            })
            .Build();

        var services = new ServiceCollection();
        
        // Act - Configure services using the hybrid mode
        ServiceConfiguration.ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify that services can be resolved
        var llmProvider = serviceProvider.GetService<ILLMProvider>();
        Assert.NotNull(llmProvider);
        Assert.IsType<HybridLLMProvider>(llmProvider);

        var hybridConfig = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<HybridConfig>>();
        Assert.NotNull(hybridConfig);
        Assert.Equal("OpenAI", hybridConfig.Value.LLM.Provider);
        Assert.Equal("Ollama", hybridConfig.Value.Embedding.Provider);
        Assert.Equal(768, hybridConfig.Value.Embedding.Dimensions);
    }

    [Fact]
    public async Task HybridLLMProvider_ShouldCreateKernel()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("Hybrid:LLM:Provider", "OpenAI"),
                new KeyValuePair<string, string>("Hybrid:LLM:ModelId", "gpt-4o-mini"),
                new KeyValuePair<string, string>("Hybrid:LLM:ApiKey", "test-key"),
                new KeyValuePair<string, string>("Hybrid:Embedding:Provider", "Ollama"),
                new KeyValuePair<string, string>("Hybrid:Embedding:ModelId", "nomic-embed-text"),
                new KeyValuePair<string, string>("Hybrid:Embedding:Endpoint", "http://localhost:11434"),
                new KeyValuePair<string, string>("Hybrid:Embedding:Dimensions", "768")
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<HybridConfig>(configuration.GetSection("Hybrid"));
        var serviceProvider = services.BuildServiceProvider();
        
        var hybridProvider = new HybridLLMProvider(serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HybridConfig>>());

        // Act & Assert - Should not throw
        var kernel = await hybridProvider.CreateKernelAsync();
        Assert.NotNull(kernel);

        var embeddingGenerator = hybridProvider.GetEmbeddingGenerator();
        Assert.NotNull(embeddingGenerator);
    }
}