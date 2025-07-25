using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.ContextGathering.Interfaces;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState.Models;
using System.Text.Json;
using System.Diagnostics;

namespace PokeLLM.Game.ContextGathering;

public class ContextGatheringService : IContextGatheringService
{
    private readonly Kernel _kernel;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IVectorStoreService _vectorStoreService;

    public ContextGatheringService(
        IOptions<ModelConfig> options, 
        IGameStateRepository gameStateRepository, 
        IVectorStoreService vectorStoreService)
    {
        _gameStateRepository = gameStateRepository;
        _vectorStoreService = vectorStoreService;

        var apiKey = options.Value.ApiKey;
        var modelId = options.Value.ModelId;

        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey
        );

        // Register plugin services in kernel DI
        kernelBuilder.Services.AddSingleton(_gameStateRepository);
        kernelBuilder.Services.AddSingleton(_vectorStoreService);

        _kernel = kernelBuilder.Build();

        // Load all plugins for context gathering
        LoadContextGatheringPlugins();
    }

    public async Task<GameContext> GatherContextAsync(
        string playerInput, 
        string adventureSummary, 
        List<string> recentHistory, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contextGatheringPrompt = await LoadContextGatheringPromptAsync();
            var recentHistoryText = string.Join("\n", recentHistory);

            var prompt = $@"{contextGatheringPrompt}

## Current Task
Gather context for the following player input:

**Player Input**: {playerInput}

**Adventure Summary**: {adventureSummary}

**Recent History**: 
{recentHistoryText}

Use the available functions to gather all necessary context. When you have completed your research, respond with a JSON object that matches the GameContext structure with the following properties:
- relevantEntities: Dictionary of entity IDs to entity objects
- missingEntities: Array of entity names that were referenced but not found
- gameStateUpdates: Array of strings describing any changes made
- vectorStoreData: Array of VectorStoreResult objects with relevant information
- contextSummary: String summary of gathered context
- recommendedActions: Array of suggested actions for the main game chat

Begin your context gathering now:";

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.1 // Low temperature for more consistent analysis
            };

            var result = await _kernel.InvokePromptAsync(
                prompt,
                new KernelArguments(executionSettings),
                cancellationToken: cancellationToken
            );

            var responseText = result.ToString();
            
            // Try to parse the JSON response
            var gameContext = TryParseGameContext(responseText);
            
            Debug.WriteLine($"Context gathering completed for input: {playerInput}");
            Debug.WriteLine($"Found {gameContext.RelevantEntities.Count} relevant entities");
            Debug.WriteLine($"Found {gameContext.VectorStoreData.Count} vector store results");
            
            return gameContext;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in context gathering: {ex.Message}");
            
            // Return a minimal context object on error
            return new GameContext
            {
                ContextSummary = $"Error gathering context: {ex.Message}",
                RecommendedActions = ["Proceed with available information"]
            };
        }
    }

    private GameContext TryParseGameContext(string responseText)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var gameContext = JsonSerializer.Deserialize<GameContext>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return gameContext ?? new GameContext();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse GameContext JSON: {ex.Message}");
        }

        // Fallback: create a basic context object
        return new GameContext
        {
            ContextSummary = "Failed to parse context response",
            RecommendedActions = ["Proceed with minimal context"]
        };
    }

    private void LoadContextGatheringPlugins()
    {
        try
        {
            // Load all available plugins for comprehensive context gathering
            var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
            _kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
            
            var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
            _kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
            
            var dicePlugin = new DicePlugin(_gameStateRepository);
            _kernel.ImportPluginFromObject(dicePlugin, "Dice");
            
            Debug.WriteLine("Context gathering plugins loaded: VectorStore, GameEngine, Dice");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading context gathering plugins: {ex.Message}");
        }
    }

    private async Task<string> LoadContextGatheringPromptAsync()
    {
        try
        {
            var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ContextGatheringSubroutine.md");
            return await File.ReadAllTextAsync(promptPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load context gathering prompt: {ex.Message}");
            return @"You are a context gathering subroutine. Your job is to gather all necessary context for the main game chat.
                     Use the available functions to search for entities and information relevant to the player input.
                     Return a structured JSON response with your findings.";
        }
    }
}