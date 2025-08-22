using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.State;

namespace PokeLLM.Agents;

public class SetupAgent : BaseGameAgent
{
    private readonly IEventLog _eventLog;

    public override string Id => "setup-agent";
    public override string Name => "Setup Agent";
    
    public override string Instructions => """
        You are the Setup Agent for a Pokemon RPG game. Your role is to generate the initial adventure setup.

        Core Responsibilities:
        1. WORLD CREATION: Generate adventure modules with consistent setting, theme, and story
        2. CHARACTER INTEGRATION: Incorporate player preferences into the adventure
        3. QUEST SEEDING: Create initial quests and objectives
        4. NPC PLACEMENT: Define key NPCs and their roles
        5. NARRATIVE HOOKS: Establish compelling story elements

        Output Requirements:
        - Generate structured adventure data
        - Ensure internal consistency
        - Balance challenge and accessibility
        - Create engaging narrative opportunities
        - Establish clear starting conditions

        Style Guidelines:
        - Authentic Pokemon universe feel
        - Age-appropriate content
        - Balanced mix of exploration, combat, and story
        - Clear objectives and motivations
        - Room for player agency and choice
        """;

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
        _logger.LogInformation("Generating adventure module for player {PlayerName} with setting {Setting}", 
            playerName, preferredSetting);

        try
        {
            // Create a basic adventure module with default structure
            var adventureModule = new AdventureModule
            {
                Name = $"{playerName}'s Pokemon Adventure",
                Description = $"A personalized Pokemon adventure for {playerName}",
                Theme = preferredSetting,
                World = new WorldConfiguration
                {
                    Region = "Kanto",
                    Setting = "Classic Pokemon Adventure",
                    Difficulty = DifficultyLevel.Normal
                },
                Quests = new List<QuestTemplate>
                {
                    new QuestTemplate
                    {
                        Name = "First Steps",
                        Description = "Begin your Pokemon journey",
                        Type = QuestType.Main,
                        IsActive = true,
                        Objectives = new List<QuestObjective>
                        {
                            new QuestObjective
                            {
                                Description = "Meet Professor Oak",
                                IsCompleted = false
                            }
                        }
                    },
                    new QuestTemplate
                    {
                        Name = "Gym Challenge",
                        Description = "Challenge the local Gym Leader",
                        Type = QuestType.Main,
                        IsActive = false
                    }
                },
                NPCs = new List<NPCTemplate>
                {
                    new NPCTemplate
                    {
                        Name = "Professor Oak",
                        Description = "Pokemon Researcher",
                        Role = "Guide",
                        Personality = "Wise and helpful",
                        Location = "Pallet Town",
                        IsImportant = true
                    },
                    new NPCTemplate
                    {
                        Name = "Brock",
                        Description = "Gym Leader",
                        Role = "Gym Leader",
                        Personality = "Rock-type specialist",
                        Location = "Pewter City",
                        IsImportant = true
                    }
                },
                Locations = new List<LocationTemplate>
                {
                    new LocationTemplate
                    {
                        Name = "Pallet Town",
                        Description = "A small, peaceful town",
                        Region = "Kanto",
                        Type = LocationType.Town
                    },
                    new LocationTemplate
                    {
                        Name = "Route 1",
                        Description = "A route connecting Pallet Town and Viridian City",
                        Region = "Kanto",
                        Type = LocationType.Route
                    }
                },
                AvailableStarters = new List<string> { "Bulbasaur", "Charmander", "Squirtle" }
            };

            // Log the creation
            await _eventLog.AppendEventAsync(
                GameEvent.Create("adventure_module_created", 
                    $"Adventure module created for {playerName}",
                    new Dictionary<string, object> 
                    { 
                        ["player_name"] = playerName,
                        ["setting"] = preferredSetting,
                        ["module_id"] = adventureModule.Id
                    }),
                cancellationToken);

            _logger.LogInformation("Adventure module generated successfully for {PlayerName}", playerName);
            return adventureModule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating adventure module for {PlayerName}", playerName);
            throw;
        }
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.8,
                ["max_tokens"] = 1500
            }
        };
    }
}