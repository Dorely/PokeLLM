using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.State;
using PokeLLM.Memory;
using System;

namespace PokeLLM.Agents;

public interface IAgentFactory
{
    Task<IGameAgentManager> CreateAgentManagerAsync();
}

public class AgentFactory : IAgentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AgentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<IGameAgentManager> CreateAgentManagerAsync()
    {
        var agentManager = new GameAgentManager(
            _serviceProvider.GetRequiredService<ILogger<GameAgentManager>>());

        // Get the kernel directly from DI instead of creating it through ILLMProvider
        var kernel = _serviceProvider.GetRequiredService<Kernel>();

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
        
        // Register intent classifier that depends on kernel
        services.AddSingleton<IIntentClassifier, LLMIntentClassifier>();
        
        // Register agent factory
        services.AddSingleton<IAgentFactory, AgentFactory>();
        
        // Register a factory method for creating the agent manager using the async-friendly pattern
        services.AddSingleton<IGameAgentManager>(provider =>
        {
            var factory = provider.GetRequiredService<IAgentFactory>();
            
            // Use async factory pattern recommended by Semantic Kernel documentation
            return factory.CreateAgentManagerAsync().GetAwaiter().GetResult();
        });

        // Register state management services
        services.AddSingleton<RandomNumberService>();
        
        // Note: GameOrchestrator and GameSession are now registered in AddGameOrchestration extension method

        return services;
    }
}