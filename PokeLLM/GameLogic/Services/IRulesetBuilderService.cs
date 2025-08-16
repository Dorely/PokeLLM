using System.Text.Json;

namespace PokeLLM.GameLogic.Services;

/// <summary>
/// Service for building and constructing rulesets programmatically
/// </summary>
public interface IRulesetBuilderService
{
    /// <summary>
    /// Initialize a new ruleset builder
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Load an existing ruleset for editing
    /// </summary>
    void LoadFromExisting(JsonDocument existingRuleset);
    
    /// <summary>
    /// Set basic metadata for the ruleset
    /// </summary>
    void SetMetadata(string id, string name, string description, string author, List<string> tags);
    
    /// <summary>
    /// Set game mechanics and systems
    /// </summary>
    void SetGameMechanics(string gameDescription, string victoryCondition, string progressionSystem, List<string> coreMechanics);
    
    /// <summary>
    /// Set game state schema
    /// </summary>
    void SetGameStateSchema(List<string> requiredCollections, List<string> playerFields, Dictionary<string, string> dynamicCollections);
    
    /// <summary>
    /// Set game data configuration flags
    /// </summary>
    void SetGameDataFlags(bool includeClasses, bool includeItems, bool includeAbilities);
    
    /// <summary>
    /// Generate basic function definitions based on current configuration
    /// </summary>
    void GenerateBasicFunctions();
    
    /// <summary>
    /// Generate basic prompt templates based on current configuration
    /// </summary>
    void GenerateBasicPromptTemplates();
    
    /// <summary>
    /// Build the complete ruleset JSON
    /// </summary>
    JsonDocument BuildRuleset();
    
    /// <summary>
    /// Save the current ruleset to file
    /// </summary>
    Task<string> SaveRulesetAsync();
    
    /// <summary>
    /// Get current metadata for display
    /// </summary>
    RulesetMetadata GetCurrentMetadata();
    
    /// <summary>
    /// Get current mechanics for reference
    /// </summary>
    GameMechanics GetCurrentMechanics();
}

/// <summary>
/// Metadata information for a ruleset being built
/// </summary>
public class RulesetMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Version { get; set; } = "1.0.0";
}

/// <summary>
/// Game mechanics configuration
/// </summary>
public class GameMechanics
{
    public string GameDescription { get; set; } = string.Empty;
    public string VictoryCondition { get; set; } = string.Empty;
    public string ProgressionSystem { get; set; } = string.Empty;
    public List<string> CoreMechanics { get; set; } = new();
}

/// <summary>
/// Implementation of ruleset builder service
/// </summary>
public class RulesetBuilderService : IRulesetBuilderService
{
    private RulesetMetadata _metadata = new();
    private GameMechanics _mechanics = new();
    private List<string> _requiredCollections = new();
    private List<string> _playerFields = new();
    private Dictionary<string, string> _dynamicCollections = new();
    private bool _includeClasses;
    private bool _includeItems;
    private bool _includeAbilities;
    private Dictionary<string, object> _functionDefinitions = new();
    private Dictionary<string, string> _promptTemplates = new();

    public void Initialize()
    {
        _metadata = new RulesetMetadata();
        _mechanics = new GameMechanics();
        _requiredCollections.Clear();
        _playerFields.Clear();
        _dynamicCollections.Clear();
        _functionDefinitions.Clear();
        _promptTemplates.Clear();
        _includeClasses = false;
        _includeItems = false;
        _includeAbilities = false;
    }

