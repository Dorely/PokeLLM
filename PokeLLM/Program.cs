using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Create configuration and set up DI using ServiceConfiguration
var config = ServiceConfiguration.CreateConfiguration();
var services = new ServiceCollection();
ServiceConfiguration.ConfigureServices(services, config);

var provider = services.BuildServiceProvider();

var llm = provider.GetRequiredService<ILLMProvider>();
var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();

// Ensure game state is initialized and set to GameCreation phase
if (!await gameStateRepository.HasGameStateAsync())
{
    // Create new game state if none exists (defaults to GameCreation phase)
    await gameStateRepository.CreateNewGameStateAsync("New Player");
    Console.WriteLine("New game state created. Starting in Game Creation phase.");
}
else
{
    // Load existing state and ensure it's set to GameCreation phase
    var gameState = await gameStateRepository.LoadLatestStateAsync();
}

//// Get LLM response
var firstResponse = llm.GetCompletionStreamingAsync("The program has finished loading. Introduce yourself to the player.");

Console.WriteLine($"LLM: ");
await foreach (var chunk in firstResponse)
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

    //// Get LLM response
    var response = llm.GetCompletionStreamingAsync(input);

    Console.WriteLine($"LLM: ");
    await foreach (var chunk in response)
    {
        Console.Write(chunk);
    }
}