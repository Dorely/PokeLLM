using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Orchestration;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.VectorStore;

namespace PokeLLM.Game.Configuration;

public static class ServiceConfiguration
{
    // Configuration: Change this to switch between providers
    // Valid values: "OpenAI", "Ollama", "Hybrid"
    private const string LLM_PROVIDER = "Hybrid"; // Use hybrid mode for OpenAI + Ollama embeddings

    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add configuration
        services.AddSingleton(configuration);
        
        // Configure based on the selected provider mode
        switch (LLM_PROVIDER)
        {
            case "OpenAI":
                services.Configure<ModelConfig>(config =>
                {
                    configuration.GetSection("OpenAi").Bind(config);
                    // Set default embedding dimensions for OpenAI if not specified
                    if (config.EmbeddingDimensions <= 0)
                        config.EmbeddingDimensions = 1536; // Default for text-embedding-3-small
                });
                break;
            case "Ollama":
                services.Configure<ModelConfig>(config =>
                {
                    configuration.GetSection("Ollama").Bind(config);
                    // Set default embedding dimensions for Ollama if not specified
                    if (config.EmbeddingDimensions <= 0)
                        config.EmbeddingDimensions = 768; // Default for nomic-embed-text
                });
                break;
            case "Hybrid":
                // Configure hybrid settings
                services.Configure<HybridConfig>(config =>
                {
                    configuration.GetSection("Hybrid").Bind(config);
                    
                    // Set defaults if not specified
                    if (string.IsNullOrEmpty(config.LLM.Provider))
                        config.LLM.Provider = "OpenAI";
                    if (string.IsNullOrEmpty(config.Embedding.Provider))
                        config.Embedding.Provider = "Ollama";
                    
                    // Set default embedding dimensions based on provider
                    if (config.Embedding.Dimensions <= 0)
                    {
                        config.Embedding.Dimensions = config.Embedding.Provider.ToLower() == "openai" ? 1536 : 768;
                    }
                    
                    // Set default models
                    if (string.IsNullOrEmpty(config.LLM.ModelId))
                        config.LLM.ModelId = config.LLM.Provider.ToLower() == "openai" ? "gpt-4o-mini" : "llama3.1";
                    if (string.IsNullOrEmpty(config.Embedding.ModelId))
                        config.Embedding.ModelId = config.Embedding.Provider.ToLower() == "openai" ? "text-embedding-3-small" : "nomic-embed-text";
                    
                    // Copy API keys from respective sections if not provided
                    if (string.IsNullOrEmpty(config.LLM.ApiKey) && config.LLM.Provider.ToLower() == "openai")
                        config.LLM.ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
                    if (string.IsNullOrEmpty(config.Embedding.ApiKey) && config.Embedding.Provider.ToLower() == "openai")
                        config.Embedding.ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
                    if (string.IsNullOrEmpty(config.Embedding.Endpoint) && config.Embedding.Provider.ToLower() == "ollama")
                        config.Embedding.Endpoint = configuration.GetValue<string>("Ollama:ApiKey") ?? "http://localhost:11434";
                });
                break;
            default:
                throw new InvalidOperationException($"Unknown LLM provider: {LLM_PROVIDER}");
        }
        
        services.Configure<QdrantConfig>(configuration.GetSection("Qdrant"));

