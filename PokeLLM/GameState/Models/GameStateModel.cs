using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

/// <summary>
/// The root object for the entire game state. This serves as the "live sheet" of what's happening in the world right now.
/// It is designed to be serialized and provided to an LLM as grounding context.
/// </summary>
public class GameStateModel
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("gameId")]
    [Description("Unique identifier for this game instance, used for organizing save files")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("gameTurnNumber")]
    public int GameTurnNumber { get; set; } = 0;

    [JsonPropertyName("sessionStartTime")]
    public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastSaveTime")]
    public DateTime LastSaveTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("player")]
    [Description("Basic player information - extended data stored in RulesetGameData")]
    public BasicPlayerState Player { get; set; } = new();

    [JsonPropertyName("currentLocationId")]
    [Description("The ID of the player's current location. Used to look up the location in WorldLocations.")]
    public string CurrentLocationId { get; set; } = string.Empty;

    [JsonPropertyName("worldLocations")]
    [Description("All loaded locations in the world, keyed by their unique Location ID. Location structure defined by active ruleset.")]
    public Dictionary<string, object> WorldLocations { get; set; } = new();

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Morning;

    [JsonPropertyName("weather")]
    public Weather Weather { get; set; } = Weather.Clear;

    [JsonPropertyName("worldEntities")]
    [Description("All game entities (NPCs, creatures, items, etc.), keyed by their unique Entity ID. Entity types defined by active ruleset.")]
    public Dictionary<string, object> WorldEntities { get; set; } = new();

    [JsonPropertyName("adventureSummary")]
    [Description("A continuously updated high-level summary of the adventure so far.")]
    public string AdventureSummary { get; set; } = string.Empty;

    [JsonPropertyName("recentEvents")]
    [Description("A short log of the most recent significant actions and dialogues to maintain short-term context for the LLM.")]
    public List<EventLog> RecentEvents { get; set; } = new();

    [JsonPropertyName("currentPhase")]
    public GamePhase CurrentPhase { get; set; } = GamePhase.GameSetup;

    [JsonPropertyName("phaseChangeSummary")]
    [Description("A details report of what has occurred and why the phase is changing. To be passed to the next chat handler.")]
    public string PhaseChangeSummary { get; set; } = string.Empty;

    [JsonPropertyName("combatState")]
    [Description("The state of the current combat encounter. This is null when not in combat. Combat structure defined by active ruleset.")]
    public object CombatState { get; set; }

    [JsonPropertyName("currentContext")]
    [Description("Rich contextual description of the current scene, environment, and situation for storytelling continuity.")]
    public string CurrentContext { get; set; } = "";

    [JsonPropertyName("activeRulesetId")]
    [Description("The ID of the currently active ruleset that defines game mechanics and available functions.")]
    public string ActiveRulesetId { get; set; } = string.Empty;

    [JsonPropertyName("rulesetGameData")]
    [Description("Dynamic game data specific to the active ruleset. Contents vary based on the ruleset's gameStateSchema.")]
    public Dictionary<string, JsonElement> RulesetGameData { get; set; } = new();
}

public class EventLog
{
    public int TurnNumber { get; set; }
    public string EventDescription {  get; set; }
}

/// <summary>
/// Basic player state with minimal hardcoded fields. Extended player data stored in RulesetGameData.
/// </summary>
public class BasicPlayerState
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("relationships")]
    [Description("Relationships with NPCs and factions, keyed by ID. Scale is -100 to 100.")]
    public Dictionary<string, int> Relationships { get; set; } = new();

    [JsonPropertyName("conditions")]
    [Description("Player conditions and status effects")]
    public List<string> Conditions { get; set; } = new();
}

/// <summary>
/// Generic item instance for inventories
/// </summary>
public class ItemInstance
{
    [JsonPropertyName("itemId")]
    [Description("Unique, descriptive item ID, e.g., 'item_potion'. Used for vector DB lookups.")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}



[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeOfDay { Dawn, Morning, Day, Afternoon, Dusk, Night }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Weather { Clear, Cloudy, Rain, Storm, Thunderstorm, Snow, Fog, Sandstorm, Sunny, Overcast }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamePhase 
{ 
    GameSetup, 
    WorldGeneration, 
    Exploration, 
    Combat, 
    LevelUp 
}