using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Orchestration.MultiAgent;
using PokeLLM.GameState;

namespace PokeLLM.Tests;

public class DependencyResolutionTests
{
    [Fact]
    public void ServiceDependenciesResolve()
    {
        var provider = Program.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ITurnOrchestrator>();
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
