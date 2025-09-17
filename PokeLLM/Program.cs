using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

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
        var moduleRepository = provider.GetRequiredService<IAdventureModuleRepository>();

        var sessionState = await InitializeSessionAsync(gameStateRepository, moduleRepository);
        Console.WriteLine();
        Console.WriteLine($"Loaded session '{sessionState.SessionName}' using module '{sessionState.Module.ModuleTitle}'.");
        Console.WriteLine();

        var initialPrompt = sessionState.CurrentPhase == GamePhase.GameSetup
            ? "The adventure session has been initialized. Greet the player and begin the setup process for configuring the module."
            : "Game is done loading. Introduce yourself to the player.";

        Console.WriteLine("PokeLLM: ");
        await foreach (var chunk in gameController.ProcessInputAsync(initialPrompt))
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
            if (string.IsNullOrWhiteSpace(input) || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            Console.WriteLine("PokeLLM: ");
            try
            {
                await foreach (var chunk in gameController.ProcessInputAsync(input))
                {
                    Console.Write(chunk);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    private static async Task<AdventureSessionState> InitializeSessionAsync(
        IGameStateRepository gameStateRepository,
        IAdventureModuleRepository moduleRepository)
    {
        var sessions = await gameStateRepository.ListSessionsAsync();
        AdventureSessionState sessionState;

        if (sessions.Count > 0)
        {
            var selectedSession = PromptForSessionSelection(sessions);
            if (selectedSession is not null)
            {
                gameStateRepository.SetActiveSession(selectedSession.FilePath);
                sessionState = await gameStateRepository.LoadLatestStateAsync();
                return sessionState;
            }
        }
        else
        {
            Console.WriteLine("No existing adventure sessions found.");
        }

        sessionState = await CreateNewSessionAsync(gameStateRepository, moduleRepository);
        return sessionState;
    }

    private static AdventureSessionSummary? PromptForSessionSelection(IReadOnlyList<AdventureSessionSummary> sessions)
    {
        while (true)
        {
            Console.WriteLine("Select an adventure session to load or create a new one:");
            for (var i = 0; i < sessions.Count; i++)
            {
                var summary = sessions[i];
                var setupStatus = summary.IsSetupComplete ? "Setup Complete" : "Setup In Progress";
                Console.WriteLine($"  {i + 1}. {summary.SessionName} | Module: {summary.ModuleTitle} | Phase: {summary.CurrentPhase} | {setupStatus} | Updated: {summary.LastUpdatedTime:u}");
            }
            Console.WriteLine("  N. Create a new session");
            Console.Write("> ");

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (string.Equals(input, "N", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (int.TryParse(input, out var index) && index >= 1 && index <= sessions.Count)
            {
                return sessions[index - 1];
            }

            Console.WriteLine("Invalid selection. Please try again.\n");
        }
    }

    private static async Task<AdventureSessionState> CreateNewSessionAsync(
        IGameStateRepository gameStateRepository,
        IAdventureModuleRepository moduleRepository)
    {
        Console.WriteLine();
        Console.WriteLine("Let's create a new adventure session.");

        var modules = await moduleRepository.ListModulesAsync();
        var selectedModule = await PromptForModuleSelectionAsync(modules, moduleRepository);
        var moduleFilePath = selectedModule.FilePath;
        var module = await moduleRepository.LoadAsync(moduleFilePath);

        AdventureSessionState session;
        if (module.Metadata.IsSetupComplete)
        {
            session = moduleRepository.CreateBaselineSession(module);
        }
        else
        {
            session = new AdventureSessionState
            {
                Metadata =
                {
                    CurrentPhase = GamePhase.GameSetup,
                    CurrentContext = module.World.StartingContext,
                    PhaseChangeSummary = string.Empty,
                    GameTurnNumber = 0
                }
            };

            session.Module.ModuleId = module.Metadata.ModuleId;
            session.Module.ModuleTitle = module.Metadata.Title;
            session.Module.ModuleVersion = module.Metadata.Version;
            session.Module.ModuleChecksum = module.Metadata.ModuleId;
        }

        session.Module.ModuleFileName = Path.GetFileName(moduleFilePath);
        session.Metadata.IsSetupComplete = module.Metadata.IsSetupComplete;

        var suggestedSessionName = string.IsNullOrWhiteSpace(session.SessionName)
            ? module.Metadata.Title
            : session.SessionName;
        var sessionName = PromptForText("Enter a name for this session (leave blank to use the suggested name):", suggestedSessionName);
        if (!string.IsNullOrWhiteSpace(sessionName))
        {
            session.SessionName = sessionName;
        }

        var savedSession = await gameStateRepository.CreateNewGameStateAsync(session);
        Console.WriteLine($"Created new session '{savedSession.SessionName}' linked to module '{module.Metadata.Title}'.");
        return savedSession;
    }

    private static async Task<AdventureModuleSummary> PromptForModuleSelectionAsync(
        IReadOnlyList<AdventureModuleSummary> modules,
        IAdventureModuleRepository moduleRepository)
    {
        if (modules.Count == 0)
        {
            Console.WriteLine("No adventure modules found. We will create a new one.");
            return await CreateNewModuleAsync(moduleRepository);
        }

        while (true)
        {
            Console.WriteLine("Select an adventure module or create a new one:");
            for (var i = 0; i < modules.Count; i++)
            {
                var summary = modules[i];
                var setupStatus = summary.IsSetupComplete ? "Ready" : "Needs Setup";
                Console.WriteLine($"  {i + 1}. {summary.Title} (Version {summary.Version}) | {setupStatus} | Updated: {summary.LastModifiedUtc:u}");
            }
            Console.WriteLine("  C. Create a new module");
            Console.Write("> ");

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (string.Equals(input, "C", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateNewModuleAsync(moduleRepository);
            }

            if (int.TryParse(input, out var index) && index >= 1 && index <= modules.Count)
            {
                return modules[index - 1];
            }

            Console.WriteLine("Invalid selection. Please try again.\n");
        }
    }

    private static async Task<AdventureModuleSummary> CreateNewModuleAsync(IAdventureModuleRepository moduleRepository)
    {
        Console.WriteLine();
        Console.WriteLine("Creating a new adventure module.");

        var title = PromptForText("Enter a module title:", defaultValue: "Untitled Adventure");
        var summary = PromptForText("Enter a brief description (optional):", allowEmpty: true);

        var module = moduleRepository.CreateNewModule(title, summary);
        await moduleRepository.SaveAsync(module);
        var fullPath = moduleRepository.GetModuleFilePath(module.Metadata.ModuleId);

        Console.WriteLine($"Created module '{module.Metadata.Title}' with id {module.Metadata.ModuleId}.");
        return new AdventureModuleSummary
        {
            ModuleId = module.Metadata.ModuleId,
            Title = module.Metadata.Title,
            Version = module.Metadata.Version,
            IsSetupComplete = module.Metadata.IsSetupComplete,
            LastModifiedUtc = File.GetLastWriteTimeUtc(fullPath),
            FilePath = fullPath
        };
    }

    private static string PromptForText(string message, string? defaultValue = null, bool allowEmpty = false)
    {
        while (true)
        {
            Console.Write(message);
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                Console.Write($" [{defaultValue}]");
            }
            Console.Write(" ");

            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
            {
                if (!allowEmpty && string.IsNullOrWhiteSpace(defaultValue))
                {
                    Console.WriteLine("Value cannot be empty. Please try again.");
                    continue;
                }

                return defaultValue ?? string.Empty;
            }

            return input.Trim();
        }
    }
}
