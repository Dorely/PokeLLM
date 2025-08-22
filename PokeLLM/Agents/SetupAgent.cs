using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.State;
using System.Text.Json;

namespace PokeLLM.Agents;

public class SetupAgent : BaseGameAgent
{
    public override string Id => "setup-agent";
    public override string Name => "Setup Agent";
    
    public override string Instructions => """
        You are the Setup Agent for a Pokemon RPG game. Your role is to guide players through initial game setup and generate a structured Adventure Module.

        Your responsibilities:
        1. Collect player preferences for setting, character background, and initial story hooks
        2. Generate a comprehensive Adventure Module JSON that includes:
           - Player character with name, backstory, and initial stats
           - Initial quests and objectives
           - NPC seeds with personalities and roles
           - Regional structure with locations
           - Plot hooks to drive narrative

        When generating the Adventure Module, ensure:
        - The setting is cohesive and engaging
        - NPCs have distinct personalities and clear motivations
        - Quests have clear objectives and progression
        - The world feels alive with interconnected elements
        - Everything follows Pokemon universe conventions

        Always validate your JSON output against the schema and ensure all required fields are populated with meaningful content.
        """;

    private readonly IEventLog _eventLog;

    public SetupAgent(Kernel kernel, ILogger<SetupAgent> logger, IEventLog eventLog) 
        : base(kernel, logger)
    {
        _eventLog = eventLog;
    }

    public async Task<AdventureModule> GenerateAdventureModuleAsync(
        string playerName,
        string characterBackstory,
        string preferredSetting,
        CancellationToken cancellationToken = default)
    {
        var prompt = CreateAdventureModulePrompt(playerName, characterBackstory, preferredSetting);
        
        var chat = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chat.AddSystemMessage(Instructions);
        chat.AddUserMessage(prompt);

        var responses = await _chatService.GetChatMessageContentsAsync(
            chat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault();

        var adventureModule = ParseAdventureModuleResponse(response.Content ?? "", playerName, characterBackstory);
        
        await _eventLog.AppendEventAsync(
            GameEvent.Create("adventure_module_generated", "Adventure module created by Setup Agent", 
                new Dictionary<string, object> { ["module_id"] = adventureModule.Id }),
            cancellationToken);

        return adventureModule;
    }

    private string CreateAdventureModulePrompt(string playerName, string characterBackstory, string preferredSetting)
    {
        return $"""
            Generate a complete Adventure Module for a Pokemon RPG with the following player preferences:
            
            Player Name: {playerName}
            Character Backstory: {characterBackstory}
            Preferred Setting: {preferredSetting}
            
            Create a JSON structure with:
            1. Player character with appropriate stats and abilities
            2. 3-5 initial quests with clear objectives
            3. 5-8 NPC seeds with distinct personalities
            4. 3-4 regions with multiple locations each
            5. 4-6 plot hooks to drive the narrative
            
            Respond with valid JSON only - no additional text or formatting.
            """;
    }

    private AdventureModule ParseAdventureModuleResponse(string response, string playerName, string backstory)
    {
        try
        {
            // Try to parse the JSON response
            var jsonDoc = JsonDocument.Parse(response);
            
            // For now, create a basic adventure module
            // In a full implementation, this would parse the LLM's JSON response
            return CreateDefaultAdventureModule(playerName, backstory);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse Adventure Module JSON, using default");
            return CreateDefaultAdventureModule(playerName, backstory);
        }
    }

    private AdventureModule CreateDefaultAdventureModule(string playerName, string backstory)
    {
        var playerId = Guid.NewGuid().ToString();
        
        var playerCharacter = new PlayerCharacter(
            Name: playerName,
            Backstory: backstory,
            Stats: new Dictionary<string, int>
            {
                ["Level"] = 1,
                ["Health"] = 100,
                ["Attack"] = 10,
                ["Defense"] = 10,
                ["Speed"] = 10
            },
            Abilities: new List<string> { "Tackle", "Growl" }
        );

        var quests = new List<Quest>
        {
            new Quest("quest-1", "First Steps", "Begin your Pokemon journey", QuestStatus.Active, 
                new List<string> { "Visit Professor Oak", "Choose your starter Pokemon" }),
            new Quest("quest-2", "Gym Challenge", "Challenge the local Gym Leader", QuestStatus.NotStarted,
                new List<string> { "Train your Pokemon", "Battle Gym Leader" })
        };

        var npcSeeds = new List<NpcSeed>
        {
            new NpcSeed("npc-1", "Professor Oak", "Pokemon Researcher", "Wise and helpful", "Pallet Town"),
            new NpcSeed("npc-2", "Brock", "Gym Leader", "Rock-type specialist", "Pewter City"),
            new NpcSeed("npc-3", "Nurse Joy", "Pokemon Center Nurse", "Caring and dedicated", "Pokemon Center")
        };

        var regions = new List<Region>
        {
            new Region("region-1", "Kanto", "The classic Pokemon region", 
                new List<string> { "Pallet Town", "Pewter City", "Cerulean City" })
        };

        var plotHooks = new List<PlotHook>
        {
            new PlotHook("hook-1", "Mysterious Pokemon sightings in the forest", "Enter Viridian Forest"),
            new PlotHook("hook-2", "Team Rocket spotted in the area", "Investigate suspicious activity")
        };

        return new AdventureModule(
            Id: Guid.NewGuid().ToString(),
            Title: "Pokemon Adventure",
            Setting: "Kanto Region",
            Theme: "Classic Pokemon Journey",
            PlayerCharacter: playerCharacter,
            Quests: quests,
            NpcSeeds: npcSeeds,
            Regions: regions,
            PlotHooks: plotHooks,
            CreatedAt: DateTime.UtcNow
        );
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.8,
                ["max_tokens"] = 2000
            }
        };
    }
}