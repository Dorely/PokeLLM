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

    [JsonPropertyName("gameTurnNumber")]
    public int GameTurnNumber { get; set; } = 0;

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
    public Dictionary<string, CharacterDetails> WorldNpcs { get; set; } = new();

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
    [Description("A details report of what has occurred and why the phase is changing. To be passed to the next chat handler.")]
    public string PhaseChangeSummary { get; set; } = string.Empty;

    [JsonPropertyName("combatState")]
    [Description("The state of the current combat encounter. This is null when not in combat.")]
    public CombatState CombatState { get; set; }
}

public class PlayerState
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("abilities")]
    public List<string> Abilities { get; set; } = new();

    [JsonPropertyName("characterDetails")]
    public CharacterDetails CharacterDetails { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; } = 0;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<string> Conditions { get; set; } = new();

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

public class Npc
{
    [JsonPropertyName("id")]
    [Description("Unique, descriptive character ID, e.g., 'char_gary_oak' or 'char_lance'. Used as a key in dictionaries and for vector DB lookups.")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("characterDetails")]
    public CharacterDetails CharacterDetails { get; set; } = new();

    [JsonPropertyName("isTrainer")]
    public bool IsTrainer { get; set; } = false;

    [JsonPropertyName("pokemonOwned")]
    [Description("List of the unique instance IDs of the pokemon this character owns. Pokemon data is stored in the WorldPokemon collection.")]
    public List<string> PokemonOwned { get; set; } = new();

    [JsonPropertyName("factions")]
    [Description("List of the Faction IDs this entity belongs to.")]
    public List<string> Factions { get; set; } = new();
}

public class CharacterDetails
{
    [JsonPropertyName("class")]
    public string Class { get; set; } = string.Empty;

    [JsonPropertyName("inventory")]
    public List<ItemInstance> Inventory { get; set; } = new();

    [JsonPropertyName("money")]
    public int Money { get; set; } = 500;

    [JsonPropertyName("globalRenown")]
    [Description("0-100 scale of how famous this character is in a positive way.")]
    public int GlobalRenown { get; set; } = 0;

    [JsonPropertyName("globalNotoriety")]
    [Description("0-100 scale of how famous this character is in a negative way.")]
    public int GlobalNotoriety { get; set; } = 0;
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
    [Description("List of moves this Pokemon knows. Up to 4 moves can be known at once.")]
    public List<Move> KnownMoves { get; set; } = new();

    [JsonPropertyName("heldItem")]
    [Description("The item this pokemon is holding")]
    public string HeldItem { get; set; } = string.Empty;

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

/// <summary>
/// D&D 5e-style ability scores for characters and Pokemon.
/// Each score typically ranges from 3-20, with 10-11 being average for humans.
/// </summary>
public class Stats
{

    [JsonPropertyName("currentVigor")]
    public int CurrentVigor { get; set; } = 10;

    [JsonPropertyName("maxVigor")]
    public int MaxVigor { get; set; } = 10;

    [JsonPropertyName("strength")]
    [Description("Physical power and raw muscle. Affects melee attack damage and athletic feats.")]
    public int Strength { get; set; } = 10;

    [JsonPropertyName("dexterity")]
    [Description("Agility, reflexes, and hand-eye coordination. Affects AC, initiative, and ranged attacks.")]
    public int Dexterity { get; set; } = 10;

    [JsonPropertyName("constitution")]
    [Description("Health, stamina, and vital force. Affects hit points and endurance.")]
    public int Constitution { get; set; } = 10;

    [JsonPropertyName("intelligence")]
    [Description("Reasoning ability, memory, and analytical thinking. Affects special attack accuracy and damage.")]
    public int Intelligence { get; set; } = 10;

    [JsonPropertyName("wisdom")]
    [Description("Awareness, intuition, and insight. Affects perception and saving throws.")]
    public int Wisdom { get; set; } = 10;

