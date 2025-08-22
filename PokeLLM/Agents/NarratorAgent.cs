using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Memory;

namespace PokeLLM.Agents;

public class NarratorAgent : BaseGameAgent
{
    private readonly MemoryEnabledAgentThreadFactory _threadFactory;

    public override string Id => "narrator-agent";
    public override string Name => "Narrator Agent";
    
    public override string Instructions => """
        You are the Narrator Agent for a Pokemon RPG game. Your role is to create immersive, engaging prose that brings the Pokemon world to life.

        Core Responsibilities:
        1. IMMERSIVE DESCRIPTIONS: Paint vivid scenes of locations, Pokemon, and events
        2. DIALOGUE & CHARACTER: Bring NPCs to life with distinct voices and personalities
        3. ATMOSPHERIC DETAILS: Add sensory details that enhance immersion
        4. EMOTIONAL RESONANCE: Capture the excitement, tension, and wonder of Pokemon adventures
        5. CANON PRESERVATION: Never alter or contradict established facts or mechanical outcomes

        Critical Rules:
        - NEVER change numerical values, stats, or mechanical outcomes
        - NEVER contradict established Adventure Module content
        - NEVER alter player inventory, health, or Pokemon status
        - Always work WITH mechanical results, not against them
        - Focus on HOW things happen, not WHAT mechanically happens

        Writing Style:
        - Evocative and descriptive prose
        - Present tense for immediacy
        - Pokemon universe authenticity
        - Age-appropriate content
        - Balance action with character moments
        - Concise yet impactful descriptions

        What You Do:
        - Describe Pokemon battle animations and effects
        - Set atmospheric mood for locations
        - Voice NPC dialogue and reactions
        - Narrate travel and exploration
        - Add flavor to mechanical actions

        What You DON'T Do:
        - Calculate damage or stats
        - Determine battle outcomes
        - Change inventory or Pokemon status
        - Make mechanical decisions
        - Contradict previous established facts
        """;

    public NarratorAgent(
        Kernel kernel, 
        ILogger<NarratorAgent> logger,
        MemoryEnabledAgentThreadFactory threadFactory) 
        : base(kernel, logger)
    {
        _threadFactory = threadFactory;
    }

    /// <summary>
    /// Creates a memory-enabled thread for this agent
    /// </summary>
    public async Task<MemoryEnabledAgentThread> CreateThreadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _threadFactory.CreateNarratorThreadAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Enhanced invoke method that uses memory-enabled threads
    /// </summary>
    public async IAsyncEnumerable<ChatMessageContent> InvokeWithMemoryAsync(
        MemoryEnabledAgentThread thread,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Add user message to thread (this will notify memory components)
        await thread.AddMessageAsync(new ChatMessageContent(AuthorRole.User, userMessage), cancellationToken);

        // Create enhanced chat history with memory context
        var agentChat = await thread.CreateAgentChatHistoryAsync(Instructions, cancellationToken);

        // Get response from the chat service
        var responses = await _chatService.GetChatMessageContentsAsync(
            agentChat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault() ?? new ChatMessageContent(AuthorRole.Assistant, "No response generated.");

        // Add assistant response to thread
        await thread.AddMessageAsync(response, cancellationToken);

        _logger.LogInformation("Narrator Agent generated response with memory context for session {SessionId}", thread.SessionId);

        yield return response;
    }

    public async Task<string> NarrateActionAsync(
        string playerAction,
        string mechanicalResult,
        GameContext context,
        MemoryEnabledAgentThread? thread = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateNarrationPrompt(playerAction, mechanicalResult, context);
        
        if (thread != null)
        {
            // Use memory-enhanced approach
            var responses = InvokeWithMemoryAsync(thread, prompt, cancellationToken);
            await foreach (var memoryResponse in responses)
            {
                return memoryResponse.Content ?? "The action unfolds before you.";
            }
        }

        // Fall back to basic approach if no thread provided
        var chat = new ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var basicResponses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = basicResponses.FirstOrDefault();
        return response.Content ?? "The action unfolds before you.";
    }

    public async Task<string> DescribeLocationAsync(
        string locationName,
        GameContext context,
        MemoryEnabledAgentThread? thread = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateLocationPrompt(locationName, context);
        
        if (thread != null)
        {
            var responses = InvokeWithMemoryAsync(thread, prompt, cancellationToken);
            await foreach (var memoryResponse in responses)
            {
                return memoryResponse.Content ?? $"You find yourself in {locationName}.";
            }
        }

        // Fall back to basic approach
        var chat = new ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var basicResponses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = basicResponses.FirstOrDefault();
        return response.Content ?? $"You find yourself in {locationName}.";
    }

    public async Task<string> VoiceNPCDialogueAsync(
        string npcName,
        string context,
        string playerInput,
        MemoryEnabledAgentThread? thread = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateDialoguePrompt(npcName, context, playerInput);
        
        if (thread != null)
        {
            var responses = InvokeWithMemoryAsync(thread, prompt, cancellationToken);
            await foreach (var memoryResponse in responses)
            {
                return memoryResponse.Content ?? $"{npcName} looks at you thoughtfully.";
            }
        }

        // Fall back to basic approach
        var chat = new ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var basicResponses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = basicResponses.FirstOrDefault();
        return response.Content ?? $"{npcName} looks at you thoughtfully.";
    }

    private string CreateNarrationPrompt(string playerAction, string mechanicalResult, GameContext context)
    {
        return $"""
            Narrate the following action with immersive Pokemon RPG prose:
            
            Player Action: {playerAction}
            Mechanical Result: {mechanicalResult}
            Player Location: [Location tracking moved to world state]
            Player: {context.PlayerState.Name} (Level {context.PlayerState.Level})
            
            Requirements:
            - Preserve all mechanical outcomes exactly as given
            - Add vivid sensory details and Pokemon world atmosphere
            - Keep the focus on HOW the action unfolds
            - Maintain Pokemon universe authenticity
            - Write in present tense with 2-3 paragraphs maximum
            - Use any relevant past events or user information to enhance the narrative
            
            Focus on visual, auditory, and emotional elements that bring the scene to life.
            """;
    }

    private string CreateLocationPrompt(string locationName, GameContext context)
    {
        return $"""
            Describe the Pokemon location with immersive detail:
            
            Location: {locationName}
            Region: [World state tracking simplified]
            Time of Day: {GetTimeOfDay()}
            Player: {context.PlayerState.Name}
            
            Create a vivid description that includes:
            - Visual atmosphere and environmental details
            - Pokemon that might be present
            - Sounds, smells, and other sensory elements
            - Any notable landmarks or features
            - The emotional tone of the location
            - Reference any past events that occurred here if relevant
            
            Keep it concise but evocative (2-3 paragraphs).
            """;
    }

    private string CreateDialoguePrompt(string npcName, string context, string playerInput)
    {
        return $"""
            Create authentic NPC dialogue for a Pokemon RPG:
            
            NPC: {npcName}
            Context: {context}
            Player said: "{playerInput}"
            
            Generate dialogue that:
            - Reflects the NPC's personality and role
            - Fits the Pokemon universe tone
            - Responds appropriately to the player's input
            - Includes body language and mannerisms
            - Stays true to established character traits
            - References past interactions with this NPC if any exist
            
            Format as: "[Dialogue]" followed by narrative description of their actions/expressions.
            """;
    }

    private string GetTimeOfDay()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 6 and < 12 => "Morning",
            >= 12 and < 18 => "Afternoon",
            >= 18 and < 22 => "Evening",
            _ => "Night"
        };
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.8,
                ["max_tokens"] = 800
            }
        };
    }
}