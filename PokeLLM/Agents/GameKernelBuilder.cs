using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Game.Configuration;
using PokeLLM.State;

namespace PokeLLM.Agents;

public class GameKernelBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly List<Type> _plugins = new();

    public GameKernelBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public GameKernelBuilder AddPlugin<T>() where T : class
    {
        _plugins.Add(typeof(T));
        return this;
    }

    public GameKernelBuilder AddMemoryServices()
    {
        // Memory services will be configured here
        return this;
    }

    public Kernel Build()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Configure LLM provider based on configuration
        var llmConfig = _configuration.GetSection("LLM").Get<LLMConfig>();
        if (llmConfig?.Provider == "OpenAI")
        {
            var apiKey = _configuration["LLM:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: llmConfig.ModelId ?? "gpt-4",
                apiKey: apiKey);
        }
        else if (llmConfig?.Provider == "Ollama")
        {
            var endpoint = _configuration["LLM:Endpoint"] ?? "http://localhost:11434";
            kernelBuilder.AddOllamaChatCompletion(
                modelId: llmConfig.ModelId ?? "llama2",
                endpoint: new Uri(endpoint));
        }
        else if (llmConfig?.Provider == "Gemini")
        {
            var apiKey = _configuration["LLM:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
            kernelBuilder.AddGoogleAIGeminiChatCompletion(
                modelId: llmConfig.ModelId ?? "gemini-pro",
                apiKey: apiKey);
        }
        
        // Logging will be configured externally

        // Register state services
        kernelBuilder.Services.AddSingleton<IEventLog, InMemoryEventLog>();
        kernelBuilder.Services.AddSingleton<RandomNumberService>();
        
        // Add plugins
        var kernel = kernelBuilder.Build();
        foreach (var pluginType in _plugins)
        {
            var plugin = Activator.CreateInstance(pluginType);
            if (plugin != null)
            {
                kernel.Plugins.AddFromObject(plugin);
            }
        }

        return kernel;
    }
}

public class RandomNumberService
{
    private Random _random;
    
    public RandomNumberService()
    {
        _random = new Random();
    }
    
    public void SetSeed(int seed)
    {
        _random = new Random(seed);
    }
    
    public int Next(int min, int max) => _random.Next(min, max);
    public int Next(int max) => _random.Next(max);
    public double NextDouble() => _random.NextDouble();
}