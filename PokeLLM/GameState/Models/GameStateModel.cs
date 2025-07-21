using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

public class GameStateModel
{
    [JsonPropertyName("trainer")]
    public TrainerState Trainer { get; set; } = new();

    [JsonPropertyName("pokemonTeam")]
    public PokemonTeam PokemonTeam { get; set; } = new();

    [JsonPropertyName("worldState")]
    public GameWorldState WorldState { get; set; } = new();

    [JsonPropertyName("battleState")]
    public BattleState? BattleState { get; set; } = null;
}

public class TrainerState
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("archetype")]
    public TrainerArchetype Archetype { get; set; } = TrainerArchetype.None;

    [JsonPropertyName("conditions")]
    public List<ActiveCondition> Conditions { get; set; } = new();

    [JsonPropertyName("inventory")]
    public Dictionary<string, int> Inventory { get; set; } = new();

    [JsonPropertyName("money")]
    public int Money { get; set; } = 0;

    [JsonPropertyName("globalRenown")]
    public int GlobalRenown { get; set; } = 0; // positive reputation

    [JsonPropertyName("globalNotoriety")]
    public int GlobalNotoriety { get; set; } = 0; // negative reputation
}

public class Stats
{
    [JsonPropertyName("strength")]
    public StatLevel Strength { get; set; } = StatLevel.Novice;

    [JsonPropertyName("agility")]
    public StatLevel Agility { get; set; } = StatLevel.Novice;

    [JsonPropertyName("social")]
    public StatLevel Social { get; set; } = StatLevel.Novice;

    [JsonPropertyName("intelligence")]
    public StatLevel Intelligence { get; set; } = StatLevel.Novice;
}

public class ActiveCondition
{
    [JsonPropertyName("type")]
    public TrainerCondition Type { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = -1; // -1 = permanent until removed

    [JsonPropertyName("severity")]
    public int Severity { get; set; } = 1; // For conditions with varying intensity
}

public class PokemonTeam
{
    [JsonPropertyName("activePokemon")]
    public List<Pokemon> ActivePokemon { get; set; } = new();

    [JsonPropertyName("boxedPokemon")]
    public List<Pokemon> BoxedPokemon { get; set; } = new();

    [JsonPropertyName("maxPartySize")]
    public int MaxPartySize { get; set; } = 6;
}

public class Pokemon
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("species")]
    public string Species { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("knownMoves")]
    public HashSet<string> KnownMoves { get; set; } = new();

    [JsonPropertyName("currentVigor")]
    public int CurrentVigor { get; set; }

    [JsonPropertyName("maxVigor")]
    public int MaxVigor { get; set; }

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("type1")]
    public string Type1 { get; set; } = string.Empty;

    [JsonPropertyName("type2")]
    public string Type2 { get; set; }

    [JsonPropertyName("ability")]
    public string Ability { get; set; }

    [JsonPropertyName("caughtLocation")]
    public string CaughtLocation { get; set; } = string.Empty;

    [JsonPropertyName("friendship")]
    public int Friendship { get; set; } = 50; // 0-100 scale
}

public class GameWorldState
{
    [JsonPropertyName("currentLocation")]
    public string CurrentLocation { get; set; }

    [JsonPropertyName("currentRegion")]
    public string CurrentRegion { get; set; }

    [JsonPropertyName("visitedLocations")]
    public HashSet<string> VisitedLocations { get; set; } = new();

    [JsonPropertyName("gymBadges")]
    public List<GymBadge> GymBadges { get; set; } = new();

    [JsonPropertyName("worldFlags")]
    public Dictionary<string, object> WorldFlags { get; set; } = new();

    [JsonPropertyName("npcRelationships")]
    public Dictionary<string, int> NPCRelationships { get; set; } = new();

    [JsonPropertyName("factionReputations")]
    public Dictionary<string, int> FactionReputations { get; set; } = new();

    [JsonPropertyName("discoveredLore")]
    public HashSet<string> DiscoveredLore { get; set; } = new();

    [JsonPropertyName("timeOfDay")]
    public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Morning;

    [JsonPropertyName("weatherCondition")]
    public string WeatherCondition { get; set; } = "Clear";
}

public class GymBadge
{
    [JsonPropertyName("gymName")]
    public string GymName { get; set; } = string.Empty;

    [JsonPropertyName("leaderName")]
    public string LeaderName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("badgeType")]
    public string BadgeType { get; set; } = string.Empty; // Fire, Water, etc.
}

