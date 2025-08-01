using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Orchestration;
using PokeLLM.Game.Plugins;

namespace PokeLLM.Game.Configuration;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add configuration
        services.AddSingleton(configuration);
        services.Configure<ModelConfig>(configuration.GetSection("OpenAi"));
        services.Configure<QdrantConfig>(configuration.GetSection("Qdrant"));

        // Register IEmbeddingGenerator separately to break circular dependency
        services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider => 
        {
            var options = serviceProvider.GetRequiredService<IOptions<ModelConfig>>();
            
            // Ensure we have valid configuration values or use defaults
            var apiKey = !string.IsNullOrWhiteSpace(options.Value.ApiKey) ? options.Value.ApiKey : "test-api-key";
            var embeddingModelId = !string.IsNullOrWhiteSpace(options.Value.EmbeddingModelId) ? options.Value.EmbeddingModelId : "text-embedding-3-small";
            
            // Create a minimal kernel just for the embedding generator
            var kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            kernelBuilder.AddOpenAIEmbeddingGenerator(
                modelId: embeddingModelId,
                apiKey: apiKey
            );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            
            var kernel = kernelBuilder.Build();
            return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        });

        // Add core services (order matters to avoid circular dependencies)
        services.AddSingleton<IGameStateRepository, GameStateRepository>();
        services.AddTransient<IVectorStoreService, QdrantVectorStoreService>();

        // Register Game Logic Services
        services.AddTransient<IGameLogicService, GameLogicService>();
        
        // Register all plugins
        services.AddTransient<GameCreationPhasePlugin>();
        services.AddTransient<CharacterCreationPhasePlugin>();
        services.AddTransient<WorldGenerationPhasePlugin>();
        services.AddTransient<ExplorationPhasePlugin>();
        services.AddTransient<CombatPhasePlugin>();
        services.AddTransient<LevelUpPhasePlugin>();
        services.AddTransient<ContextGatheringPlugin>();
        services.AddTransient<ContextManagementPlugin>();
        services.AddTransient<ChatManagementPlugin>();
        
        // Register OpenAI-specific LLM provider (low-level)
        services.AddTransient<OpenAiLLMProvider>();
        // Register ILLMProvider as OpenAiLLMProvider for DI
        services.AddTransient<ILLMProvider, OpenAiLLMProvider>();
        
        // Register the main orchestration service
        services.AddTransient<IOrchestrationService, OrchestrationService>();

        return services;
    }

    public static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .Build();
    }
}