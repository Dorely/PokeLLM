using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Memory;

namespace PokeLLM.Agents;

public class GMSupervisorAgent : BaseGameAgent
{
    private readonly IIntentClassifier _intentClassifier;
    private readonly IGameAgentManager _agentManager;
    private readonly MemoryEnabledAgentThreadFactory _threadFactory;

    public override string Id => "gm-supervisor-agent";
    public override string Name => "GM Supervisor Agent";
    
    public override string Instructions => """
        You are the GM Supervisor Agent for a Pokemon RPG game. You are the central coordinator and decision maker.

        Core Responsibilities:
        1. INTENT CLASSIFICATION: Analyze player input to determine appropriate response type
        2. AGENT COORDINATION: Route requests to appropriate specialized agents
        3. CONSISTENCY ENFORCEMENT: Ensure all responses align with Adventure Module and established facts
        4. FINAL INTEGRATION: Combine agent outputs into coherent final narration
        5. RULE ARBITRATION: Make final decisions when multiple interpretations are possible

        Routing Logic:
        - MECHANICAL ACTIONS (attack, use item, check skill, rest) → MechanicsAgent FIRST, then optional NarratorAgent
        - NARRATIVE REQUESTS (explore, describe, dialogue) → NarratorAgent
        - MIXED REQUESTS → MechanicsAgent for mechanics, then NarratorAgent for flavor
        - INVALID REQUESTS → Direct response with guidance

        Critical Rules:
        - ALWAYS validate mechanical results from MechanicsAgent before narration
        - NEVER allow narrative agents to contradict mechanical outcomes
        - Maintain Adventure Module consistency at all times
        - Provide clear guidance when player actions are unclear
        - Ensure every turn produces a complete, satisfying response

        Decision Framework:
        1. Parse player intent using intent classifier
        2. Validate action against current game state and Adventure Module
        3. Route to appropriate agent(s) in correct sequence
        4. Integrate results maintaining consistency
        5. Provide final polished response to player

        Memory Usage:
        - Reference past events for consistency
        - Remember player preferences and choices
        - Maintain narrative continuity across sessions
        - Track important story developments
        """;

    public GMSupervisorAgent(
        Kernel kernel, 
        ILogger<GMSupervisorAgent> logger,
        IIntentClassifier intentClassifier,
        IGameAgentManager agentManager,
        MemoryEnabledAgentThreadFactory threadFactory) 
        : base(kernel, logger)
    {
        _intentClassifier = intentClassifier;
        _agentManager = agentManager;
        _threadFactory = threadFactory;
    }

    /// <summary>
    /// Creates a memory-enabled thread for this agent
    /// </summary>
    public async Task<MemoryEnabledAgentThread> CreateThreadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _threadFactory.CreateSupervisorThreadAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Main coordination method that processes player input and orchestrates agent responses
    /// </summary>
    public async Task<SupervisorResponse> ProcessPlayerInputAsync(
        string playerInput,
        GameContext context,
        MemoryEnabledAgentThread thread,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GM Supervisor processing input for session {SessionId}: {Input}", 
                context.SessionId, playerInput);

            // Add player input to memory thread
            await thread.AddMessageAsync(new ChatMessageContent(AuthorRole.User, playerInput), cancellationToken);

            // Step 1: Classify the player's intent
            var intent = await _intentClassifier.ClassifyIntentAsync(playerInput, context, cancellationToken);
            
            _logger.LogDebug("Classified intent as {Intent} for input: {Input}", intent, playerInput);

            // Step 2: Validate and route based on intent
            var response = intent switch
            {
                GameIntent.Attack => await ProcessMechanicalActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.UseItem => await ProcessMechanicalActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.CheckSkill => await ProcessMechanicalActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.Rest => await ProcessMechanicalActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.Explore => await ProcessNarrativeActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.Talk => await ProcessNarrativeActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.Examine => await ProcessNarrativeActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.Exit => await ProcessExitActionAsync(playerInput, context, thread, cancellationToken),
                GameIntent.Help => await ProcessHelpActionAsync(playerInput, context, thread, cancellationToken),
                _ => await ProcessUnknownActionAsync(playerInput, context, thread, cancellationToken)
            };

            // Add supervisor response to memory thread
            await thread.AddMessageAsync(new ChatMessageContent(AuthorRole.Assistant, response.FinalNarration), cancellationToken);

