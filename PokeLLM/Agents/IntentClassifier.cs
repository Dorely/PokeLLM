using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

namespace PokeLLM.Agents;

public interface IIntentClassifier
{
    Task<GameIntent> ClassifyAsync(string userInput, GameContext context, CancellationToken cancellationToken = default);
    Task<(GameIntent Intent, double Confidence)> ClassifyWithConfidenceAsync(string userInput, GameContext context, CancellationToken cancellationToken = default);
}

public class LLMIntentClassifier : IIntentClassifier
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public LLMIntentClassifier(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<GameIntent> ClassifyAsync(string userInput, GameContext context, CancellationToken cancellationToken = default)
    {
        var (intent, _) = await ClassifyWithConfidenceAsync(userInput, context, cancellationToken);
        return intent;
    }

    public async Task<(GameIntent Intent, double Confidence)> ClassifyWithConfidenceAsync(string userInput, GameContext context, CancellationToken cancellationToken = default)
    {
        var prompt = CreateClassificationPrompt(userInput, context);
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(prompt);
        chatHistory.AddUserMessage(userInput);

        var responses = await _chatService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings: new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                MaxTokens = 100
            },
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault();

        return ParseClassificationResponse(response.Content ?? "");
    }

    private string CreateClassificationPrompt(string userInput, GameContext context)
    {
        var availableIntents = Enum.GetNames<GameIntent>().Where(name => name != "Unknown");
        
        return $$"""
            You are an intent classifier for a Pokemon RPG game. Classify the user's input into one of these intents:
            
            {{string.Join(", ", availableIntents)}}
            
            Current game context:
            - Player: {{context.PlayerState.Name}} (Level {{context.PlayerState.Level}})
            - Player vigor: {{context.PlayerState.Stats.CurrentVigor}}/{{context.PlayerState.Stats.MaxVigor}}
            - Player class: {{context.PlayerState.CharacterDetails.Class}}
            
            Respond with JSON in this format:
            {
                "intent": "IntentName",
                "confidence": 0.95
            }
            
            If you're unsure, use "Unknown" with lower confidence.
            """;
    }

    private (GameIntent Intent, double Confidence) ParseClassificationResponse(string response)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;
            
            if (root.TryGetProperty("intent", out var intentElement) &&
                root.TryGetProperty("confidence", out var confidenceElement))
            {
                var intentStr = intentElement.GetString();
                var confidence = confidenceElement.GetDouble();
                
                if (Enum.TryParse<GameIntent>(intentStr, true, out var intent))
                {
                    return (intent, confidence);
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to default
        }
        
        return (GameIntent.Unknown, 0.0);
    }
}

public class RuleBasedIntentClassifier : IIntentClassifier
{
    private readonly Dictionary<string[], GameIntent> _rules = new()
    {
        { new[] { "attack", "fight", "battle", "combat" }, GameIntent.Attack },
        { new[] { "move", "go", "travel", "walk", "run" }, GameIntent.Move },
        { new[] { "look", "examine", "inspect", "check" }, GameIntent.Examine },
        { new[] { "talk", "speak", "chat", "say" }, GameIntent.Talk },
        { new[] { "search", "find", "explore" }, GameIntent.Search },
        { new[] { "use", "item" }, GameIntent.UseItem },
        { new[] { "flee", "escape", "run away" }, GameIntent.Flee },
        { new[] { "rest", "sleep", "heal" }, GameIntent.Rest },
        { new[] { "status", "stats", "character" }, GameIntent.CheckStatus },
        { new[] { "inventory", "items", "bag" }, GameIntent.ManageInventory },
        { new[] { "save" }, GameIntent.Save },
        { new[] { "load" }, GameIntent.Load },
        { new[] { "help" }, GameIntent.Help },
        { new[] { "exit", "quit", "leave" }, GameIntent.Exit }
    };

    public Task<GameIntent> ClassifyAsync(string userInput, GameContext context, CancellationToken cancellationToken = default)
    {
        var lowercaseInput = userInput.ToLowerInvariant();
        
        foreach (var (keywords, intent) in _rules)
        {
            if (keywords.Any(keyword => lowercaseInput.Contains(keyword)))
            {
                return Task.FromResult(intent);
            }
        }
        
        return Task.FromResult(GameIntent.Unknown);
    }

    public async Task<(GameIntent Intent, double Confidence)> ClassifyWithConfidenceAsync(string userInput, GameContext context, CancellationToken cancellationToken = default)
    {
        var intent = await ClassifyAsync(userInput, context, cancellationToken);
        var confidence = intent == GameIntent.Unknown ? 0.1 : 0.8;
        return (intent, confidence);
    }
}