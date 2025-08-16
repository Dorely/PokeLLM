using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Orchestration;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.VectorStore;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameLogic;
using PokeLLM.GameLogic.Services;
using PokeLLM.Plugins;

namespace PokeLLM.Game.Configuration;

public static class ServiceConfiguration
{
    // Configuration: Change these to switch between providers
    //private const string MAIN_LLM_PROVIDER = "Gemini"; // "OpenAI", "Ollama", "Gemini"
    private const string MAIN_LLM_PROVIDER = "OpenAI"; // "OpenAI", "Ollama", "Gemini"
    private const string EMBEDDING_PROVIDER = "Ollama"; // "OpenAI", "Ollama" (default)

    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add configuration
        services.AddSingleton(configuration);
        
        // Configure flexible provider system
        services.Configure<FlexibleProviderConfig>(config =>
        {
            // Configure main LLM provider
            config.LLM.Provider = MAIN_LLM_PROVIDER;
            config.Embedding.Provider = EMBEDDING_PROVIDER;
            
            // Set default models and dimensions based on providers
            switch (MAIN_LLM_PROVIDER.ToLower())
            {
                case "openai":
                    configuration.GetSection("OpenAi").Bind(config.LLM);
                    config.LLM.ModelId = config.LLM.ModelId ?? "gpt-4o-mini";
                    break;
                case "ollama":
                    configuration.GetSection("Ollama").Bind(config.LLM);
                    config.LLM.ModelId = config.LLM.ModelId ?? "llama3.1";
                    config.LLM.Endpoint = config.LLM.Endpoint ?? "http://localhost:11434";
                    break;
                case "gemini":
                    configuration.GetSection("Gemini").Bind(config.LLM);
                    config.LLM.ModelId = config.LLM.ModelId ?? "gemini-1.5-flash";
                    break;
                default:
                    throw new InvalidOperationException($"Unknown main LLM provider: {MAIN_LLM_PROVIDER}");
            }
            
            // Configure embedding provider (always separate)
            switch (EMBEDDING_PROVIDER.ToLower())
            {
                case "openai":
                    var openAiSection = configuration.GetSection("OpenAi");
                    config.Embedding.ApiKey = openAiSection["ApiKey"];
                    config.Embedding.ModelId = config.Embedding.ModelId ?? "text-embedding-3-small";
                    config.Embedding.Dimensions = config.Embedding.Dimensions > 0 ? config.Embedding.Dimensions : 1536;
                    break;
                case "ollama":
                    var ollamaSection = configuration.GetSection("Ollama");
                    config.Embedding.Endpoint = ollamaSection["Endpoint"] ?? "http://localhost:11434";
                    config.Embedding.ModelId = config.Embedding.ModelId ?? "nomic-embed-text";
                    config.Embedding.Dimensions = config.Embedding.Dimensions > 0 ? config.Embedding.Dimensions : 768;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown embedding provider: {EMBEDDING_PROVIDER}");
            }
        });
        
        // Also configure individual ModelConfig for backward compatibility
        services.Configure<ModelConfig>(config =>
        {
            switch (MAIN_LLM_PROVIDER.ToLower())
            {
                case "openai":
                    configuration.GetSection("OpenAi").Bind(config);
                    config.EmbeddingDimensions = EMBEDDING_PROVIDER.ToLower() == "openai" ? 1536 : 768;
                    break;
                case "ollama":
                    configuration.GetSection("Ollama").Bind(config);
                    config.EmbeddingDimensions = EMBEDDING_PROVIDER.ToLower() == "openai" ? 1536 : 768;
                    break;
                case "gemini":
                    configuration.GetSection("Gemini").Bind(config);
                    config.EmbeddingDimensions = EMBEDDING_PROVIDER.ToLower() == "openai" ? 1536 : 768;
                    break;
            }
        });
        
        services.Configure<QdrantConfig>(configuration.GetSection("Qdrant"));

