using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Orchestration;
using PokeLLM.GameState;

namespace Tests;

public class ProgramIntegrationTests
{
    [Fact]
    public void ServiceDependenciesResolve()
    {
        var provider = Program.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<IOrchestrationService>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();

        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void GameLogicServiceResolves()
    {
        var provider = Program.BuildServiceProvider();

        var gameLogicService = provider.GetRequiredService<IGameLogicService>();
        
        Assert.NotNull(gameLogicService);
    }
}