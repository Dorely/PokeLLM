using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState;
using PokeLLM.GameLogic.Services;

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
        var gameController = provider.GetRequiredService<IGameController>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();
        var rulesetManager = provider.GetRequiredService<PokeLLM.GameRules.Interfaces.IRulesetManager>();
        var rulesetSelectionService = provider.GetRequiredService<IRulesetSelectionService>();

        string selectedRulesetId;
        
        // Check if a game already exists
        if (await gameStateRepository.HasGameStateAsync())
        {
            // Load existing game and get its ruleset
            var existingGameState = await gameStateRepository.LoadLatestStateAsync();
            selectedRulesetId = existingGameState.ActiveRulesetId ?? "pokemon-adventure";
            
            Console.WriteLine($"Loading existing game with {selectedRulesetId} ruleset...");
            
            // Set the active ruleset to match the existing game
            try
            {
                await rulesetManager.SetActiveRulesetAsync(selectedRulesetId);
                var activeRuleset = rulesetManager.GetActiveRuleset();
                if (activeRuleset == null)
                {
                    Console.WriteLine($"Warning: Could not load {selectedRulesetId} ruleset. Game may not function correctly.");
                }
                else
                {
                    Console.WriteLine($"✓ {selectedRulesetId} ruleset loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error loading {selectedRulesetId} ruleset: {ex.Message}");
            }
        }
        else
        {
            // New game - let user select ruleset (with wizard option)
            selectedRulesetId = await rulesetSelectionService.SelectRulesetWithWizardAsync();
            
            // Set the active ruleset
            try
            {
                await rulesetManager.SetActiveRulesetAsync(selectedRulesetId);
                var activeRuleset = rulesetManager.GetActiveRuleset();
                if (activeRuleset == null)
                {
                    Console.WriteLine($"Warning: Selected {selectedRulesetId} ruleset could not be loaded. Game may not function correctly.");
                }
                else
                {
                    Console.WriteLine($"✓ {selectedRulesetId} ruleset loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error loading {selectedRulesetId} ruleset: {ex.Message}");
                // Fallback to default ruleset
                selectedRulesetId = "pokemon-adventure";
                await rulesetManager.SetActiveRulesetAsync(selectedRulesetId);
                Console.WriteLine("Falling back to pokemon-adventure ruleset.");
            }

            // Create new game state with selected ruleset
            await gameStateRepository.CreateNewGameStateAsync();
            Console.WriteLine($"New game created with {selectedRulesetId} ruleset.");
        }

        Console.WriteLine($"PokeLLM: ");
        await foreach (var chunk in gameController.ProcessInputAsync("Game is done loading. Introduce yourself to the player"))
        {
            Console.Write(chunk);
        }

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

            // Process player input through the new game controller
            Console.WriteLine($"PokeLLM: ");
            try
            {
                await foreach (var chunk in gameController.ProcessInputAsync(input))
                {
                    Console.Write(chunk);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}