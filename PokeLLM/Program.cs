using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.Orchestration;
using PokeLLM.GameState;

namespace PokeLLM.Game;

public class Program
{
    public static ServiceProvider BuildServiceProvider()
    {

        // Create configuration and set up DI using ServiceConfiguration
        var config = ServiceConfiguration.CreateConfiguration();
        var services = new ServiceCollection();
        ServiceConfiguration.ConfigureServices(services, config);

        var provider = services.BuildServiceProvider();

        return provider;
    }

    private static async Task Main(string[] args)
    {
        var provider = BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrationService>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();

        // Ensure game state is initialized and set to GameCreation phase
        if (!await gameStateRepository.HasGameStateAsync())
        {
            // Create new game state if none exists (defaults to GameCreation phase)
            await gameStateRepository.CreateNewGameStateAsync();
            Console.WriteLine("New game state created. Starting in Game Creation phase.");
        }
        else
        {
            // Load existing state and ensure it's set to GameCreation phase
            var gameState = await gameStateRepository.LoadLatestStateAsync();
        }

        // Get initial welcome message through the new game loop
        Console.WriteLine($"LLM: ");
        await foreach (var chunk in orchestrator.OrchestrateAsync("Game is done loading. Introduce yourself to the player"))
        {
            Console.Write(chunk);
        }

        while (true)
        {
            Console.WriteLine("\nYou (multi-line, end with blank line):");
            var lines = new List<string>();
            while (true)
            {
                var line = Console.ReadLine();
                if (line == null || string.IsNullOrWhiteSpace(line))
                    break;
                lines.Add(line);
            }

            Console.WriteLine("\nSending...");
            var input = string.Join('\n', lines);
            if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "exit")
                break;

            // Process player input through the new game loop architecture
            Console.WriteLine($"LLM: ");
            await foreach (var chunk in orchestrator.OrchestrateAsync(input))
            {
                Console.Write(chunk);
            }
        }
    }
}