        // Register IEmbeddingGenerator based on embedding provider
        services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider => 
        {
            var flexConfig = serviceProvider.GetRequiredService<IOptions<FlexibleProviderConfig>>();
            return CreateEmbeddingGenerator(flexConfig);
        });

        // Add core services (order matters to avoid circular dependencies)
        services.AddSingleton<IGameStateRepository, GameStateRepository>();
        services.AddTransient<IVectorStoreService, QdrantVectorStoreService>();

        // Register unified entity service for dynamic ruleset management
        services.AddTransient<IEntityService, BasicEntityService>();

        // Register Game Logic Services
        services.AddTransient<IGameLogicService, GameLogicService>();
        services.AddTransient<ICharacterManagementService, CharacterManagementService>();
        services.AddTransient<IInformationManagementService, InformationManagementService>();
        services.AddTransient<INpcManagementService, NpcManagementService>();
        // TEMP: Disabled Pokemon-specific services that need refactoring
        // services.AddTransient<IPokemonManagementService, PokemonManagementService>();
        // services.AddTransient<IPlayerPokemonManagementService, PlayerPokemonManagementService>();
        services.AddTransient<IWorldManagementService, WorldManagementService>();
        services.AddTransient<IRulesetSelectionService, RulesetSelectionService>();

        // Register plugins (updated for new architecture)
        services.AddTransient<ExplorationPhasePlugin>();
        services.AddTransient<CombatPhasePlugin>();
        services.AddTransient<LevelUpPhasePlugin>();
        services.AddTransient<UnifiedContextPlugin>();
        services.AddTransient<GameSetupPhasePlugin>();
        services.AddTransient<WorldGenerationPhasePlugin>();
        services.AddTransient<RulesetManagementPlugin>();
        services.AddTransient<RulesetWizardPlugin>();

        // Register generic rule system services
        services.AddTransient<IJavaScriptRuleEngine, JavaScriptRuleEngine>();
        services.AddTransient<IDynamicFunctionFactory, DynamicFunctionFactory>();
        services.AddTransient<IRulesetService, RulesetService>();
        services.AddSingleton<IRulesetManager, RulesetManager>();
        
        // Register ruleset wizard services
        services.AddTransient<IRulesetWizardService, RulesetWizardService>();
        services.AddTransient<IRulesetBuilderService, RulesetBuilderService>();
        services.AddTransient<IRulesetSchemaValidator, RulesetSchemaValidator>();

        // Register all LLM providers
        services.AddTransient<OpenAiLLMProvider>();
        services.AddTransient<OllamaLLMProvider>();
        services.AddTransient<GeminiLLMProvider>();
        
        // Register ILLMProvider based on main LLM provider configuration
        switch (MAIN_LLM_PROVIDER.ToLower())
        {
            case "openai":
                services.AddTransient<ILLMProvider, OpenAiLLMProvider>();
                break;
            case "ollama":
                services.AddTransient<ILLMProvider, OllamaLLMProvider>();
                break;
            case "gemini":
                services.AddTransient<ILLMProvider, GeminiLLMProvider>();
                break;
            default:
                throw new InvalidOperationException($"Unknown main LLM provider: {MAIN_LLM_PROVIDER}");
        }

        // Register new service architecture
        services.AddScoped<IUnifiedContextService, UnifiedContextService>();
        
        // Register phase service provider - create a minimal implementation that works with dynamic rulesets
        services.AddScoped<IPhaseServiceProvider, PhaseServiceProvider>();
        
        // Register IPhaseService factory to provide a default phase service for services that need it
        services.AddTransient<IPhaseService>(serviceProvider =>
        {
            var phaseServiceProvider = serviceProvider.GetRequiredService<IPhaseServiceProvider>();
            // Use Exploration phase as default for general LLM interactions
            return phaseServiceProvider.GetPhaseService(GameState.Models.GamePhase.Exploration);
        });
        
        services.AddScoped<IGameController, GameController>();

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

    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(IOptions<FlexibleProviderConfig> options)
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