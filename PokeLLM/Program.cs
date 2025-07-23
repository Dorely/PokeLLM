using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

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

services.AddTransient<IPhaseManager, PhaseManager>();
services.AddTransient<ILLMProvider, OpenAiProvider>();
services.AddTransient<IVectorStoreService, VectorStoreService>();
services.AddSingleton<IGameStateRepository, GameStateRepository>();

var provider = services.BuildServiceProvider();

var llm = provider.GetRequiredService<ILLMProvider>();
llm.RegisterPlugins(provider.GetRequiredService<IVectorStoreService>(), provider.GetRequiredService<IGameStateRepository>());

// Create chat history
var history = llm.CreateHistory();

Console.WriteLine("Welcome to PokeLLM! Type 'exit' to quit.");
Console.WriteLine("Enter your message. Finish with a blank line to send.\n");
Console.WriteLine("Bot is trained on data up to October 2023.\n");

//start the prompt and get the game flowing before initiating player input
var firstResponse = llm.GetCompletionStreamingAsync("Session Start - Begin character creation", history);
Console.WriteLine($"LLM: ");
string fullFirstResponse = "";
await foreach (var chunk in firstResponse)
{
    Console.Write(chunk);
    fullFirstResponse += chunk;
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
    var response = llm.GetCompletionStreamingAsync(input, history);

    Console.WriteLine($"LLM: ");
    string fullResponse = "";
    await foreach (var chunk in response)
    {
        Console.Write(chunk);
        fullResponse += chunk;
    }

    // Add LLM response to history
    history.AddAssistantMessage(fullResponse);
}