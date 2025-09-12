using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.Orchestration.MultiAgent;
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
        var orchestrator = provider.GetRequiredService<ITurnOrchestrator>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();

        if (!await gameStateRepository.HasGameStateAsync())
        {
            await gameStateRepository.CreateNewGameStateAsync();
            Console.WriteLine("New game created.");
        }

        Console.WriteLine($"PokeLLM: ");
        var intro = await orchestrator.ProcessTurnAsync("Game is done loading. Introduce yourself to the player");
        Console.Write(intro);

        while (true)
        {
            Console.WriteLine("\nYou (multi-line, end with 'blank line'):");
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

            // Process player input through the orchestrator
            Console.WriteLine($"PokeLLM: ");
            var output = await orchestrator.ProcessTurnAsync(input);
            Console.Write(output);
        }
    }
}
