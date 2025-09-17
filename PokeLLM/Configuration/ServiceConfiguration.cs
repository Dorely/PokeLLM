using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.LLM;
using PokeLLM.Game.Orchestration;
using PokeLLM.Game.Plugins;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.VectorStore;

namespace PokeLLM.Game.Configuration;

public static class ServiceConfiguration
{
    private const string DefaultProvider = "OpenAI";

    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);

        var providerSelection = configuration.GetSection("Provider");
        var selectedLlmProvider = providerSelection["LLM"] ?? DefaultProvider;
        var selectedEmbeddingProvider = providerSelection["Embedding"];
        if (string.IsNullOrWhiteSpace(selectedEmbeddingProvider))
        {
            selectedEmbeddingProvider = selectedLlmProvider;
        }

        services.Configure<FlexibleProviderConfig>(config =>
        {
            ApplyLlmConfig(config.LLM, configuration, selectedLlmProvider);
            ApplyEmbeddingConfig(config.Embedding, configuration, selectedEmbeddingProvider);
        });

        services.Configure<ModelConfig>(config =>
        {
            var llm = new LLMConfig();
            ApplyLlmConfig(llm, configuration, selectedLlmProvider);
            config.ApiKey = llm.ApiKey;
            config.ModelId = llm.ModelId;

            var embedding = new EmbeddingConfig();
            ApplyEmbeddingConfig(embedding, configuration, selectedEmbeddingProvider);
            config.EmbeddingModelId = embedding.ModelId;
            config.EmbeddingDimensions = embedding.Dimensions;
        });

        services.Configure<QdrantConfig>(configuration.GetSection("Qdrant"));

        services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
        {
            var flexConfig = serviceProvider.GetRequiredService<IOptions<FlexibleProviderConfig>>();
            return CreateEmbeddingGenerator(flexConfig);
        });

        services.Configure<AdventureSessionRepositoryOptions>(configuration.GetSection("GameState"));
        services.AddSingleton<IGameStateRepository, GameStateRepository>();
        services.AddSingleton<IAdventureModuleRepository, AdventureModuleRepository>();
        services.AddTransient<IVectorStoreService, QdrantVectorStoreService>();

        services.AddTransient<IGameLogicService, GameLogicService>();
        services.AddTransient<ICharacterManagementService, CharacterManagementService>();
        services.AddTransient<IInformationManagementService, InformationManagementService>();
        services.AddTransient<INpcManagementService, NpcManagementService>();
        services.AddTransient<IPokemonManagementService, PokemonManagementService>();
        services.AddTransient<IPlayerPokemonManagementService, PlayerPokemonManagementService>();
        services.AddTransient<IWorldManagementService, WorldManagementService>();

        services.AddTransient<ExplorationPhasePlugin>();
        services.AddTransient<CombatPhasePlugin>();
        services.AddTransient<LevelUpPhasePlugin>();
        services.AddTransient<UnifiedContextPlugin>();
        services.AddTransient<GameSetupPhasePlugin>();
        services.AddTransient<WorldGenerationPhasePlugin>();

        services.AddTransient<OpenAiLLMProvider>();
        services.AddTransient<OllamaLLMProvider>();
        services.AddTransient<GeminiLLMProvider>();

        services.AddTransient<ILLMProvider>(serviceProvider =>
        {
            var flexConfig = serviceProvider.GetRequiredService<IOptions<FlexibleProviderConfig>>();
            return flexConfig.Value.LLM.Provider.ToLower() switch
            {
                "openai" => serviceProvider.GetRequiredService<OpenAiLLMProvider>(),
                "ollama" => serviceProvider.GetRequiredService<OllamaLLMProvider>(),
                "gemini" => serviceProvider.GetRequiredService<GeminiLLMProvider>(),
                _ => throw new InvalidOperationException($"Unknown LLM provider: {flexConfig.Value.LLM.Provider}")
            };
        });

        services.AddScoped<IUnifiedContextService, UnifiedContextService>();
        services.AddScoped<IPhaseServiceProvider, PhaseServiceProvider>();
        services.AddScoped<IGameController, GameController>();

        return services;
    }

    private static void ApplyLlmConfig(LLMConfig target, IConfiguration configuration, string provider)
    {
        switch (provider.ToLower())
        {
            case "openai":
                var openAi = configuration.GetSection("OpenAi");
                target.Provider = "OpenAI";
                target.ApiKey = openAi["ApiKey"];
                if (string.IsNullOrWhiteSpace(target.ApiKey))
                {
                    target.ApiKey = "test-api-key";
                }
                target.ModelId = openAi["ModelId"] ?? "gpt-4.1-mini";
                target.Endpoint = openAi["Endpoint"];
                break;
            case "ollama":
                var ollama = configuration.GetSection("Ollama");
                target.Provider = "Ollama";
                target.Endpoint = ollama["Endpoint"] ?? "http://localhost:11434";
                target.ModelId = ollama["ModelId"] ?? "llama3.1";
                break;
            case "gemini":
                var gemini = configuration.GetSection("Gemini");
                target.Provider = "Gemini";
                target.ApiKey = gemini["ApiKey"];
                if (string.IsNullOrWhiteSpace(target.ApiKey))
                {
                    target.ApiKey = "test-api-key";
                }
                target.ModelId = gemini["ModelId"] ?? "gemini-2.5-flash";
                break;
            default:
                throw new InvalidOperationException($"Unknown LLM provider: {provider}");
        }
    }

    private static void ApplyEmbeddingConfig(EmbeddingConfig target, IConfiguration configuration, string provider)
    {
        switch (provider.ToLower())
        {
            case "openai":
                var openAi = configuration.GetSection("OpenAi");
                target.Provider = "OpenAI";
                target.ApiKey = openAi["ApiKey"];
                target.ModelId = openAi["EmbeddingModelId"] ?? "text-embedding-3-small";
                target.Dimensions = int.TryParse(openAi["EmbeddingDimensions"], out var openAiDims) ? openAiDims : 1536;
                break;
            case "ollama":
                var ollama = configuration.GetSection("Ollama");
                target.Provider = "Ollama";
                target.Endpoint = ollama["Endpoint"] ?? "http://localhost:11434";
                target.ModelId = ollama["EmbeddingModelId"] ?? "nomic-embed-text";
                target.Dimensions = int.TryParse(ollama["EmbeddingDimensions"], out var ollamaDims) ? ollamaDims : 768;
                break;
            default:
                throw new InvalidOperationException($"Unknown embedding provider: {provider}");
        }
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(IOptions<FlexibleProviderConfig> options)
    {
        var config = options.Value;
        var kernelBuilder = Kernel.CreateBuilder();

        switch (config.Embedding.Provider.ToLower())
        {
            case "openai":
                var apiKey = !string.IsNullOrWhiteSpace(config.Embedding.ApiKey) ? config.Embedding.ApiKey : "test-api-key";
                var embeddingModelId = !string.IsNullOrWhiteSpace(config.Embedding.ModelId) ? config.Embedding.ModelId : "text-embedding-3-small";
#pragma warning disable SKEXP0010
                kernelBuilder.AddOpenAIEmbeddingGenerator(
                    modelId: embeddingModelId,
                    apiKey: apiKey);
#pragma warning restore SKEXP0010
                break;
            case "ollama":
                var endpoint = !string.IsNullOrEmpty(config.Embedding.Endpoint) ? new Uri(config.Embedding.Endpoint) : new Uri("http://localhost:11434");
                var ollamaEmbeddingModelId = !string.IsNullOrWhiteSpace(config.Embedding.ModelId) ? config.Embedding.ModelId : "nomic-embed-text";
#pragma warning disable SKEXP0070
                kernelBuilder.AddOllamaEmbeddingGenerator(
                    modelId: ollamaEmbeddingModelId,
                    endpoint: endpoint);
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