    public void LoadFromExisting(JsonDocument existingRuleset)
    {
        Initialize();
        
        var root = existingRuleset.RootElement;
        
        // Load metadata
        if (root.TryGetProperty("metadata", out var metadata))
        {
            _metadata.Id = metadata.GetPropertyOrDefault("id", "");
            _metadata.Name = metadata.GetPropertyOrDefault("name", "");
            _metadata.Description = metadata.GetPropertyOrDefault("description", "");
            _metadata.Version = metadata.GetPropertyOrDefault("version", "1.0.0");
            
            if (metadata.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
            {
                _metadata.Author = authors.EnumerateArray().FirstOrDefault().GetString() ?? "";
            }
            
            if (metadata.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                _metadata.Tags = tags.EnumerateArray().Select(t => t.GetString() ?? "").ToList();
            }
        }
        
        // Load game state schema
        if (root.TryGetProperty("gameStateSchema", out var schema))
        {
            if (schema.TryGetProperty("requiredCollections", out var collections) && collections.ValueKind == JsonValueKind.Array)
            {
                _requiredCollections = collections.EnumerateArray().Select(c => c.GetString() ?? "").ToList();
            }
            
            if (schema.TryGetProperty("playerFields", out var fields) && fields.ValueKind == JsonValueKind.Array)
            {
                _playerFields = fields.EnumerateArray().Select(f => f.GetString() ?? "").ToList();
            }
            
            if (schema.TryGetProperty("dynamicCollections", out var dynamic))
            {
                foreach (var prop in dynamic.EnumerateObject())
                {
                    _dynamicCollections[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
        }
        
        // Detect what data types are included
        _includeClasses = root.TryGetProperty("trainerClasses", out _) || root.TryGetProperty("characterClasses", out _);
        _includeItems = root.TryGetProperty("items", out _);
        _includeAbilities = root.TryGetProperty("abilities", out _);
        
        // Load function definitions
        if (root.TryGetProperty("functionDefinitions", out var functions))
        {
            foreach (var phase in functions.EnumerateObject())
            {
                _functionDefinitions[phase.Name] = JsonSerializer.Deserialize<object>(phase.Value.GetRawText()) ?? new object();
            }
        }
        
        // Load prompt templates
        if (root.TryGetProperty("promptTemplates", out var prompts))
        {
            foreach (var phase in prompts.EnumerateObject())
            {
                _promptTemplates[phase.Name] = phase.Value.GetString() ?? "";
            }
        }
        
        // Infer mechanics from existing data (basic inference)
        _mechanics.GameDescription = _metadata.Description;
        _mechanics.CoreMechanics = _metadata.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
    }

    public void SetMetadata(string id, string name, string description, string author, List<string> tags)
    {
        _metadata.Id = id;
        _metadata.Name = name;
        _metadata.Description = description;
        _metadata.Author = author;
        _metadata.Tags = tags;
    }

    public void SetGameMechanics(string gameDescription, string victoryCondition, string progressionSystem, List<string> coreMechanics)
    {
        _mechanics.GameDescription = gameDescription;
        _mechanics.VictoryCondition = victoryCondition;
        _mechanics.ProgressionSystem = progressionSystem;
        _mechanics.CoreMechanics = coreMechanics;
    }

    public void SetGameStateSchema(List<string> requiredCollections, List<string> playerFields, Dictionary<string, string> dynamicCollections)
    {
        _requiredCollections = requiredCollections;
        _playerFields = playerFields;
        _dynamicCollections = dynamicCollections;
    }

    public void SetGameDataFlags(bool includeClasses, bool includeItems, bool includeAbilities)
    {
        _includeClasses = includeClasses;
        _includeItems = includeItems;
        _includeAbilities = includeAbilities;
    }

    public void GenerateBasicFunctions()
    {
        // Generate basic function definitions for each phase
        _functionDefinitions["GameSetup"] = CreateGameSetupFunctions();
        _functionDefinitions["WorldGeneration"] = CreateWorldGenerationFunctions();
        _functionDefinitions["Exploration"] = CreateExplorationFunctions();
        _functionDefinitions["Combat"] = CreateCombatFunctions();
        _functionDefinitions["LevelUp"] = CreateLevelUpFunctions();
    }

    public void GenerateBasicPromptTemplates()
    {
        var gameName = _metadata.Name;
        var gameDescription = _mechanics.GameDescription;
        
        _promptTemplates["GameSetup"] = $@"You are the Game Master for {gameName}, {gameDescription}.

Guide the player through character creation and initial game setup. Be engaging and help them understand the game world and their character's place in it.

Current character info: {{character_info}}
Game state: {{game_state}}";

        _promptTemplates["WorldGeneration"] = $@"You are the Game Master for {gameName}, {gameDescription}.

Create vivid, immersive locations and interesting NPCs. Establish the setting and atmosphere that fits the game's theme.

Current location: {{current_location}}
Game state: {{game_state}}";

        _promptTemplates["Exploration"] = $@"You are the Game Master for {gameName}, {gameDescription}.

Narrate the player's adventures with engaging descriptions and meaningful choices. Respond to their actions and advance the story.

Player action: {{player_input}}
Current location: {{current_location}}
Character info: {{character_info}}
Game state: {{game_state}}";

        _promptTemplates["Combat"] = $@"You are the Game Master for {gameName}, {gameDescription}.

Manage combat encounters with exciting descriptions and tactical decisions. Handle combat mechanics according to the game rules.

Combat state: {{combat_state}}
Character info: {{character_info}}
Game state: {{game_state}}";

        _promptTemplates["LevelUp"] = $@"You are the Game Master for {gameName}, {gameDescription}.

Guide character progression and reward the player's achievements. Handle level-ups, skill improvements, and new abilities.

Character info: {{character_info}}
Available upgrades: {{available_upgrades}}
Game state: {{game_state}}";
    }

    public JsonDocument BuildRuleset()
    {
        var ruleset = new
        {
            metadata = new
            {
                id = _metadata.Id,
                name = _metadata.Name,
                version = _metadata.Version,
                description = _metadata.Description,
                authors = new[] { _metadata.Author },
                tags = _metadata.Tags.ToArray()
            },
            gameStateSchema = new
            {
                requiredCollections = _requiredCollections.ToArray(),
                playerFields = _playerFields.ToArray(),
                dynamicCollections = _dynamicCollections
            },
            functionDefinitions = _functionDefinitions,
            promptTemplates = _promptTemplates
        };

        // Convert to JsonDocument
        var json = JsonSerializer.Serialize(ruleset, new JsonSerializerOptions { WriteIndented = true });
        return JsonDocument.Parse(json);
    }

    public async Task<string> SaveRulesetAsync()
    {
        var ruleset = BuildRuleset();
        var rulesetsDirectory = "Rulesets";
        
        if (!Directory.Exists(rulesetsDirectory))
        {
            Directory.CreateDirectory(rulesetsDirectory);
        }
        
        var filename = $"custom-{_metadata.Id}.json";
        var filepath = Path.Combine(rulesetsDirectory, filename);
        
        await File.WriteAllTextAsync(filepath, ruleset.RootElement.GetRawText());
        
        return _metadata.Id;
    }

    public RulesetMetadata GetCurrentMetadata() => _metadata;

    public GameMechanics GetCurrentMechanics() => _mechanics;

    private object CreateGameSetupFunctions()
    {
        var functions = new List<object>
        {
            new
            {
                name = "create_character",
                description = "Create a new character with basic attributes",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        character_name = new { type = "string", description = "Name of the character" },
                        character_class = new { type = "string", description = "Character class or archetype" }
                    },
                    required = new[] { "character_name" }
                },
                ruleValidations = new[]
                {
                    "Character name must be unique",
                    "Character class must be valid if specified"
                }
            },
            new
            {
                name = "set_starting_location",
                description = "Set the starting location for the adventure",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        location_name = new { type = "string", description = "Name of the starting location" },
                        location_description = new { type = "string", description = "Description of the location" }
                    },
                    required = new[] { "location_name", "location_description" }
                }
            }
        };

        return functions;
    }

