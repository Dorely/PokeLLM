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
