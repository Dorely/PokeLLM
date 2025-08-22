using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.State;

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

        // Create and register all agents
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
            agentManager);
    }

    private NarratorAgent CreateNarratorAgent(Kernel kernel)
    {
        return new NarratorAgent(
            kernel,
            _serviceProvider.GetRequiredService<ILogger<NarratorAgent>>());
    }

    private MechanicsAgent CreateMechanicsAgent(Kernel kernel)
    {
        return new MechanicsAgent(
            kernel,
            _serviceProvider.GetRequiredService<ILogger<MechanicsAgent>>(),
            _serviceProvider.GetRequiredService<RandomNumberService>(),
            _serviceProvider.GetRequiredService<IEventLog>());
    }
}

public static class AgentServiceExtensions
{
    public static IServiceCollection AddGameAgents(this IServiceCollection services)
    {
        services.AddSingleton<IAgentFactory, AgentFactory>();
        services.AddSingleton<IIntentClassifier, LLMIntentClassifier>();
        
        // Register individual agent types for DI
        services.AddTransient<SetupAgent>();
        services.AddTransient<GMSupervisorAgent>();
        services.AddTransient<NarratorAgent>();
        services.AddTransient<MechanicsAgent>();

        return services;
    }
}