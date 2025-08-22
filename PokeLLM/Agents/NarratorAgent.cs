using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace PokeLLM.Agents;

public class NarratorAgent : BaseGameAgent
{
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

    public NarratorAgent(Kernel kernel, ILogger<NarratorAgent> logger) 
        : base(kernel, logger)
    {
    }

    public async Task<string> NarrateActionAsync(
        string playerAction,
        string mechanicalResult,
        GameContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateNarrationPrompt(playerAction, mechanicalResult, context);
        
        var chat = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var responses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault();

        return response.Content ?? "The action unfolds before you.";
    }

    public async Task<string> DescribeLocationAsync(
        string locationName,
        GameContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateLocationPrompt(locationName, context);
        
        var chat = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var responses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault();

        return response.Content ?? $"You find yourself in {locationName}.";
    }

    public async Task<string> VoiceNPCDialogueAsync(
        string npcName,
        string context,
        string playerInput,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateDialoguePrompt(npcName, context, playerInput);
        
        var chat = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var responses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault();

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