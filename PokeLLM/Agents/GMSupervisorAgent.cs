using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace PokeLLM.Agents;

public class GMSupervisorAgent : BaseGameAgent
{
    public override string Id => "gm-supervisor";
    public override string Name => "GM Supervisor Agent";
    
    public override string Instructions => """
        You are the GM Supervisor Agent, the central coordinator for the Pokemon RPG game. Your role is critical for maintaining game flow and consistency.

        Core Responsibilities:
        1. INTENT CLASSIFICATION: Analyze player input and classify it into specific game intents
        2. AGENT ROUTING: Determine which subordinate agents should handle the request
        3. RULE ENFORCEMENT: Ensure all game rules and Adventure Module consistency are maintained
        4. OUTPUT COORDINATION: Merge outputs from subordinate agents into coherent turn narration
        5. GAME STATE OVERSIGHT: Monitor overall game progression and pacing

        Agent Routing Rules:
        - Mechanical actions (combat, item use, skill checks) → Mechanics Agent
        - Narrative descriptions and world-building → Narrator Agent  
        - Complex scenarios may require both agents in sequence
        - Always maintain Adventure Module canon and established facts

        Consistency Requirements:
        - Never contradict established Adventure Module content
        - Maintain character personalities and world rules
        - Ensure Pokemon universe authenticity
        - Track quest progression and world state changes

        Communication Style:
        - Clear, authoritative coordination
        - Preserve player agency while guiding narrative
        - Balance mechanical accuracy with engaging storytelling
        - Provide context for subordinate agents when routing requests
        """;

    private readonly IIntentClassifier _intentClassifier;
    private readonly IGameAgentManager _agentManager;

    public GMSupervisorAgent(
        Kernel kernel, 
        ILogger<GMSupervisorAgent> logger,
        IIntentClassifier intentClassifier,
        IGameAgentManager agentManager) 
        : base(kernel, logger)
    {
        _intentClassifier = intentClassifier;
        _agentManager = agentManager;
    }

    public async IAsyncEnumerable<ChatMessageContent> SuperviseAndRouteAsync(
        string userInput,
        GameContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GM Supervisor processing user input: {Input}", userInput);

        // Step 1: Classify intent
        var (intent, confidence) = await _intentClassifier.ClassifyWithConfidenceAsync(
            userInput, context, cancellationToken);

        _logger.LogInformation("Classified intent: {Intent} (confidence: {Confidence:F2})", 
            intent, confidence);

        // Step 2: Route to appropriate agents based on intent
        var responses = new List<ChatMessageContent>();

        if (IsMechanicalIntent(intent))
        {
            var mechanicsAgent = _agentManager.GetAgent("mechanics-agent");
            if (mechanicsAgent != null)
            {
                var mechanicsChat = CreateMechanicsContext(userInput, context, intent);
                await foreach (var response in mechanicsAgent.InvokeAsync(mechanicsChat, cancellationToken))
                {
                    responses.Add(response);
                }
            }
        }

        // Always get narrative enhancement unless it's a pure mechanical operation
        if (ShouldIncludeNarrative(intent))
        {
            var narratorAgent = _agentManager.GetAgent("narrator-agent");
            if (narratorAgent != null)
            {
                var narratorChat = CreateNarratorContext(userInput, context, intent, responses);
                await foreach (var response in narratorAgent.InvokeAsync(narratorChat, cancellationToken))
                {
                    responses.Add(response);
                }
            }
        }

        // Step 3: Aggregate and coordinate final response
        var finalResponse = await CoordinateFinalResponse(userInput, intent, responses, context, cancellationToken);
        yield return finalResponse;
    }

    private bool IsMechanicalIntent(GameIntent intent)
    {
        return intent switch
        {
            GameIntent.Attack => true,
            GameIntent.UseItem => true,
            GameIntent.Rest => true,
            GameIntent.CheckStatus => true,
            GameIntent.ManageInventory => true,
            GameIntent.LevelUp => true,
            _ => false
        };
    }

    private bool ShouldIncludeNarrative(GameIntent intent)
    {
        return intent switch
        {
            GameIntent.CheckStatus => false,
            GameIntent.ManageInventory => false,
            GameIntent.Help => false,
            GameIntent.Save => false,
            GameIntent.Load => false,
            _ => true
        };
    }

    private ChatHistory CreateMechanicsContext(string userInput, GameContext context, GameIntent intent)
    {
        var chat = new ChatHistory();
        chat.AddSystemMessage($"Process this {intent} action mechanically:");
        chat.AddUserMessage($"Player action: {userInput}");
        chat.AddAssistantMessage($"Current player state: {JsonSerializePlayerState(context.CurrentState.Player)}");
        return chat;
    }

    private ChatHistory CreateNarratorContext(string userInput, GameContext context, GameIntent intent, List<ChatMessageContent> mechanicsResults)
    {
        var chat = new ChatHistory();
        chat.AddSystemMessage($"Provide narrative for this {intent} action:");
        chat.AddUserMessage($"Player action: {userInput}");
        
        if (mechanicsResults.Any())
        {
            var mechanicsOutput = string.Join("\n", mechanicsResults.Select(r => r.Content));
            chat.AddAssistantMessage($"Mechanical results: {mechanicsOutput}");
        }
        
        return chat;
    }

    private async Task<ChatMessageContent> CoordinateFinalResponse(
        string userInput, 
        GameIntent intent, 
        List<ChatMessageContent> agentResponses, 
        GameContext context,
        CancellationToken cancellationToken)
    {
        if (!agentResponses.Any())
        {
            return new ChatMessageContent(AuthorRole.Assistant, 
                "I'm not sure how to respond to that. Could you try rephrasing your action?");
        }

        if (agentResponses.Count == 1)
        {
            return agentResponses[0];
        }

        // Coordinate multiple agent responses
        var coordinationPrompt = $"""
            Coordinate the following agent responses into a single, coherent turn narration:
            
            Player action: {userInput}
            Intent: {intent}
            
            Agent responses:
            {string.Join("\n---\n", agentResponses.Select(r => r.Content))}
            
            Create a unified response that incorporates both mechanical and narrative elements naturally.
            """;

        var chat = new ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(coordinationPrompt);

        var responses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault() ?? new ChatMessageContent(AuthorRole.Assistant, "Unable to coordinate response.");

        return response;
    }

    private string JsonSerializePlayerState(State.PlayerState playerState)
    {
        return $"{{\"Name\":\"{playerState.Name}\",\"Level\":{playerState.Level},\"Health\":{playerState.Health},\"Location\":\"{playerState.CurrentLocation}\"}}";
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.3,
                ["max_tokens"] = 1500
            }
        };
    }
}