using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeLLM.Controllers;
using System.Text;

namespace PokeLLM.UI;

public interface IGameUI
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public class ConsoleGameUI : IGameUI
{
    private readonly IGameController _gameController;
    private readonly ILogger<ConsoleGameUI> _logger;
    private string? _currentSessionId;

    public ConsoleGameUI(IGameController gameController, ILogger<ConsoleGameUI> logger)
    {
        _gameController = gameController;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("=== PokeLLM Agent-Based RPG ===");
        Console.WriteLine();

        try
        {
            await ShowMainMenuAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in console UI");
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private async Task ShowMainMenuAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("\n=== Main Menu ===");
            Console.WriteLine("1. Start New Game");
            Console.WriteLine("2. List Active Games");
            Console.WriteLine("3. Resume Game");
            Console.WriteLine("4. Exit");
            Console.Write("\nChoose an option: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await StartNewGameAsync(cancellationToken);
                    break;
                case "2":
                    await ListActiveGamesAsync();
                    break;
                case "3":
                    await ResumeGameAsync(cancellationToken);
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    private async Task StartNewGameAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("\n=== Start New Game ===");
        
        Console.Write("Enter your name: ");
        var playerName = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            Console.WriteLine("Invalid name. Returning to main menu.");
            return;
        }

        Console.Write("Enter your character's backstory: ");
        var backstory = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(backstory))
        {
            backstory = "A young Pokemon trainer starting their journey.";
        }

        Console.Write("Enter preferred setting (leave blank for default): ");
        var setting = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(setting))
        {
            setting = "Kanto Region";
        }

        Console.WriteLine("\nStarting your Pokemon adventure...");

        try
        {
            var gameInfo = await _gameController.StartNewGameAsync(
                new StartGameRequest(playerName, backstory, setting),
                cancellationToken);

            _currentSessionId = gameInfo.SessionId;
            Console.WriteLine($"Game started! Session ID: {gameInfo.SessionId}");

            await PlayGameAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting new game");
            Console.WriteLine($"Failed to start game: {ex.Message}");
        }
    }

    private async Task PlayGameAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentSessionId))
        {
            Console.WriteLine("No active game session.");
            return;
        }

        Console.WriteLine("\n=== Game Started ===");
        Console.WriteLine("Type 'quit' to return to main menu, 'save' to save your game.");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (input.Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                await SaveGameAsync();
                continue;
            }

            try
            {
                var response = new StringBuilder();
                await foreach (var chunk in _gameController.PlayTurnAsync(
                    new PlayTurnRequest(_currentSessionId, input),
                    cancellationToken))
                {
                    response.Append(chunk);
                }

                Console.WriteLine();
                Console.WriteLine(response.ToString());
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing player input");
                Console.WriteLine($"Error processing your action: {ex.Message}");
            }
        }
    }

    private async Task ListActiveGamesAsync()
    {
        Console.WriteLine("\n=== Active Games ===");

        try
        {
            var games = await _gameController.GetActiveGamesAsync();
            
            if (!games.Any())
            {
                Console.WriteLine("No active games.");
                return;
            }

            foreach (var game in games)
            {
                Console.WriteLine($"Session ID: {game.SessionId}");
                Console.WriteLine($"Player: {game.PlayerName}");
                Console.WriteLine($"Status: {(game.IsActive ? "Active" : "Inactive")}");
                Console.WriteLine($"Created: {game.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active games");
            Console.WriteLine($"Error listing games: {ex.Message}");
        }
    }

    private async Task ResumeGameAsync(CancellationToken cancellationToken)
    {
        Console.Write("Enter session ID to resume: ");
        var sessionId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(sessionId))
        {
            Console.WriteLine("Invalid session ID.");
            return;
        }

        _currentSessionId = sessionId;
        Console.WriteLine($"Resuming game session {sessionId}...");

        await PlayGameAsync(cancellationToken);
    }

    private async Task SaveGameAsync()
    {
        if (string.IsNullOrEmpty(_currentSessionId))
        {
            Console.WriteLine("No active game to save.");
            return;
        }

        try
        {
            await _gameController.SaveGameAsync(_currentSessionId);
            Console.WriteLine("Game saved successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving game");
            Console.WriteLine($"Error saving game: {ex.Message}");
        }
    }
}

public static class ConsoleGameUIExtensions
{
    public static IServiceCollection AddConsoleGameUI(this IServiceCollection services)
    {
        services.AddTransient<IGameUI, ConsoleGameUI>();
        return services;
    }
}