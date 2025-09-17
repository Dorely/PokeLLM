using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

/// <summary>
/// Represents an adventure play session that is derived from a generated adventure module.
/// </summary>
public class AdventureSessionState
{
    [JsonPropertyName("metadata")]
    public AdventureSessionMetadata Metadata { get; set; } = new();

    [JsonPropertyName("module")]
    public AdventureSessionModuleReference Module { get; set; } = new();

    [JsonPropertyName("baseline")]
    public AdventureSessionBaselineSnapshot Baseline { get; set; } = new();

    [JsonPropertyName("overlay")]
    public AdventureSessionOverlay Overlay { get; set; } = new();

    [JsonPropertyName("history")]
    public AdventureSessionHistory History { get; set; } = new();

    [JsonIgnore]
    public string SessionId => Metadata.SessionId;

    [JsonIgnore]
    public DateTime SessionStartTime => Metadata.SessionStartTime;

    [JsonIgnore]
    public DateTime LastUpdatedTime
    {
        get => Metadata.LastUpdatedTime;
        set => Metadata.LastUpdatedTime = value;
    }

    [JsonIgnore]
    public DateTime LastSaveTime
    {
        get => Metadata.LastUpdatedTime;
        set => Metadata.LastUpdatedTime = value;
    }

    [JsonIgnore]
    public int GameTurnNumber
    {
        get => Metadata.GameTurnNumber;
        set => Metadata.GameTurnNumber = value;
    }

    [JsonIgnore]
    public GamePhase CurrentPhase
    {
        get => Metadata.CurrentPhase;
        set => Metadata.CurrentPhase = value;
    }

    [JsonIgnore]
    public string CurrentContext
    {
        get => Metadata.CurrentContext;
        set => Metadata.CurrentContext = value;
    }

    [JsonIgnore]
    public string PhaseChangeSummary
    {
        get => Metadata.PhaseChangeSummary;
        set => Metadata.PhaseChangeSummary = value;
    }

    [JsonIgnore]
    public PlayerState Player
    {
        get => Baseline.Player;
        set => Baseline.Player = value;
    }

    [JsonIgnore]
    public Dictionary<string, Location> WorldLocations => Baseline.WorldLocations;

    [JsonIgnore]
    public Dictionary<string, Npc> WorldNpcs => Baseline.WorldNpcs;

    [JsonIgnore]
    public Dictionary<string, Pokemon> WorldPokemon => Baseline.WorldPokemon;

    [JsonIgnore]
    public Dictionary<string, ItemInstance> Items => Baseline.Items;

    [JsonIgnore]
    public string Region
    {
        get => Baseline.Region;
        set => Baseline.Region = value;
    }

    [JsonIgnore]
    public string CurrentLocationId
    {
        get => Baseline.CurrentLocationId;
        set => Baseline.CurrentLocationId = value;
    }

    [JsonIgnore]
    public TimeOfDay TimeOfDay
    {
        get => Baseline.TimeOfDay;
        set => Baseline.TimeOfDay = value;
    }

    [JsonIgnore]
    public Weather Weather
    {
        get => Baseline.Weather;
        set => Baseline.Weather = value;
    }

    [JsonIgnore]
    public string AdventureSummary
    {
        get => Baseline.AdventureSummary;
        set => Baseline.AdventureSummary = value;
    }

    [JsonIgnore]
    public List<EventLog> RecentEvents
    {
        get => Baseline.RecentEvents;
        set => Baseline.RecentEvents = value;
    }

    [JsonIgnore]
    public Dictionary<string, string> ActiveQuestStates => Baseline.QuestStates;

    [JsonIgnore]
    public CombatState? CombatState
    {
        get => Overlay.CombatState;
        set => Overlay.CombatState = value;
    }
}

public class AdventureSessionMetadata
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sessionStartTime")]
    public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastUpdatedTime")]
    public DateTime LastUpdatedTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("currentPhase")]
    public GamePhase CurrentPhase { get; set; } = GamePhase.GameSetup;

    [JsonPropertyName("currentContext")]
    public string CurrentContext { get; set; } = string.Empty;

    [JsonPropertyName("phaseChangeSummary")]
    public string PhaseChangeSummary { get; set; } = string.Empty;

    [JsonPropertyName("gameTurnNumber")]
    public int GameTurnNumber { get; set; }

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }
}

