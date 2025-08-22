using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeLLM.Agents;
using PokeLLM.Controllers;
using PokeLLM.Game.Configuration;
using PokeLLM.State;
using PokeLLM.UI;

namespace PokeLLM.Game;

public class Program
{
    public static ServiceProvider BuildServiceProvider()
    {
        // Create configuration and set up DI using ServiceConfiguration
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add core configuration and services
        ServiceConfiguration.ConfigureServices(services, config);
        
        // Add new agent-based architecture services
        services.AddGameAgents();
        services.AddGameOrchestration();
        services.AddGameController();
        services.AddConsoleGameUI();
        
        var provider = services.BuildServiceProvider();
        return provider;
    }

    private static async Task Main(string[] args)
    {
        var provider = BuildServiceProvider();
        
        try
        {
            var gameUI = provider.GetRequiredService<IGameUI>();
            await gameUI.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Application failed to start");
            Console.WriteLine($"Application failed: {ex.Message}");
        }
    }
}