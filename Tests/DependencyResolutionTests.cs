using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game;
using PokeLLM.Controllers;
using PokeLLM.State;

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
    public void GameStateRepositoryResolves()
    {
        var provider = Program.BuildServiceProvider();

        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();
        
        Assert.NotNull(gameStateRepository);
    }
}