        // Register IEmbeddingGenerator separately to break circular dependency
        services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider => 
        {
            switch (LLM_PROVIDER)
            {
                case "OpenAI":
                    var openAiOptions = serviceProvider.GetRequiredService<IOptions<ModelConfig>>();
                    return CreateOpenAIEmbeddingGenerator(openAiOptions);
                case "Ollama":
                    var ollamaOptions = serviceProvider.GetRequiredService<IOptions<ModelConfig>>();
                    return CreateOllamaEmbeddingGenerator(ollamaOptions);
                case "Hybrid":
                    var hybridOptions = serviceProvider.GetRequiredService<IOptions<HybridConfig>>();
                    return CreateHybridEmbeddingGenerator(hybridOptions);
                default:
                    throw new InvalidOperationException($"Unknown LLM provider: {LLM_PROVIDER}");
            }
        });

        // Add core services (order matters to avoid circular dependencies)
        services.AddSingleton<IGameStateRepository, GameStateRepository>();
        services.AddTransient<IVectorStoreService, QdrantVectorStoreService>();

        // Register Game Logic Services
        services.AddTransient<IGameLogicService, GameLogicService>();
        services.AddTransient<ICharacterManagementService, CharacterManagementService>();
        services.AddTransient<IInformationManagementService, InformationManagementService>();
        services.AddTransient<INpcManagementService, NpcManagementService>();
        services.AddTransient<IPokemonManagementService, PokemonManagementService>();
        services.AddTransient<IPlayerPokemonManagementService, PlayerPokemonManagementService>();
        services.AddTransient<IWorldManagementService, WorldManagementService>();
        
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
        
        // Register LLM providers
        services.AddTransient<OpenAiLLMProvider>();
        services.AddTransient<OllamaLLMProvider>();
        services.AddTransient<HybridLLMProvider>();
        
        // Register ILLMProvider based on configuration
        switch (LLM_PROVIDER)
        {
            case "OpenAI":
                services.AddTransient<ILLMProvider, OpenAiLLMProvider>();
                break;
            case "Ollama":
                services.AddTransient<ILLMProvider, OllamaLLMProvider>();
                break;
            case "Hybrid":
                services.AddTransient<ILLMProvider, HybridLLMProvider>();
                break;
            default:
                throw new InvalidOperationException($"Unknown LLM provider: {LLM_PROVIDER}");
        }
        
        // Register the main orchestration service
        services.AddTransient<IOrchestrationService, OrchestrationService>();

        return services;
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIEmbeddingGenerator(IOptions<ModelConfig> options)
    {
        // Ensure we have valid configuration values or use defaults for OpenAI
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
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOllamaEmbeddingGenerator(IOptions<ModelConfig> options)
    {
        // Ensure we have valid configuration values or use defaults for Ollama
        var endpoint = !string.IsNullOrWhiteSpace(options.Value.ApiKey) ? new Uri(options.Value.ApiKey) : new Uri("http://localhost:11434");
        var embeddingModelId = !string.IsNullOrWhiteSpace(options.Value.EmbeddingModelId) ? options.Value.EmbeddingModelId : "nomic-embed-text";
        
        // Create a minimal kernel just for the embedding generator
        var kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOllamaEmbeddingGenerator(
            modelId: embeddingModelId,
            endpoint: endpoint
        );
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        var kernel = kernelBuilder.Build();
        return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateHybridEmbeddingGenerator(IOptions<HybridConfig> options)
    {
        var config = options.Value;
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Create embedding generator based on embedding provider configuration
        switch (config.Embedding.Provider.ToLower())
        {
            case "openai":
                var apiKey = !string.IsNullOrWhiteSpace(config.Embedding.ApiKey) ? config.Embedding.ApiKey : "test-api-key";
                var embeddingModelId = !string.IsNullOrWhiteSpace(config.Embedding.ModelId) ? config.Embedding.ModelId : "text-embedding-3-small";
                
#pragma warning disable SKEXP0010
                kernelBuilder.AddOpenAIEmbeddingGenerator(
                    modelId: embeddingModelId,
                    apiKey: apiKey
                );
#pragma warning restore SKEXP0010
                break;
                
            case "ollama":
                var endpoint = !string.IsNullOrEmpty(config.Embedding.Endpoint) ? new Uri(config.Embedding.Endpoint) : new Uri("http://localhost:11434");
                var ollamaEmbeddingModelId = !string.IsNullOrWhiteSpace(config.Embedding.ModelId) ? config.Embedding.ModelId : "nomic-embed-text";
                
#pragma warning disable SKEXP0070
                kernelBuilder.AddOllamaEmbeddingGenerator(
                    modelId: ollamaEmbeddingModelId,
                    endpoint: endpoint
                );
#pragma warning restore SKEXP0070
                break;
                
            default:
                throw new InvalidOperationException($"Unknown embedding provider: {config.Embedding.Provider}");
        }
        
        var kernel = kernelBuilder.Build();
        return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
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