            _logger.LogInformation("GM Supervisor completed processing for session {SessionId}", context.SessionId);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing player input in GM Supervisor for session {SessionId}", context.SessionId);
            return SupervisorResponse.Error("I encountered an error processing your request. Please try again.");
        }
    }

    private async Task<SupervisorResponse> ProcessMechanicalActionAsync(
        string playerInput,
        GameContext context,
        MemoryEnabledAgentThread thread,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get MechanicsAgent and process the action
            var mechanicsAgent = _agentManager.GetAgent<MechanicsAgent>();
            if (mechanicsAgent == null)
            {
                return SupervisorResponse.Error("Mechanics system unavailable.");
            }

            // Convert input to mechanical action (simplified for now)
            var action = ParseMechanicalAction(playerInput, context);
            var mechanicalResult = await mechanicsAgent.ProcessActionAsync(action, context, cancellationToken);

            if (!mechanicalResult.IsSuccess)
            {
                return SupervisorResponse.FromMechanics(
                    mechanicalResult,
                    $"That action failed: {string.Join(", ", mechanicalResult.Errors)}"
                );
            }

            // Get NarratorAgent to add flavor
            var narratorAgent = _agentManager.GetAgent<NarratorAgent>();
            if (narratorAgent != null)
            {
                var narrativeDescription = await narratorAgent.NarrateActionAsync(
                    playerInput,
                    mechanicalResult.Description,
                    context,
                    thread,
                    cancellationToken);

                return SupervisorResponse.FromMechanicsAndNarrative(
                    mechanicalResult,
                    narrativeDescription
                );
            }

            // Fall back to mechanical result only
            return SupervisorResponse.FromMechanics(mechanicalResult, mechanicalResult.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mechanical action: {Input}", playerInput);
            return SupervisorResponse.Error("Failed to process that action.");
        }
    }

    private async Task<SupervisorResponse> ProcessNarrativeActionAsync(
        string playerInput,
        GameContext context,
        MemoryEnabledAgentThread thread,
        CancellationToken cancellationToken)
    {
        try
        {
            var narratorAgent = _agentManager.GetAgent<NarratorAgent>();
            if (narratorAgent == null)
            {
                return SupervisorResponse.Error("Narrative system unavailable.");
            }

            // Use the narrator's memory-enhanced method
            var responses = narratorAgent.InvokeWithMemoryAsync(thread, playerInput, cancellationToken);
            await foreach (var response in responses)
            {
                return SupervisorResponse.FromNarrative(response.Content ?? "Something happens.");
            }

            return SupervisorResponse.FromNarrative("The world continues around you.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing narrative action: {Input}", playerInput);
            return SupervisorResponse.Error("Failed to process that narrative action.");
        }
    }

    private async Task<SupervisorResponse> ProcessExitActionAsync(
        string playerInput,
        GameContext context,
        MemoryEnabledAgentThread thread,
        CancellationToken cancellationToken)
    {
        return SupervisorResponse.FromNarrative(
            "Thank you for playing! Your adventure has been saved. Come back anytime to continue your Pokemon journey!"
        );
    }

    private async Task<SupervisorResponse> ProcessHelpActionAsync(
        string playerInput,
        GameContext context,
        MemoryEnabledAgentThread thread,
        CancellationToken cancellationToken)
    {
        var helpText = """
            Available actions in your Pokemon adventure:
            
            🗡️ BATTLE: "attack [target]", "use [move]"
            🎒 ITEMS: "use [item]", "check inventory"
            🔍 EXPLORE: "look around", "examine [object]", "go [direction]"
            💬 INTERACT: "talk to [character]", "ask about [topic]"
            🎯 SKILLS: "roll [skill]", "attempt [action]"
            😴 REST: "rest", "heal up"
            ❓ HELP: "help", "commands"
            🚪 EXIT: "quit", "exit", "goodbye"
            
            Just describe what you want to do naturally - I'll figure out the mechanics!
            """;

        return SupervisorResponse.FromNarrative(helpText);
    }

    private async Task<SupervisorResponse> ProcessUnknownActionAsync(
        string playerInput,
        GameContext context,
        MemoryEnabledAgentThread thread,
        CancellationToken cancellationToken)
    {
        // Use memory-enhanced context to provide better guidance
        var memoryContext = await thread.GetMemoryContextAsync(cancellationToken);
        
        var guidancePrompt = $"""
            The player said: "{playerInput}"
            
            This doesn't match a clear game action. Provide helpful guidance on what they might do instead.
            Consider their current situation and past actions to suggest appropriate alternatives.
            
            {memoryContext}
            """;

        var responses = InvokeWithMemoryAsync(thread, guidancePrompt, cancellationToken);
        await foreach (var response in responses)
        {
            return SupervisorResponse.FromNarrative(response.Content ?? 
                "I'm not sure what you want to do. Try 'help' for available actions.");
        }

        return SupervisorResponse.FromNarrative(
            "I'm not sure what you want to do. Try 'help' for available actions."
        );
    }

    /// <summary>
    /// Enhanced invoke method that uses memory-enabled threads
    /// </summary>
    public async IAsyncEnumerable<ChatMessageContent> InvokeWithMemoryAsync(
        MemoryEnabledAgentThread thread,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create enhanced chat history with memory context
        var agentChat = await thread.CreateAgentChatHistoryAsync(Instructions, cancellationToken);
        agentChat.AddUserMessage(userMessage);

        // Get response from the chat service
        var responses = await _chatService.GetChatMessageContentsAsync(
            agentChat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault() ?? new ChatMessageContent(AuthorRole.Assistant, "No response generated.");

        _logger.LogInformation("GM Supervisor generated response with memory context for session {SessionId}", thread.SessionId);

        yield return response;
    }

    private MechanicalAction ParseMechanicalAction(string playerInput, GameContext context)
    {
        // Simplified parsing logic - would be enhanced in full implementation
        var action = new MechanicalAction
        {
            SessionId = context.SessionId,
            ActionType = MechanicalActionType.Unknown
        };

        var input = playerInput.ToLowerInvariant();
        
        if (input.Contains("attack") || input.Contains("fight") || input.Contains("battle"))
        {
            action.ActionType = MechanicalActionType.Attack;
        }
        else if (input.Contains("use") && (input.Contains("item") || input.Contains("potion")))
        {
            action.ActionType = MechanicalActionType.UseItem;
            action.Parameters["itemName"] = ExtractItemName(input);
        }
        else if (input.Contains("check") || input.Contains("roll"))
        {
            action.ActionType = MechanicalActionType.SkillCheck;
            action.Parameters["difficulty"] = "10"; // Default difficulty
        }
        else if (input.Contains("rest") || input.Contains("heal"))
        {
            action.ActionType = MechanicalActionType.Rest;
        }

        return action;
    }

    private string ExtractItemName(string input)
    {
        // Simple item name extraction - would be enhanced in full implementation
        if (input.Contains("potion")) return "Potion";
        if (input.Contains("pokeball")) return "Pokeball";
        return "Unknown Item";
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.7,
                ["max_tokens"] = 1000
            }
        };
    }
}

