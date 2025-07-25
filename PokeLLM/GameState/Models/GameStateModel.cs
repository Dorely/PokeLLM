using System.ComponentModel;
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

    [JsonPropertyName("sessionStartTime")]
    public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastSaveTime")]
    public DateTime LastSaveTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("player")]
    public PlayerState Player { get; set; } = new();

    [JsonPropertyName("currentLocationId")]
    [Description("The ID of the player's current location. Used to look up the location in WorldLocations.")]
    public string CurrentLocationId { get; set; } = string.Empty;

    [JsonPropertyName("worldLocations")]
    [Description("All loaded locations in the world, keyed by their unique Location ID.")]
    public Dictionary<string, Location> WorldLocations { get; set; } = new();

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public TimeOfDay? TimeOfDay { get; set; }

    [JsonPropertyName("weather")]
    public Weather? Weather { get; set; }

    [JsonPropertyName("worldNpcs")]
    [Description("All generated NPCs, keyed by their unique Character ID.")]
    public Dictionary<string, Character> WorldNpcs { get; set; } = new();

    [JsonPropertyName("worldPokemon")]
    [Description("All generated pokemon (wild or otherwise not on the player's team), keyed by their unique Pokemon Instance ID.")]
    public Dictionary<string, Pokemon> WorldPokemon { get; set; } = new();

    [JsonPropertyName("adventureSummary")]
    [Description("A continuously updated high-level summary of the adventure so far.")]
    public string AdventureSummary { get; set; } = string.Empty;

    [JsonPropertyName("recentEvents")]
    [Description("A short log of the most recent significant actions and dialogues to maintain short-term context for the LLM.")]
    public List<string> RecentEvents { get; set; } = new();

    [JsonPropertyName("currentPhase")]
    public GamePhase CurrentPhase { get; set; } = GamePhase.GameCreation;

    [JsonPropertyName("phaseChangeSummary")]
    [Description("A summary what has taken place and why the phase is changing.")]
    public string PhaseChangeSummary { get; set; } = string.Empty;

    [JsonPropertyName("previousPhaseConversationSummary")]
    [Description("A summary of the conversation from the previous phase to provide context for the new phase.")]
    public string PreviousPhaseConversationSummary { get; set; } = string.Empty;
}

/// <summary>
/// Structured object containing all necessary context for the main game chat to properly orchestrate the game.
/// This is returned by the Context Gathering Subroutine.
/// </summary>
public class GameContext
{
    [JsonPropertyName("relevantEntities")]
    [Description("Characters, Pokémon, locations, and items that are relevant to the current player input.")]
    public Dictionary<string, object> RelevantEntities { get; set; } = new();

    [JsonPropertyName("missingEntities")]
    [Description("List of entities that were referenced but don't exist in game state or vector store.")]
    public List<string> MissingEntities { get; set; } = new();

    [JsonPropertyName("gameStateUpdates")]
    [Description("Any updates that were made to the game state during context gathering.")]
    public List<string> GameStateUpdates { get; set; } = new();

    [JsonPropertyName("vectorStoreData")]
    [Description("Relevant lore, descriptions, and background information retrieved from the vector store.")]
    public List<VectorStoreResult> VectorStoreData { get; set; } = new();

    [JsonPropertyName("contextSummary")]
    [Description("A high-level summary of the gathered context and its relevance to the player's input.")]
    public string ContextSummary { get; set; } = string.Empty;

    [JsonPropertyName("recommendedActions")]
    [Description("Suggested actions or considerations for the main game chat based on the gathered context.")]
    public List<string> RecommendedActions { get; set; } = new();
}

public class VectorStoreResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public float RelevanceScore { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class PlayerState
{
    [JsonPropertyName("character")]
    public Character Character { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("availableStatPoints")]
    [Description("1 point awarded per level up.")]
    public int AvailableStatPoints { get; set; } = 1;

    [JsonPropertyName("characterCreationComplete")]
    public bool CharacterCreationComplete { get; set; } = false;

    [JsonPropertyName("teamPokemon")]
    [Description("Caught pokemon actively on the team. Limit 6.")]
    public List<OwnedPokemon> TeamPokemon { get; set; } = new();

    [JsonPropertyName("boxedPokemon")]
    [Description("Caught pokemon not actively on the team.")]
    public List<OwnedPokemon> BoxedPokemon { get; set; } = new();

    [JsonPropertyName("playerNpcRelationships")]
    [Description("List of player's individual relationships, keyed by Character ID (e.g., 'char_prof_oak'). Scale is -100 to 100.")]
    public Dictionary<string, int> PlayerNpcRelationships { get; set; } = new();