public class BattleState
{
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = false;

    [JsonPropertyName("battleType")]
    public BattleType BattleType { get; set; } = BattleType.Wild;

    [JsonPropertyName("currentTurn")]
    public int CurrentTurn { get; set; } = 1;

    [JsonPropertyName("currentPhase")]
    public BattlePhase CurrentPhase { get; set; } = BattlePhase.SelectAction;

    [JsonPropertyName("turnOrder")]
    public List<string> TurnOrder { get; set; } = new();

    [JsonPropertyName("currentActorId")]
    public string CurrentActorId { get; set; } = string.Empty;

    [JsonPropertyName("battleParticipants")]
    public List<BattleParticipant> BattleParticipants { get; set; } = new();

    [JsonPropertyName("battleConditions")]
    public List<BattleCondition> BattleConditions { get; set; } = new();

    [JsonPropertyName("battlefield")]
    public Battlefield Battlefield { get; set; } = new();

    [JsonPropertyName("weather")]
    public BattleWeather Weather { get; set; } = new();

    [JsonPropertyName("victoryConditions")]
    public List<VictoryCondition> VictoryConditions { get; set; } = new();

    [JsonPropertyName("battleLog")]
    public List<BattleLogEntry> BattleLog { get; set; } = new();
}

public class BattleParticipant
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public ParticipantType Type { get; set; } = ParticipantType.PlayerPokemon;

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = string.Empty;

    [JsonPropertyName("pokemon")]
    public BattlePokemon? Pokemon { get; set; }

    [JsonPropertyName("trainer")]
    public BattleTrainer? Trainer { get; set; }

    [JsonPropertyName("position")]
    public BattlePosition Position { get; set; } = new();

    [JsonPropertyName("initiative")]
    public int Initiative { get; set; } = 0;

    [JsonPropertyName("hasActed")]
    public bool HasActed { get; set; } = false;

    [JsonPropertyName("isDefeated")]
    public bool IsDefeated { get; set; } = false;

    [JsonPropertyName("relationships")]
    public Dictionary<string, RelationshipType> Relationships { get; set; } = new();
}

public class BattlePokemon
{
    [JsonPropertyName("pokemonData")]
    public Pokemon PokemonData { get; set; } = new();

    [JsonPropertyName("currentVigor")]
    public int CurrentVigor { get; set; }

    [JsonPropertyName("maxVigor")]
    public int MaxVigor { get; set; }

    [JsonPropertyName("statusEffects")]
    public List<StatusEffect> StatusEffects { get; set; } = new();

    [JsonPropertyName("temporaryStats")]
    public Dictionary<string, int> TemporaryStats { get; set; } = new();

    [JsonPropertyName("usedMoves")]
    public List<string> UsedMoves { get; set; } = new();

    [JsonPropertyName("lastAction")]
    public BattleAction? LastAction { get; set; }
}

public class BattleTrainer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<ActiveCondition> Conditions { get; set; } = new();

    [JsonPropertyName("remainingPokemon")]
    public List<Pokemon> RemainingPokemon { get; set; } = new();

    [JsonPropertyName("canEscape")]
    public bool CanEscape { get; set; } = true;

    [JsonPropertyName("hasActed")]
    public bool HasActed { get; set; } = false;
}

public class BattlePosition
{
    [JsonPropertyName("x")]
    public int X { get; set; } = 0;

    [JsonPropertyName("y")]
    public int Y { get; set; } = 0;

    [JsonPropertyName("elevation")]
    public int Elevation { get; set; } = 0;

    [JsonPropertyName("terrain")]
    public string Terrain { get; set; } = "Normal";

    [JsonPropertyName("cover")]
    public CoverType Cover { get; set; } = CoverType.None;
}

public class StatusEffect
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public StatusEffectType Type { get; set; } = StatusEffectType.Debuff;

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = -1; // -1 = permanent until removed

    [JsonPropertyName("severity")]
    public int Severity { get; set; } = 1;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("effects")]
    public Dictionary<string, object> Effects { get; set; } = new();
}

public class BattleAction
{
    [JsonPropertyName("actorId")]
    public string ActorId { get; set; } = string.Empty;

    [JsonPropertyName("actionType")]
    public BattleActionType ActionType { get; set; } = BattleActionType.Move;

    [JsonPropertyName("targetIds")]
    public List<string> TargetIds { get; set; } = new();