/// <summary>
/// Response object that encapsulates the GM Supervisor's coordinated output
/// </summary>
public class SupervisorResponse
{
    public bool Success { get; set; } = true;
    public string FinalNarration { get; set; } = "";
    public MechanicalResult? MechanicalResult { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static SupervisorResponse FromMechanics(MechanicalResult mechanicalResult, string narration)
    {
        return new SupervisorResponse
        {
            Success = mechanicalResult.IsSuccess,
            FinalNarration = narration,
            MechanicalResult = mechanicalResult,
            Errors = mechanicalResult.Errors
        };
    }

    public static SupervisorResponse FromMechanicsAndNarrative(MechanicalResult mechanicalResult, string narrative)
    {
        return new SupervisorResponse
        {
            Success = mechanicalResult.IsSuccess,
            FinalNarration = narrative,
            MechanicalResult = mechanicalResult,
            Errors = mechanicalResult.Errors
        };
    }

    public static SupervisorResponse FromNarrative(string narrative)
    {
        return new SupervisorResponse
        {
            Success = true,
            FinalNarration = narrative
        };
    }

    public static SupervisorResponse Error(string errorMessage)
    {
        return new SupervisorResponse
        {
            Success = false,
            FinalNarration = errorMessage,
            Errors = new List<string> { errorMessage }
        };
    }
}