public class AdventureSessionModuleReference
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("moduleTitle")]
    public string ModuleTitle { get; set; } = string.Empty;

    [JsonPropertyName("moduleVersion")]
    public string ModuleVersion { get; set; } = string.Empty;

    [JsonPropertyName("moduleChecksum")]
    public string ModuleChecksum { get; set; } = string.Empty;
}

public class AdventureSessionBaselineSnapshot
{
    [JsonPropertyName("player")]
    public PlayerState Player { get; set; } = new();

    [JsonPropertyName("worldLocations")]
    public Dictionary<string, Location> WorldLocations { get; set; } = new();

    [JsonPropertyName("worldNpcs")]
    public Dictionary<string, Npc> WorldNpcs { get; set; } = new();

    [JsonPropertyName("worldPokemon")]
    public Dictionary<string, Pokemon> WorldPokemon { get; set; } = new();

    [JsonPropertyName("items")]
    public Dictionary<string, ItemInstance> Items { get; set; } = new();

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("currentLocationId")]
    public string CurrentLocationId { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Morning;

    [JsonPropertyName("weather")]
    public Weather Weather { get; set; } = Weather.Clear;

    [JsonPropertyName("adventureSummary")]
    public string AdventureSummary { get; set; } = string.Empty;

    [JsonPropertyName("recentEvents")]
    public List<EventLog> RecentEvents { get; set; } = new();

    [JsonPropertyName("questStates")]
    public Dictionary<string, string> QuestStates { get; set; } = new();
}

public class AdventureSessionOverlay
{
    [JsonPropertyName("addedLocations")]
    public Dictionary<string, Location> AddedLocations { get; set; } = new();

    [JsonPropertyName("updatedLocations")]
    public Dictionary<string, Location> UpdatedLocations { get; set; } = new();

    [JsonPropertyName("removedLocationIds")]
    public List<string> RemovedLocationIds { get; set; } = new();

    [JsonPropertyName("addedNpcs")]
    public Dictionary<string, Npc> AddedNpcs { get; set; } = new();

    [JsonPropertyName("updatedNpcs")]
    public Dictionary<string, Npc> UpdatedNpcs { get; set; } = new();

    [JsonPropertyName("removedNpcIds")]
    public List<string> RemovedNpcIds { get; set; } = new();

    [JsonPropertyName("addedPokemon")]
    public Dictionary<string, Pokemon> AddedPokemon { get; set; } = new();

    [JsonPropertyName("updatedPokemon")]
    public Dictionary<string, Pokemon> UpdatedPokemon { get; set; } = new();

    [JsonPropertyName("removedPokemonIds")]
    public List<string> RemovedPokemonIds { get; set; } = new();

    [JsonPropertyName("itemAdjustments")]
    public List<AdventureSessionItemAdjustment> ItemAdjustments { get; set; } = new();

    [JsonPropertyName("adventureNotes")]
    public List<AdventureSessionNote> AdventureNotes { get; set; } = new();

    [JsonPropertyName("questUpdates")]
    public Dictionary<string, string> QuestUpdates { get; set; } = new();

    [JsonPropertyName("combatState")]
    public CombatState? CombatState { get; set; }
}

public class AdventureSessionItemAdjustment
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("delta")]
    public int Delta { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class AdventureSessionNote
{
    [JsonPropertyName("noteId")]
    public string NoteId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;
}

public class AdventureSessionHistory
{
    [JsonPropertyName("entries")]
    public List<AdventureSessionHistoryEntry> Entries { get; set; } = new();
}

public class AdventureSessionHistoryEntry
{
    [JsonPropertyName("turnNumber")]
    public int TurnNumber { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    [JsonPropertyName("relatedIds")]
    public List<string> RelatedIds { get; set; } = new();
}