    [JsonPropertyName("moveName")]
    public string MoveName { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    [JsonPropertyName("results")]
    public List<ActionResult> Results { get; set; } = new();
}

public class ActionResult
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("damage")]
    public int Damage { get; set; } = 0;

    [JsonPropertyName("healing")]
    public int Healing { get; set; } = 0;

    [JsonPropertyName("statusEffects")]
    public List<StatusEffect> StatusEffects { get; set; } = new();

    [JsonPropertyName("effects")]
    public Dictionary<string, object> Effects { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class BattleCondition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = -1;

    [JsonPropertyName("effects")]
    public Dictionary<string, object> Effects { get; set; } = new();
}

public class Battlefield
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 10;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 10;

    [JsonPropertyName("terrainTypes")]
    public Dictionary<string, TerrainTile> TerrainTypes { get; set; } = new();

    [JsonPropertyName("hazards")]
    public List<BattleHazard> Hazards { get; set; } = new();

    [JsonPropertyName("specialFeatures")]
    public Dictionary<string, object> SpecialFeatures { get; set; } = new();
}

public class TerrainTile
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("terrainType")]
    public string TerrainType { get; set; } = "Normal";

    [JsonPropertyName("elevation")]
    public int Elevation { get; set; } = 0;

    [JsonPropertyName("cover")]
    public CoverType Cover { get; set; } = CoverType.None;

    [JsonPropertyName("effects")]
    public Dictionary<string, object> Effects { get; set; } = new();
}

public class BattleHazard
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public BattlePosition Position { get; set; } = new();

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = -1;

    [JsonPropertyName("effects")]
    public Dictionary<string, object> Effects { get; set; } = new();

    [JsonPropertyName("affectedTypes")]
    public List<string> AffectedTypes { get; set; } = new();
}

public class BattleWeather
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Clear";

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = -1;

    [JsonPropertyName("effects")]
    public Dictionary<string, object> Effects { get; set; } = new();
}

public class VictoryCondition
{
    [JsonPropertyName("type")]
    public VictoryType Type { get; set; } = VictoryType.DefeatAllEnemies;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class BattleLogEntry
{
    [JsonPropertyName("turn")]
    public int Turn { get; set; }

    [JsonPropertyName("phase")]
    public BattlePhase Phase { get; set; }

    [JsonPropertyName("actorId")]
    public string ActorId { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("targets")]
    public List<string> Targets { get; set; } = new();

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Enums
public enum StatType
{
    Strength,
    Agility,
    Social,
    Intelligence
}

public enum StatLevel
{
    Hopeless = -2,
    Incompetent = -1,
    Novice = 0,
    Trained = 1,
    Experienced = 2,
    Expert = 3,
    Veteran = 4,
    Master = 5,
    Grandmaster = 6,
    Legendary = 7
}

public enum TrainerCondition
{
    Healthy,
    Tired,
    Injured,
    Poisoned,
    Inspired,
    Focused,
    Exhausted,
    Confident,
    Intimidated
}

public enum TrainerArchetype
{
    None,
    BugCatcher,
    Hiker,
    Psychic,
    Medium,
    AceTrainer,
    Researcher,
    Coordinator,
    Ranger
}

public enum TimeOfDay
{
    Morning,
    Afternoon,
    Evening,
    Night
}

public enum BattleType
{
    Wild,
    Trainer,
    Gym,
    Elite,
    Champion,
    Team,
    Raid,
    Tournament
}

public enum BattlePhase
{
    Initialize,
    SelectAction,
    ResolveActions,
    ApplyEffects,
    CheckVictory,
    EndTurn,
    BattleEnd
}

public enum ParticipantType
{
    PlayerPokemon,
    PlayerTrainer,
    EnemyPokemon,
    EnemyTrainer,
    NeutralPokemon,
    Environment
}

public enum RelationshipType
{
    Neutral,
    Friendly,
    Hostile,
    Allied,
    Protecting,
    Targeting
}

public enum StatusEffectType
{
    Buff,
    Debuff,
    Condition,
    Protection,
    Vulnerability
}

public enum BattleActionType
{
    Move,
    Switch,
    Item,
    Escape,
    Wait,
    TrainerAction,
    Environment
}

public enum CoverType
{
    None,
    Light,
    Heavy,
    Total
}

public enum VictoryType
{
    DefeatAllEnemies,
    DefeatSpecificTarget,
    Survival,
    Escape,
    Objective,
    Timer
}
