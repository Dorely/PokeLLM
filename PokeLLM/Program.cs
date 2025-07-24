using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.Configure<ModelConfig>(config.GetSection("OpenAi"));
services.Configure<QdrantConfig>(config.GetSection("Qdrant"));

services.AddTransient<ILLMProvider, OpenAiProvider>();
services.AddTransient<IVectorStoreService, VectorStoreService>();
services.AddSingleton<IGameStateRepository, GameStateRepository>();

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

Console.WriteLine("Welcome to PokeLLM! Type 'exit' to quit.");
Console.WriteLine("Enter your message. Finish with a blank line to send.\n");

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
    string fullResponse = "";
    await foreach (var chunk in response)
    {
        Console.Write(chunk);
        fullResponse += chunk;
    }
}