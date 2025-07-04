using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.LLM;
using PokeLLM.Game.LLM.Interfaces;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.Configure<ModelConfig>(config.GetSection("OpenAi"));

services.AddSingleton<ILLMProvider, OpenAiProvider>();
//services.AddSingleton<GameStateRepository>(_ => new GameStateRepository("game.db"));
//services.AddSingleton<IRetriever, Retriever>();
var provider = services.BuildServiceProvider();

var llm = provider.GetRequiredService<ILLMProvider>();

// Create chat history
var history = llm.CreateHistory();

Console.WriteLine("Welcome to PokeLLM! Type 'exit' to quit.");
Console.WriteLine("Enter your message. Finish with a blank line to send.\n");

while (true)
{
    Console.WriteLine("You (multi-line, end with blank line):");
    var lines = new List<string>();
    while (true)
    {
        var line = Console.ReadLine();
        if (line == null || string.IsNullOrWhiteSpace(line))
            break;
        lines.Add(line);
    }

    var input = string.Join('\n', lines);
    if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "exit")
        break;

    // Get LLM response
    var response = await llm.GetCompletionAsync(input, history);
    Console.WriteLine($"LLM: {response}\n");
    // Add LLM response to history
    history.AddAssistantMessage(response);
}