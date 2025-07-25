using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

public class GameStateModel
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sessionStartTime")]
    public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastSaveTime")]
    public DateTime LastSaveTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("currentPhase")]
    public GamePhase CurrentPhase { get; set; } = GamePhase.GameCreation;

    [JsonPropertyName("phaseChangeSummary")]
    [Description("A summary what has taken place and why the phase is changing")]
    public string PhaseChangeSummary { get; set; } = string.Empty;

    [JsonPropertyName("previousPhaseConversationSummary")]
    [Description("A summary of the conversation from the previous phase to provide context for the new phase")]
    public string PreviousPhaseConversationSummary { get; set; } = string.Empty;

    [JsonPropertyName("player")]
    public PlayerState Player { get; set; } = new();

    [JsonPropertyName("currentLocation")]
    public string CurrentLocation { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public TimeOfDay? TimeOfDay { get; set; }

    [JsonPropertyName("weather")]
    public Weather? Weather { get; set; }

    [JsonPropertyName("worldNpcs")]
    [Description("All generated NPCs")]
    public List<Character> WorldNpcs { get; set; } = new();

    [JsonPropertyName("worldPokemon")]
    [Description("All generated pokemon, wild or otherwise that are not on the players team")]
    public List<Pokemon> WorldPokemon { get; set; } = new();

    [JsonPropertyName("adventureSummary")]
    [Description("A continuously updated summary of the adventure so far.")]
    public string AdventureSummary { get; set; } = string.Empty;
}

public class PlayerState
{
    [JsonPropertyName("character")]
    public Character Character { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("availableStatPoints")]
    [Description("1 point awarded per level up")]
    public int AvailableStatPoints { get; set; } = 1; // For character creation and future point allocation

    [JsonPropertyName("characterCreationComplete")]
    public bool CharacterCreationComplete { get; set; } = false; // Track if initial character creation is done

    [JsonPropertyName("teamPokemon")]
    [Description("caught pokemon actively on the team. Limit 6")]
    public List<OwnedPokemon> TeamPokemon { get; set; } = new();

    [JsonPropertyName("boxedPokemon")]
    [Description("caught pokemon not actively on the team.")]
    public List<OwnedPokemon> BoxedPokemon { get; set; } = new();

    [JsonPropertyName("playerRelationships")]
    [Description("list of player's individual relationships. scale is -100 to 100  where 0 is unknown by them, -100 is hated enemy, and 100 would be best friend")]
    public Dictionary<string, int> PlayerNpcRelationships { get; set; } = new();

    [JsonPropertyName("factionRelationships")]
    [Description("list of player's factions reputations. scale is -100 to 100  where 0 is unknown by them, -100 is hated enemy, and 100 would be leader of the faction")]
    public Dictionary<string, int> PlayerFactionRelationships { get; set; } = new();

    [JsonPropertyName("gymBadges")]
    public List<string> GymBadges { get; set; } = new();
}

public class Character
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<string> Conditions { get; set; } = new();

    [JsonPropertyName("inventory")]
    public Dictionary<string, int> Inventory { get; set; } = new();

    [JsonPropertyName("money")]
    public int Money { get; set; } = 0;

    [JsonPropertyName("globalRenown")]
    [Description("0-100 scale of how famous this entity is in a positive way. Points awarded when good or heroic acts are witnessed.")]
    public int GlobalRenown { get; set; } = 0; // positive reputation

    [JsonPropertyName("globalNotoriety")]
    [Description("0-100 scale of how famous this entity is in a negative way. Points awarded when evil or illegal acts are witnessed.")]
    public int GlobalNotoriety { get; set; } = 0; // negative reputation

    [JsonPropertyName("factions")]
    [Description("List of the names of factions this entity belongs to.")]
    public List<string> Factions { get; set; } = new();

    [JsonPropertyName("isTrainer")]
    public bool IsTrainer { get; set; } = false;

    [JsonPropertyName("pokemonTeam")]
    [Description("List of the Ids of the pokemon this character owns. Pokemon data stored in WorldPokemon collection")]
    public List<string> PokemonOwned { get; set; } = new();
}

public class Stats
{
    [JsonPropertyName("power")]
    public StatLevel Power { get; set; } = StatLevel.Novice;

    [JsonPropertyName("speed")]
    public StatLevel Speed { get; set; } = StatLevel.Novice;

    [JsonPropertyName("mind")]
    public StatLevel Mind { get; set; } = StatLevel.Novice;

    [JsonPropertyName("charm")]
    public StatLevel Charm { get; set; } = StatLevel.Novice;

    [JsonPropertyName("defense")]
    public StatLevel Defense { get; set; } = StatLevel.Novice;

    [JsonPropertyName("spirit")]
    public StatLevel Spirit { get; set; } = StatLevel.Novice;
}

public class OwnedPokemon
{
    [JsonPropertyName("pokemon")]
    public Pokemon Pokemon { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("availableStatPoints")]
    public int AvailableStatPoints { get; set; } = 0;

    [JsonPropertyName("caughtLocation")]
    public string CaughtLocation { get; set; } = string.Empty;

    [JsonPropertyName("friendship")]
    [Description("0 - 100 scale. 0 Is hated, 100 is loved.")]
    public int Friendship { get; set; } = 50; // 0-100 scale
}

public class Pokemon
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nickName")]
    public string NickName { get; set; } = string.Empty;

    [JsonPropertyName("species")]
    public string Species { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("knownMoves")]
    public HashSet<string> KnownMoves { get; set; } = new();

    [JsonPropertyName("currentVigor")]
    public int CurrentVigor { get; set; } = 10;

    [JsonPropertyName("maxVigor")]
    public int MaxVigor { get; set; } = 10;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("type1")]
    public PokemonType Type1 { get; set; } = PokemonType.Normal;

    [JsonPropertyName("type2")]
    public PokemonType? Type2 { get; set; }

    [JsonPropertyName("abilities")]
    public List<string> Abilities { get; set; } = new();

    [JsonPropertyName("statusEffects")]
    public List<string> StatusEffects { get; set; } = new();

    [JsonPropertyName("factions")]
    [Description("List of the names of factions this entity belongs to.")]
    public List<string> Factions { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PokemonType
{
    Normal,
    Fire,
    Water,
    Grass,
    Electric,
    Ice,
    Fighting,
    Poison,
    Ground,
    Flying,
    Psychic,
    Bug,
    Rock,
    Ghost,
    Dragon,
    Steel,
    Dark,
    Fairy
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeOfDay
{
    Dawn,
    Morning,
    Day,
    Afternoon,
    Dusk,
    Night
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Weather
{
    Clear,
    Cloudy,
    Rain,
    Storm,
    Thunderstorm,
    Snow,
    Fog,
    Sandstorm,
    Sunny,
    Overcast
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamePhase
{
    GameCreation,
    CharacterCreation,
    WorldGeneration,
    Exploration,
    Combat,
    LevelUp
}