    private object CreateWorldGenerationFunctions()
    {
        var functions = new List<object>
        {
            new
            {
                name = "create_location",
                description = "Create a new location in the game world",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Name of the location" },
                        description = new { type = "string", description = "Detailed description" },
                        location_type = new { type = "string", description = "Type of location" }
                    },
                    required = new[] { "name", "description" }
                }
            },
            new
            {
                name = "create_npc",
                description = "Create a non-player character",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "NPC name" },
                        description = new { type = "string", description = "NPC description" },
                        role = new { type = "string", description = "NPC's role or function" },
                        location = new { type = "string", description = "Where the NPC is located" }
                    },
                    required = new[] { "name", "description", "location" }
                }
            }
        };

        return functions;
    }

    private object CreateExplorationFunctions()
    {
        var functions = new List<object>
        {
            new
            {
                name = "move_to_location",
                description = "Move the player to a different location",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        destination = new { type = "string", description = "Name of the destination location" },
                        travel_method = new { type = "string", description = "How the player travels there" }
                    },
                    required = new[] { "destination" }
                }
            },
            new
            {
                name = "interact_with_npc",
                description = "Have the player interact with an NPC",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        npc_name = new { type = "string", description = "Name of the NPC" },
                        interaction_type = new { type = "string", description = "Type of interaction" },
                        dialogue = new { type = "string", description = "What the player says" }
                    },
                    required = new[] { "npc_name", "interaction_type" }
                }
            },
            new
            {
                name = "search_area",
                description = "Search the current area for items or clues",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        search_type = new { type = "string", description = "What kind of search to perform" },
                        target = new { type = "string", description = "Specific thing to search for" }
                    }
                }
            }
        };

        return functions;
    }

    private object CreateCombatFunctions()
    {
        var functions = new List<object>
        {
            new
            {
                name = "start_combat",
                description = "Initiate a combat encounter",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        enemy_name = new { type = "string", description = "Name of the enemy" },
                        enemy_type = new { type = "string", description = "Type of enemy" },
                        combat_reason = new { type = "string", description = "Why combat started" }
                    },
                    required = new[] { "enemy_name", "enemy_type" }
                }
            },
            new
            {
                name = "player_attack",
                description = "Player performs an attack action",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        attack_type = new { type = "string", description = "Type of attack" },
                        target = new { type = "string", description = "Target of the attack" }
                    },
                    required = new[] { "attack_type", "target" }
                }
            },
            new
            {
                name = "end_combat",
                description = "End the combat encounter",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        result = new { type = "string", description = "Combat result (victory, defeat, flee)" },
                        experience_gained = new { type = "number", description = "Experience points gained" }
                    },
                    required = new[] { "result" }
                }
            }
        };

        return functions;
    }

    private object CreateLevelUpFunctions()
    {
        var functions = new List<object>
        {
            new
            {
                name = "gain_experience",
                description = "Award experience points to the player",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        amount = new { type = "number", description = "Amount of experience to award" },
                        reason = new { type = "string", description = "Why experience was awarded" }
                    },
                    required = new[] { "amount", "reason" }
                }
            },
            new
            {
                name = "level_up_character",
                description = "Level up the character",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        new_level = new { type = "number", description = "New character level" },
                        stat_increases = new { type = "object", description = "Stat increases gained" },
                        new_abilities = new { type = "array", description = "New abilities unlocked" }
                    },
                    required = new[] { "new_level" }
                }
            },
            new
            {
                name = "unlock_ability",
                description = "Unlock a new ability for the character",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        ability_name = new { type = "string", description = "Name of the ability" },
                        ability_description = new { type = "string", description = "What the ability does" }
                    },
                    required = new[] { "ability_name", "ability_description" }
                }
            }
        };

        return functions;
    }
}

/// <summary>
/// Extension methods for JsonElement
/// </summary>
public static class JsonElementExtensions
{
    public static string GetPropertyOrDefault(this JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.GetString() ?? defaultValue;
        }
        return defaultValue;
    }
}