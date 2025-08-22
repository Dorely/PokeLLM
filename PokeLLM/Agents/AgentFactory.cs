using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.State;
using PokeLLM.Memory;
using System;

namespace PokeLLM.Agents;

public interface IAgentFactory
{
    Task<IGameAgentManager> CreateAgentManagerAsync(Kernel kernel);
}

public class AgentFactory : IAgentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AgentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<IGameAgentManager> CreateAgentManagerAsync(Kernel kernel)
    {
        var agentManager = new GameAgentManager(
            _serviceProvider.GetRequiredService<ILogger<GameAgentManager>>());

        // Create and register all agents with memory support
        var agents = new IGameAgent[]
        {
            CreateSetupAgent(kernel),
            CreateGMSupervisorAgent(kernel, agentManager),
            CreateNarratorAgent(kernel),
            CreateMechanicsAgent(kernel)
        };

        foreach (var agent in agents)
        {
            agentManager.RegisterAgent(agent);
        }

        return agentManager;
    }

    private SetupAgent CreateSetupAgent(Kernel kernel)
    {
        return new SetupAgent(
            kernel,
            _serviceProvider.GetRequiredService<ILogger<SetupAgent>>(),
            _serviceProvider.GetRequiredService<IEventLog>());
    }

    private GMSupervisorAgent CreateGMSupervisorAgent(Kernel kernel, IGameAgentManager agentManager)
    {
        return new GMSupervisorAgent(
            kernel,
            _serviceProvider.GetRequiredService<ILogger<GMSupervisorAgent>>(),
            _serviceProvider.GetRequiredService<IIntentClassifier>(),
            agentManager,
            _serviceProvider.GetRequiredService<MemoryEnabledAgentThreadFactory>());
    }

    private NarratorAgent CreateNarratorAgent(Kernel kernel)
    {
        return new NarratorAgent(
            kernel,
            _serviceProvider.GetRequiredService<ILogger<NarratorAgent>>(),
            _serviceProvider.GetRequiredService<MemoryEnabledAgentThreadFactory>());
    }

    private MechanicsAgent CreateMechanicsAgent(Kernel kernel)
    {
        return new MechanicsAgent(
            kernel,
            _serviceProvider.GetRequiredService<ILogger<MechanicsAgent>>(),
            _serviceProvider.GetRequiredService<RandomNumberService>(),
            _serviceProvider.GetRequiredService<IEventLog>(),
            _serviceProvider.GetRequiredService<MemoryEnabledAgentThreadFactory>());
    }
}

public static class AgentServiceExtensions
{
    public static IServiceCollection AddGameAgents(this IServiceCollection services)
    {
        // Register memory components first
        services.AddMemoryComponents();
        
        // Register memory-enabled agent thread factory
        services.AddSingleton<MemoryEnabledAgentThreadFactory>();
        
        // Register the kernel directly without GameKernelBuilder for now
        services.AddSingleton<Kernel>(provider =>
        {
            var kernelBuilder = Kernel.CreateBuilder();
            
            // Configure AI services (simplified version)
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "test-key";
            
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: "gpt-4o-mini",
                apiKey: apiKey);

            kernelBuilder.AddOpenAITextEmbeddingGeneration(
                modelId: "text-embedding-ada-002",
                apiKey: apiKey);
                
            // Configure logging
            kernelBuilder.Services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            
            return kernelBuilder.Build();
        });
        
        // Register intent classifier that depends on kernel
        services.AddSingleton<IIntentClassifier, LLMIntentClassifier>();
        
        // Register agent factory
        services.AddSingleton<IAgentFactory, AgentFactory>();
        
        // Register a factory method for creating the agent manager
        services.AddSingleton<IGameAgentManager>(provider =>
        {
            var kernel = provider.GetRequiredService<Kernel>();
            var factory = provider.GetRequiredService<IAgentFactory>();
            
            // This is a bit of a hack since we can't await in a sync factory method
            // In a real implementation, this would be restructured
            return factory.CreateAgentManagerAsync(kernel).GetAwaiter().GetResult();
        });

        // Register state management services
        services.AddSingleton<RandomNumberService>();
        
        // Register game context and orchestration services
        services.AddScoped<GameSession>();
        services.AddScoped<GameOrchestrator>();

        return services;
    }
}