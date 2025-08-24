using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.GameState;

namespace PokeLLM.Tests;

public class DependencyResolutionTests
{
    [Fact]
    public void ServiceDependenciesResolve()
    {
        var provider = Program.BuildServiceProvider();

        var gameController = provider.GetRequiredService<IGameController>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();

        Assert.NotNull(gameController);
    }

    [Fact]
    public void GameLogicServiceResolves()
    {
        var provider = Program.BuildServiceProvider();

        var gameLogicService = provider.GetRequiredService<IGameLogicService>();
        
        Assert.NotNull(gameLogicService);
    }
}