    [JsonPropertyName("factionRelationships")]
    [Description("List of player's factions reputations, keyed by Faction ID (e.g., 'faction_team_rocket'). Scale is -100 to 100.")]
    public Dictionary<string, int> PlayerFactionRelationships { get; set; } = new();

    [JsonPropertyName("gymBadges")]
    public List<string> GymBadges { get; set; } = new();
}

public class Character
{
    [JsonPropertyName("id")]
    [Description("Unique, descriptive character ID, e.g., 'player' or 'char_gary_oak'. Used as a key in dictionaries and for vector DB lookups.")]
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
    public List<ItemInstance> Inventory { get; set; } = new();

    [JsonPropertyName("money")]
    public int Money { get; set; } = 3000;

    [JsonPropertyName("globalRenown")]
    [Description("0-100 scale of how famous this entity is in a positive way.")]
    public int GlobalRenown { get; set; } = 0;

    [JsonPropertyName("globalNotoriety")]
    [Description("0-100 scale of how famous this entity is in a negative way.")]
    public int GlobalNotoriety { get; set; } = 0;

    [JsonPropertyName("factions")]
    [Description("List of the Faction IDs this entity belongs to.")]
    public List<string> Factions { get; set; } = new();

    [JsonPropertyName("isTrainer")]
    public bool IsTrainer { get; set; } = false;

    [JsonPropertyName("pokemonOwned")]
    [Description("List of the unique instance IDs of the pokemon this character owns. Pokemon data is stored in the WorldPokemon collection.")]
    public List<string> PokemonOwned { get; set; } = new();
}

public class Pokemon
{
    [JsonPropertyName("id")]
    [Description("Unique, descriptive instance ID for this specific Pokemon, e.g., 'pkmn_inst_001_pidgey'.")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nickName")]
    public string NickName { get; set; } = string.Empty;

    [JsonPropertyName("species")]
    [Description("The species name, e.g., 'Pikachu'. Can be used to look up canonical species data in a vector DB.")]
    public string Species { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("knownMoves")]
    [Description("List of move names. Consider upgrading to a Move object for more complex mechanics.")]
    public List<string> KnownMoves { get; set; } = new();

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
    [Description("List of the Faction IDs this entity belongs to.")]
    public List<string> Factions { get; set; } = new();
}

public class OwnedPokemon
{
    [JsonPropertyName("pokemon")]
    public Pokemon Pokemon { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("availableStatPoints")]
    public int AvailableStatPoints { get; set; } = 0;

    [JsonPropertyName("caughtLocationId")]
    [Description("The ID of the location where the pokemon was caught.")]
    public string CaughtLocationId { get; set; } = string.Empty;

    [JsonPropertyName("friendship")]
    [Description("0 - 100 scale. 0 is hated, 100 is loved.")]
    public int Friendship { get; set; } = 50;
}

public class Location
{
    [JsonPropertyName("id")]
    [Description("Unique, descriptive location ID, e.g., 'loc_pallet_town'.")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("descriptionVectorId")]
    [Description("The key to look up the detailed, narrative description in the vector DB.")]
    public string DescriptionVectorId { get; set; } = string.Empty;

    [JsonPropertyName("pointsOfInterest")]
    [Description("Key locations or objects within this location, with their own IDs and descriptions.")]
    public Dictionary<string, string> PointsOfInterest { get; set; } = new();

    [JsonPropertyName("exits")]
    [Description("Connections to other locations, keyed by direction, value is the destination Location ID.")]
    public Dictionary<string, string> Exits { get; set; } = new();

    [JsonPropertyName("presentNpcIds")]
    [Description("List of Character IDs for NPCs currently at this location.")]
    public List<string> PresentNpcIds { get; set; } = new();

    [JsonPropertyName("presentPokemonIds")]
    [Description("List of Pokemon Instance IDs for wild Pokemon currently at this location.")]
    public List<string> PresentPokemonIds { get; set; } = new();
}

public class ItemInstance
{
    [JsonPropertyName("itemId")]
    [Description("Unique, descriptive item ID, e.g., 'item_potion'. Used for vector DB lookups.")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatLevel { Hopeless = -2, Incompetent = -1, Novice = 0, Trained = 1, Experienced = 2, Expert = 3, Veteran = 4, Master = 5, Grandmaster = 6, Legendary = 7 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PokemonType { Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Steel, Dark, Fairy }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeOfDay { Dawn, Morning, Day, Afternoon, Dusk, Night }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Weather { Clear, Cloudy, Rain, Storm, Thunderstorm, Snow, Fog, Sandstorm, Sunny, Overcast }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamePhase { GameCreation, CharacterCreation, WorldGeneration, Exploration, Combat, LevelUp }