    [JsonPropertyName("charisma")]
    [Description("Force of personality, leadership, and social ability. Affects Pokemon capture and friendship.")]
    public int Charisma { get; set; } = 10;
}

/// <summary>
/// Represents a Pokemon move with complete mechanical data for D&D 5e-style combat.
/// </summary>
public class Move
{
    [JsonPropertyName("id")]
    [Description("Unique move identifier, e.g., 'move_tackle' or 'move_thunderbolt'.")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [Description("Display name of the move, e.g., 'Tackle' or 'Thunderbolt'.")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [Description("The type of move: Physical (uses Strength), Special (uses Intelligence), or Status (no damage).")]
    public MoveCategory Category { get; set; } = MoveCategory.Physical;

    [JsonPropertyName("damageDice")]
    [Description("Damage dice notation for the move, e.g., '1d6', '2d10', '1d4+3', or empty for status moves.")]
    public string DamageDice { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    [Description("The Pokemon type of this move for type effectiveness calculations.")]
    public PokemonType Type { get; set; } = PokemonType.Normal;

    [JsonPropertyName("vigorCost")]
    [Description("Amount of Vigor (energy) this move costs to use. Typical range 1-5")]
    public int VigorCost { get; set; } = 1;

    [JsonPropertyName("description")]
    [Description("Narrative description of the move's effects and appearance.")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Blueprint data for Pokemon species, defining their base characteristics.
/// Used as templates when creating new Pokemon instances.
/// </summary>
public class PokemonSpeciesData
{
    [JsonPropertyName("speciesName")]
    [Description("The name of the Pokemon species, e.g., 'Pikachu'.")]
    public string SpeciesName { get; set; } = string.Empty;

    [JsonPropertyName("baseAbilityScores")]
    [Description("Base ability scores that all Pokemon of this species start with.")]
    public Stats BaseAbilityScores { get; set; } = new();

    [JsonPropertyName("learnableMoves")]
    [Description("List of moves that Pokemon of this species can learn through leveling or training.")]
    public List<Move> LearnableMoves { get; set; } = new();

    [JsonPropertyName("evolutionInfo")]
    [Description("A description of what the pokemon evolves into and it's requirements")]
    public string EvolutionInfo { get; set; } = string.Empty;

    [JsonPropertyName("type1")]
    [Description("Primary type of this Pokemon species.")]
    public PokemonType Type1 { get; set; } = PokemonType.Normal;

    [JsonPropertyName("type2")]
    [Description("Secondary type of this Pokemon species, if any.")]
    public PokemonType? Type2 { get; set; }

    [JsonPropertyName("baseVigor")]
    [Description("Base vigor (health/energy) for this species.")]
    public int BaseVigor { get; set; } = 10;
}

/// <summary>
/// Blueprint data for trainer classes, defining their progression and abilities.
/// </summary>
public class TrainerClassData
{
    [JsonPropertyName("className")]
    [Description("The name of the trainer class, e.g., 'Researcher', 'Athlete', 'Coordinator'.")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("levelUpTable")]
    [Description("Features gained at each level for this class.")]
    public Dictionary<int, string> LevelUpTable { get; set; } = new();

    [JsonPropertyName("description")]
    [Description("Description of the trainer class and its role.")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Contains all information about an active combat encounter.
/// </summary>
public class CombatState
{
    [JsonPropertyName("combatId")]
    [Description("A unique identifier for this combat encounter.")]
    public string CombatId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("combatants")]
    [Description("A list of all participants in the combat.")]
    public List<Combatant> Combatants { get; set; } = new();

    [JsonPropertyName("turnOrder")]
    [Description("The initiative order of combatants, from highest to lowest initiative.")]
    public List<string> TurnOrder { get; set; } = new();

    [JsonPropertyName("currentTurnIndex")]
    [Description("The index in the TurnOrder list of the combatant whose turn it is.")]
    public int CurrentTurnIndex { get; set; } = 0;

    [JsonPropertyName("roundNumber")]
    [Description("The current round number of the combat.")]
    public int RoundNumber { get; set; } = 1;

    [JsonPropertyName("combatLog")]
    [Description("A log of significant events that have occurred during the combat.")]
    public List<string> CombatLog { get; set; } = new();

    [JsonPropertyName("environmentEffects")]
    [Description("Any active environmental effects that might affect the combat (e.g., 'Rainy', 'Tall Grass').")]
    public List<string> EnvironmentEffects { get; set; } = new();
}

/// <summary>
/// Represents a single participant in a combat encounter.
/// </summary>
public class Combatant
{
    [JsonPropertyName("combatantId")]
    [Description("A unique ID for this combatant in this specific combat, typically the Pokemon or NPC instance ID.")]
    public string CombatantId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    [Description("The name to display for this combatant (e.g., 'Pikachu' or 'Team Rocket Grunt').")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    [Description("The team this combatant belongs to, e.g., 'Player' or 'Opponent'.")]
    public string Team { get; set; } = string.Empty;

    [JsonPropertyName("initiative")]
    [Description("The combatant's initiative roll, determining their place in the turn order.")]
    public int Initiative { get; set; } = 0;

    [JsonPropertyName("isDefeated")]
    [Description("Whether this combatant has been defeated.")]
    public bool IsDefeated { get; set; } = false;

    [JsonPropertyName("temporaryEffects")]
    [Description("A list of temporary effects on this combatant, with their duration in rounds.")]
    public Dictionary<string, int> TemporaryEffects { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MoveCategory 
{ 
    [Description("Physical moves use Strength for attack rolls and damage.")]
    Physical, 
    
    [Description("Special moves use Intelligence for attack rolls and damage.")]
    Special, 
    
    [Description("Status moves don't deal damage but apply effects.")]
    Status 
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PokemonType { Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Steel, Dark, Fairy }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeOfDay { Dawn, Morning, Day, Afternoon, Dusk, Night }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Weather { Clear, Cloudy, Rain, Storm, Thunderstorm, Snow, Fog, Sandstorm, Sunny, Overcast }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamePhase { GameCreation, CharacterCreation, WorldGeneration, Exploration, Combat, LevelUp }