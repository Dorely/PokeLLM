using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState;
using PokeLLM.GameLogic.Services;
using PokeLLM.Configuration;
using PokeLLM.Logging;

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
        
        // Get debug logger early for startup logging
        var debugLogger = provider.GetRequiredService<IDebugLogger>();
        var debugConfig = provider.GetRequiredService<IDebugConfiguration>();
        
        debugLogger.LogDebug("PokeLLM application starting...");
        debugLogger.LogDebug($"Debug mode enabled: {debugConfig.IsDebugModeEnabled}");
        debugLogger.LogDebug($"Verbose logging enabled: {debugConfig.IsVerboseLoggingEnabled}");
        debugLogger.LogDebug($"Debug prompts enabled: {debugConfig.IsDebugPromptsEnabled}");
        debugLogger.LogDebug($"Log file path: {debugConfig.LogFilePath}");
        
        var gameController = provider.GetRequiredService<IGameController>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();
        var rulesetManager = provider.GetRequiredService<PokeLLM.GameRules.Interfaces.IRulesetManager>();
        var rulesetSelectionService = provider.GetRequiredService<IRulesetSelectionService>();

        debugLogger.LogDebug("Core services initialized successfully");

        string selectedRulesetId;
        
        // Check if a game already exists
        debugLogger.LogDebug("Checking for existing game state...");
        if (await gameStateRepository.HasGameStateAsync())
        {
            // Load existing game and get its ruleset
            var existingGameState = await gameStateRepository.LoadLatestStateAsync();
            selectedRulesetId = existingGameState.ActiveRulesetId ?? "default";
            
            debugLogger.LogDebug($"Found existing game with ruleset: {selectedRulesetId}");
            debugLogger.LogDebug($"Existing game details - Turn: {existingGameState.GameTurnNumber}, Phase: {existingGameState.CurrentPhase}, Player: {existingGameState.Player.Name}");
            
            Console.WriteLine($"Loading existing game with {selectedRulesetId} ruleset...");
            
            // Set the active ruleset to match the existing game
            try
            {
                debugLogger.LogDebug($"Setting active ruleset to: {selectedRulesetId}");
                await rulesetManager.SetActiveRulesetAsync(selectedRulesetId);
                var activeRuleset = rulesetManager.GetActiveRuleset();
                if (activeRuleset == null)
                {
                    var errorMsg = $"Could not load {selectedRulesetId} ruleset. Game may not function correctly.";
                    debugLogger.LogError(errorMsg);
                    Console.WriteLine($"Warning: {errorMsg}");
                }
                else
                {
                    debugLogger.LogDebug($"✓ {selectedRulesetId} ruleset loaded successfully");
                    Console.WriteLine($"✓ {selectedRulesetId} ruleset loaded successfully");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error loading {selectedRulesetId} ruleset: {ex.Message}";
                debugLogger.LogError(errorMsg, ex);
                Console.WriteLine($"Warning: {errorMsg}");
            }
        }
        else
        {
            // New game - let user select ruleset (with wizard option)
            debugLogger.LogDebug("No existing game found. Starting new game creation process...");
            selectedRulesetId = await rulesetSelectionService.SelectRulesetWithWizardAsync();
            
            debugLogger.LogDebug($"User selected ruleset: {selectedRulesetId}");
            
            // Set the active ruleset
            try
            {
                debugLogger.LogDebug($"Loading selected ruleset: {selectedRulesetId}");
                await rulesetManager.SetActiveRulesetAsync(selectedRulesetId);
                var activeRuleset = rulesetManager.GetActiveRuleset();
                if (activeRuleset == null)
                {
                    var errorMsg = $"Selected {selectedRulesetId} ruleset could not be loaded. Game may not function correctly.";
                    debugLogger.LogError(errorMsg);
                    Console.WriteLine($"Warning: {errorMsg}");
                }
                else
                {
                    debugLogger.LogDebug($"✓ {selectedRulesetId} ruleset loaded successfully");
                    Console.WriteLine($"✓ {selectedRulesetId} ruleset loaded successfully");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error loading {selectedRulesetId} ruleset: {ex.Message}";
                debugLogger.LogError(errorMsg, ex);
                Console.WriteLine($"Warning: {errorMsg}");
                
                // Fallback to first available ruleset or create a generic one
                debugLogger.LogDebug("Attempting fallback to first available ruleset...");
                var availableRulesets = await rulesetManager.GetAvailableRulesetsAsync();
                if (availableRulesets.Any())
                {
                    selectedRulesetId = availableRulesets.First().Id;
                    await rulesetManager.SetActiveRulesetAsync(selectedRulesetId);
                    debugLogger.LogDebug($"Fallback successful: Using {selectedRulesetId} ruleset");
                    Console.WriteLine($"Falling back to {selectedRulesetId} ruleset.");
                }
                else
                {
                    debugLogger.LogError("No rulesets available. Game may not function correctly.");
                    Console.WriteLine("No rulesets available. Game may not function correctly.");
                    selectedRulesetId = "default";
                }
            }

            // Create new game state with selected ruleset
            debugLogger.LogDebug($"Creating new game state with ruleset: {selectedRulesetId}");
            await gameStateRepository.CreateNewGameStateAsync(selectedRulesetId);
            debugLogger.LogDebug("New game state created successfully");
            Console.WriteLine($"New game created with {selectedRulesetId} ruleset.");
        }

        debugLogger.LogDebug("Starting initial game interaction...");
        Console.WriteLine($"Game Engine: ");
        
        var initialInput = "Game is done loading. Introduce yourself to the player";
        debugLogger.LogUserInput($"[SYSTEM_INIT] {initialInput}");
        
        await foreach (var chunk in gameController.ProcessInputAsync(initialInput))
        {
            Console.Write(chunk);
        }

        debugLogger.LogDebug("Starting main game loop...");
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
            {
                debugLogger.LogDebug("User requested exit. Shutting down...");
                break;
            }

            debugLogger.LogUserInput(input);
            debugLogger.LogDebug($"Processing user input (length: {input.Length} characters)");

            // Process player input through the new game controller
            Console.WriteLine($"Game Engine: ");
            try
            {
                await foreach (var chunk in gameController.ProcessInputAsync(input))
                {
                    Console.Write(chunk);
                }
                debugLogger.LogDebug("User input processed successfully");
            }
            catch (Exception ex)
            {
                debugLogger.LogError($"Error processing user input: {ex.Message}", ex);
                throw;
            }
        }
        
        debugLogger.LogDebug("Application shutting down");
        debugLogger.Dispose();
    }
}