using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

public class GameStateModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public Character Character { get; set; } = new();
    public Adventure Adventure { get; set; } = new();
    public GameSettings Settings { get; set; } = new();
}

public class Character
{
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public int ExperienceToNextLevel { get; set; } = 100;
    
    // Health and Status
    public int CurrentHealth { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public CharacterStatus Status { get; set; } = CharacterStatus.Healthy;
    
    // Stats
    public CharacterStats Stats { get; set; } = new();
    
    // Skills and Abilities
    public Dictionary<string, int> Skills { get; set; } = new();
    public List<string> Abilities { get; set; } = new();
    
    // Inventory and Equipment
    public Inventory Inventory { get; set; } = new();
    public Equipment Equipment { get; set; } = new();
    
    // Pokemon Team
    public List<Pokemon> PokemonTeam { get; set; } = new();
    public List<Pokemon> StoredPokemon { get; set; } = new();
    
    // Progression
    public List<string> BadgesEarned { get; set; } = new();
    public List<string> AchievementsUnlocked { get; set; } = new();
}

public class CharacterStats
{
    public int Strength { get; set; } = 10;
    public int Intelligence { get; set; } = 10;
    public int Charisma { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Constitution { get; set; } = 10;
    public int Wisdom { get; set; } = 10;
    public int Luck { get; set; } = 10;
}

public class Inventory
{
    public int Money { get; set; } = 1000;
    public Dictionary<string, int> Items { get; set; } = new();
    public Dictionary<string, int> KeyItems { get; set; } = new();
    public Dictionary<string, int> Pokeballs { get; set; } = new()
    {
        ["Pokeball"] = 5,
        ["Great Ball"] = 0,
        ["Ultra Ball"] = 0
    };
    public Dictionary<string, int> Medicine { get; set; } = new()
    {
        ["Potion"] = 3
    };
    public Dictionary<string, int> TMsHMs { get; set; } = new();
    public Dictionary<string, int> Berries { get; set; } = new();
}

public class Equipment
{
    public string? Weapon { get; set; }
    public string? Armor { get; set; }
    public string? Accessory { get; set; }
    public Dictionary<string, string> SpecialEquipment { get; set; } = new();
}

public class Pokemon
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Species { get; set; } = "";
    public string Nickname { get; set; } = "";
    
    // Core Stats
    public int Level { get; set; } = 5;
    public int Experience { get; set; } = 0;
    public int ExperienceToNextLevel { get; set; } = 100;
    
    // Health
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public PokemonStatus Status { get; set; } = PokemonStatus.Healthy;
    
    // Types
    public string PrimaryType { get; set; } = "";
    public string? SecondaryType { get; set; }
    
    // Battle Stats
    public PokemonStats Stats { get; set; } = new();
    public PokemonStats IVs { get; set; } = new(); // Individual Values
    public PokemonStats EVs { get; set; } = new(); // Effort Values
    
    // Moves and Abilities
    public List<PokemonMove> Moves { get; set; } = new();
    public List<PokemonMove> LearnableMoves { get; set; } = new();
    public string Ability { get; set; } = "";
    public string? HiddenAbility { get; set; }
    
    // Other Properties
    public string Nature { get; set; } = "";
    public string Gender { get; set; } = "";
    public bool IsShiny { get; set; } = false;
    public string? HeldItem { get; set; }
    public int Happiness { get; set; } = 70;
    public string OriginalTrainer { get; set; } = "";
    public DateTime CaughtDate { get; set; } = DateTime.UtcNow;
    public string CaughtLocation { get; set; } = "";
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}

public class PokemonStats
{
    public int Health { get; set; } = 0;
    public int Attack { get; set; } = 0;
    public int Defense { get; set; } = 0;
    public int SpecialAttack { get; set; } = 0;
    public int SpecialDefense { get; set; } = 0;
    public int Speed { get; set; } = 0;
}

public class PokemonMove
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Category { get; set; } = ""; // Physical, Special, Status
    public int Power { get; set; } = 0;
    public int Accuracy { get; set; } = 100;
    public int CurrentPP { get; set; } = 0;
    public int MaxPP { get; set; } = 0;
    public string Description { get; set; } = "";
    public Dictionary<string, object> Effects { get; set; } = new();
}

public class Adventure
{
    // Location and World State
    public string CurrentLocation { get; set; } = "";
    public string CurrentRegion { get; set; } = "";
    public Dictionary<string, bool> VisitedLocations { get; set; } = new();
    public Dictionary<string, DateTime> LocationVisitTimes { get; set; } = new();
    
    // NPC Relationships and Encounters
    public Dictionary<string, NPCRelationship> NPCRelationships { get; set; } = new();
    public Dictionary<string, DateTime> NPCLastEncountered { get; set; } = new();
    
    // Story and Quest Progress
    public Dictionary<string, QuestProgress> ActiveQuests { get; set; } = new();
    public Dictionary<string, QuestProgress> CompletedQuests { get; set; } = new();
    public List<string> StoryFlags { get; set; } = new();
    public Dictionary<string, object> StoryVariables { get; set; } = new();
    
    // World State
    public Dictionary<string, bool> WorldFlags { get; set; } = new();
    public Dictionary<string, object> WorldVariables { get; set; } = new();
    public List<string> UnlockedAreas { get; set; } = new();
    
    // Time and Events
    public DateTime GameTime { get; set; } = DateTime.UtcNow;
    public List<ScheduledEvent> ScheduledEvents { get; set; } = new();
    public List<GameEvent> EventHistory { get; set; } = new();
}

public class NPCRelationship
{
    public string NPCId { get; set; } = "";
    public string NPCName { get; set; } = "";
    public int RelationshipLevel { get; set; } = 0; // -100 to 100
    public string RelationshipType { get; set; } = "Neutral"; // Enemy, Hostile, Neutral, Friendly, Ally
    public Dictionary<string, object> RelationshipFlags { get; set; } = new();
    public List<string> DialogueHistory { get; set; } = new();
    public DateTime FirstMet { get; set; }
    public DateTime LastInteraction { get; set; }
    public int TimesEncountered { get; set; } = 0;
}

public class QuestProgress
{
    public string QuestId { get; set; } = "";
    public string QuestName { get; set; } = "";
    public string Description { get; set; } = "";
    public QuestStatus Status { get; set; } = QuestStatus.NotStarted;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> QuestVariables { get; set; } = new();
    public List<string> CompletedObjectives { get; set; } = new();
    public List<string> FailedObjectives { get; set; } = new();
    public Dictionary<string, object> Rewards { get; set; } = new();
}

public class ScheduledEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime ScheduledTime { get; set; }
    public string EventType { get; set; } = "";
    public Dictionary<string, object> EventData { get; set; } = new();
    public bool IsRepeating { get; set; } = false;
    public TimeSpan? RepeatInterval { get; set; }
}

public class GameEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public Dictionary<string, object> EventData { get; set; } = new();
}

public class GameSettings
{
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Normal;
    public bool AutoSave { get; set; } = true;
    public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(5);
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

// Enums
public enum CharacterStatus
{
    Healthy,
    Poisoned,
    Paralyzed,
    Sleeping,
    Frozen,
    Burned,
    Confused,
    Dead
}

public enum PokemonStatus
{
    Healthy,
    Poisoned,
    BadlyPoisoned,
    Paralyzed,
    Sleeping,
    Frozen,
    Burned,
    Confused,
    Fainted
}

public enum QuestStatus
{
    NotStarted,
    Available,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public enum DifficultyLevel
{
    Easy,
    Normal,
    Hard,
    Expert
}