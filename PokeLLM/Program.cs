using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.Data;
using PokeLLM.Game.LLM;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.VectorStore;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.GameState;
using PokeLLM.GameState.Interfaces;
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

services.AddTransient<ILLMProvider, OpenAiProvider>();
services.AddTransient<IVectorStoreService, VectorStoreService>();
services.AddSingleton<IGameStateRepository, GameStateRepository>();

var provider = services.BuildServiceProvider();

//var store = provider.GetRequiredService<IVectorStoreService>();

//var collection = await store.GetGameHistory();
//await store.Upsert(collection, "test", "This is a test to see if upsert works");

var llm = provider.GetRequiredService<ILLMProvider>();
llm.RegisterPlugins(provider.GetRequiredService<IVectorStoreService>());

// Create chat history
var history = llm.CreateHistory();

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
    //var response = await llm.GetCompletionAsync(input, history);
    //Console.WriteLine($"LLM: {response}\n");
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