using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.GameState.Interfaces;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.Plugins;

namespace Tests;

public class ProgramIntegrationTests
{
    [Fact]
    public void Program_ShouldUseServiceConfiguration_ForDependencyInjection()
    {
        // This test verifies that the Program.cs refactoring correctly uses ServiceConfiguration
        
        // Arrange & Act - Simulate what Program.cs does
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Assert - Verify the same services that Program.cs expects are available
        Assert.NotNull(provider.GetRequiredService<ILLMProvider>());
        Assert.NotNull(provider.GetRequiredService<IGameStateRepository>());
        Assert.NotNull(provider.GetRequiredService<IConfiguration>());
    }

    [Fact]
    public void ServiceConfiguration_CreateConfiguration_ShouldReturnWorkingConfiguration()
    {
        // Act
        var configuration = ServiceConfiguration.CreateConfiguration();

        // Assert
        Assert.NotNull(configuration);
        
        // Verify it can read from appsettings sections (even if empty)
        var openAiSection = configuration.GetSection("OpenAi");
        var qdrantSection = configuration.GetSection("Qdrant");
        
        Assert.NotNull(openAiSection);
        Assert.NotNull(qdrantSection);
    }

    [Fact]
    public void ServiceConfiguration_ShouldSupportChainedCalls()
    {
        // Arrange
        var configuration = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();

        // Act - Test that ConfigureServices returns the service collection for chaining
        var result = ServiceConfiguration.ConfigureServices(services, configuration);
        var provider = result.BuildServiceProvider();

        // Assert
        Assert.Same(services, result);
        Assert.NotNull(provider.GetRequiredService<ILLMProvider>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolveAllCoreServices()
    {
        // Arrange
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act & Assert - Test all core services can be resolved
        Assert.NotNull(provider.GetRequiredService<IConfiguration>());
        Assert.NotNull(provider.GetRequiredService<IGameStateRepository>());
        Assert.NotNull(provider.GetRequiredService<IVectorStoreService>());
        Assert.NotNull(provider.GetRequiredService<ILLMProvider>());
        
        // Test configuration options
        Assert.NotNull(provider.GetRequiredService<IOptions<ModelConfig>>());
        Assert.NotNull(provider.GetRequiredService<IOptions<QdrantConfig>>());
    }

    [Fact]
    public void DependencyInjection_ShouldResolveMultipleInstances()
    {
        // Arrange
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act - Get multiple instances of services
        var llm1 = provider.GetRequiredService<ILLMProvider>();
        var llm2 = provider.GetRequiredService<ILLMProvider>();
        var repo1 = provider.GetRequiredService<IGameStateRepository>();
        var repo2 = provider.GetRequiredService<IGameStateRepository>();
        var vector1 = provider.GetRequiredService<IVectorStoreService>();
        var vector2 = provider.GetRequiredService<IVectorStoreService>();

        // Assert - Verify singleton vs transient behavior
        Assert.NotSame(llm1, llm2); // ILLMProvider should be transient
        Assert.Same(repo1, repo2); // IGameStateRepository should be singleton
        Assert.NotSame(vector1, vector2); // IVectorStoreService should be transient
    }

    [Fact]
    public void Configuration_ShouldBindToConfigurationObjects()
    {
        // Arrange
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act
        var modelConfigOptions = provider.GetRequiredService<IOptions<ModelConfig>>();
        var qdrantConfigOptions = provider.GetRequiredService<IOptions<QdrantConfig>>();

        // Assert
        Assert.NotNull(modelConfigOptions.Value);
        Assert.NotNull(qdrantConfigOptions.Value);
        
        // Verify some expected default values from appsettings.json
        Assert.Equal("localhost", qdrantConfigOptions.Value.Host);
        Assert.Equal(6334, qdrantConfigOptions.Value.Port);
        Assert.Equal("text-embedding-3-small", modelConfigOptions.Value.EmbeddingModelId);
    }

    [Fact]
    public void ILLMProvider_ShouldReceiveAllRequiredDependencies()
    {
        // Arrange
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act
        var llmProvider = provider.GetRequiredService<ILLMProvider>();

        // Assert - Verify the LLM provider is properly constructed
        Assert.NotNull(llmProvider);
        
        // Test that it can get an embedding generator (validates Semantic Kernel setup)
        var embeddingGenerator = llmProvider.GetEmbeddingGenerator();
        Assert.NotNull(embeddingGenerator);
    }

    [Fact]
    public void ServiceConfiguration_ShouldHandleMissingConfiguration()
    {
        // This test ensures the DI setup is robust even with missing configuration values
        
        // Arrange - Create minimal configuration
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ModelId"] = "test-model",
                ["OpenAi:EmbeddingModelId"] = "test-embedding",
                ["Qdrant:Host"] = "test-host"
            });
        var config = configBuilder.Build();
        
        var services = new ServiceCollection();

        // Act & Assert - Should not throw during configuration
        var exception = Record.Exception(() => 
        {
            ServiceConfiguration.ConfigureServices(services, config);
            var provider = services.BuildServiceProvider();
            
            // Verify core services still resolve
            Assert.NotNull(provider.GetRequiredService<IGameStateRepository>());
            Assert.NotNull(provider.GetRequiredService<IVectorStoreService>());
            Assert.NotNull(provider.GetRequiredService<IConfiguration>());
        });
        
        Assert.Null(exception);
    }

    [Fact]
    public void PluginDependencies_ShouldBeResolvableFromServiceProvider()
    {
        // Arrange
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act - Test that plugin dependencies can be resolved
        var gameStateRepo = provider.GetRequiredService<IGameStateRepository>();
        var vectorStoreService = provider.GetRequiredService<IVectorStoreService>();

        // Assert - Verify plugins can be constructed with these dependencies
        Assert.NotNull(new PhaseTransitionPlugin(gameStateRepo));
        Assert.NotNull(new GameEnginePlugin(gameStateRepo));
        Assert.NotNull(new CharacterCreationPlugin(gameStateRepo));
        Assert.NotNull(new DicePlugin(gameStateRepo));
        Assert.NotNull(new VectorStorePlugin(vectorStoreService));
    }

    [Fact]
    public void ServiceProvider_ShouldDisposeProperlyWithoutErrors()
    {
        // Arrange
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act - Resolve some services to initialize them
        var llm = provider.GetRequiredService<ILLMProvider>();
        var repo = provider.GetRequiredService<IGameStateRepository>();
        var vector = provider.GetRequiredService<IVectorStoreService>();

        // Assert - Disposal should not throw
        var exception = Record.Exception(() => provider.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void AllServicesFromProgramCs_ShouldBeResolvable()
    {
        // This test specifically mirrors what Program.cs does to ensure compatibility
        
        // Arrange - Exactly mirror Program.cs setup
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);
        var provider = services.BuildServiceProvider();

        // Act & Assert - Get the exact same services that Program.cs requires
        var llm = provider.GetRequiredService<ILLMProvider>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();

        Assert.NotNull(llm);
        Assert.NotNull(gameStateRepository);
        
        // Verify the services are functional (basic smoke test)
        Assert.NotNull(llm.GetEmbeddingGenerator());
    }
}