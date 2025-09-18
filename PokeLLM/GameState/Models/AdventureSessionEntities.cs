using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

public class EventLog
{
    public int TurnNumber { get; set; }
    public string EventDescription { get; set; } = string.Empty;
}

public class PlayerState
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("abilities")]
    public List<string> Abilities { get; set; } = new();

    [JsonPropertyName("perks")]
    public List<string> Perks { get; set; } = new();

    [JsonPropertyName("characterDetails")]
    public CharacterDetails CharacterDetails { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("trainerClassData")]
    public TrainerClass TrainerClassData { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<string> Conditions { get; set; } = new();

    [JsonPropertyName("teamPokemon")]
    public List<OwnedPokemon> TeamPokemon { get; set; } = new();

    [JsonPropertyName("boxedPokemon")]
    public List<OwnedPokemon> BoxedPokemon { get; set; } = new();

    [JsonPropertyName("playerNpcRelationships")]
    public Dictionary<string, int> PlayerNpcRelationships { get; set; } = new();

    [JsonPropertyName("playerFactionRelationships")]
    public Dictionary<string, int> PlayerFactionRelationships { get; set; } = new();

    [JsonPropertyName("gymBadges")]
    public List<string> GymBadges { get; set; } = new();

    [JsonIgnore]
    public Stats EffectiveStats => CalculateEffectiveStats();

    private Stats CalculateEffectiveStats()
    {
        return new Stats
        {
            Strength = Stats.Strength + (TrainerClassData.StatModifiers?.GetValueOrDefault("Strength", 0) ?? 0),
            Dexterity = Stats.Dexterity + (TrainerClassData.StatModifiers?.GetValueOrDefault("Dexterity", 0) ?? 0),
            Constitution = Stats.Constitution + (TrainerClassData.StatModifiers?.GetValueOrDefault("Constitution", 0) ?? 0),
            Intelligence = Stats.Intelligence + (TrainerClassData.StatModifiers?.GetValueOrDefault("Intelligence", 0) ?? 0),
            Wisdom = Stats.Wisdom + (TrainerClassData.StatModifiers?.GetValueOrDefault("Wisdom", 0) ?? 0),
            Charisma = Stats.Charisma + (TrainerClassData.StatModifiers?.GetValueOrDefault("Charisma", 0) ?? 0),
            CurrentVigor = Stats.CurrentVigor,
            MaxVigor = Stats.MaxVigor + (TrainerClassData.StatModifiers?.GetValueOrDefault("Vigor", 0) ?? 0)
        };
    }
}

public class Npc
{
    [JsonPropertyName("id")]
    [Description("Unique, descriptive character ID, e.g., 'char_gary_oak'. Used as a key in dictionaries and for vector lookups.")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    [Description("Narrative role of the NPC within the adventure module.")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("motivation")]
    public string Motivation { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("characterDetails")]
    public CharacterDetails CharacterDetails { get; set; } = new();

    [JsonPropertyName("isTrainer")]
    public bool IsTrainer { get; set; }

    [JsonPropertyName("pokemonOwned")]
    public List<string> PokemonOwned { get; set; } = new();

    [JsonPropertyName("factions")]
    public List<string> Factions { get; set; } = new();

    [JsonPropertyName("relationships")]
    public List<AdventureModuleRelationship> Relationships { get; set; } = new();

    [JsonPropertyName("dialogueScripts")]
    public List<AdventureModuleDialogueScript> DialogueScripts { get; set; } = new();
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
    public int GlobalRenown { get; set; }

    [JsonPropertyName("globalNotoriety")]
    public int GlobalNotoriety { get; set; }
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
    public List<Move> KnownMoves { get; set; } = new();

    [JsonPropertyName("heldItem")]
    public string HeldItem { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("type1")]
    public PokemonType Type1 { get; set; } = PokemonType.Normal;

    [JsonPropertyName("type2")]
    public PokemonType Type2 { get; set; } = PokemonType.None;

    [JsonPropertyName("abilities")]
    public List<string> Abilities { get; set; } = new();

    [JsonPropertyName("statusEffects")]
    public List<string> StatusEffects { get; set; } = new();

    [JsonPropertyName("factions")]
    public List<string> Factions { get; set; } = new();

    [JsonPropertyName("locationId")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("ownerNpcId")]
    public string OwnerNpcId { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;
}


public class OwnedPokemon
{
    [JsonPropertyName("pokemon")]
    public Pokemon Pokemon { get; set; } = new();

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("caughtLocationId")]
    public string CaughtLocationId { get; set; } = string.Empty;

    [JsonPropertyName("friendship")]
    public int Friendship { get; set; } = 50;
}

public class Location
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("factionsPresent")]
    public List<string> FactionsPresent { get; set; } = new();

    [JsonPropertyName("descriptionVectorId")]
    public string DescriptionVectorId { get; set; } = string.Empty;

    [JsonPropertyName("pointsOfInterest")]
    public Dictionary<string, string> PointsOfInterest { get; set; } = new();

    [JsonPropertyName("pointsOfInterestDetails")]
    public List<AdventureModulePointOfInterest> PointsOfInterestDetails { get; set; } = new();

    [JsonPropertyName("encounters")]
    public List<AdventureModuleEncounter> Encounters { get; set; } = new();

    [JsonPropertyName("exits")]
    public Dictionary<string, string> Exits { get; set; } = new();

    [JsonPropertyName("presentNpcIds")]
    public List<string> PresentNpcIds { get; set; } = new();

    [JsonPropertyName("presentPokemonIds")]
    public List<string> PresentPokemonIds { get; set; } = new();
}


public class ItemInstance
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("effects")]
    public string Effects { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("placement")]
    public List<AdventureModuleItemPlacement> Placement { get; set; } = new();
}


public class Stats
{
    [JsonPropertyName("currentVigor")]
    public int CurrentVigor { get; set; } = 10;

    [JsonPropertyName("maxVigor")]
    public int MaxVigor { get; set; } = 10;

    [JsonPropertyName("strength")]
    public int Strength { get; set; } = 10;

    [JsonPropertyName("dexterity")]
    public int Dexterity { get; set; } = 10;

    [JsonPropertyName("constitution")]
    public int Constitution { get; set; } = 10;

    [JsonPropertyName("intelligence")]
    public int Intelligence { get; set; } = 10;

    [JsonPropertyName("wisdom")]
    public int Wisdom { get; set; } = 10;

    [JsonPropertyName("charisma")]
    public int Charisma { get; set; } = 10;
}

public class Move
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public MoveCategory Category { get; set; } = MoveCategory.Physical;

    [JsonPropertyName("damageDice")]
    public string DamageDice { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public PokemonType Type { get; set; } = PokemonType.Normal;

    [JsonPropertyName("vigorCost")]
    public int VigorCost { get; set; } = 1;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class PokemonSpeciesData
{
    [JsonPropertyName("speciesName")]
    public string SpeciesName { get; set; } = string.Empty;

    [JsonPropertyName("baseAbilityScores")]
    public Stats BaseAbilityScores { get; set; } = new();

    [JsonPropertyName("learnableMoves")]
    public List<Move> LearnableMoves { get; set; } = new();

    [JsonPropertyName("evolutionInfo")]
    public string EvolutionInfo { get; set; } = string.Empty;

    [JsonPropertyName("type1")]
    public PokemonType Type1 { get; set; } = PokemonType.Normal;

    [JsonPropertyName("type2")]
    public PokemonType Type2 { get; set; } = PokemonType.None;

    [JsonPropertyName("baseVigor")]
    public int BaseVigor { get; set; } = 10;
}

public class TrainerClass
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("statModifiers")]
    public Dictionary<string, int> StatModifiers { get; set; } = new();

    [JsonPropertyName("startingAbilities")]
    public List<string> StartingAbilities { get; set; } = new();

    [JsonPropertyName("startingPerks")]
    public List<string> StartingPerks { get; set; } = new();

    [JsonPropertyName("startingMoney")]
    public int StartingMoney { get; set; } = 1000;

    [JsonPropertyName("startingItems")]
    public List<string> StartingItems { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("levelUpTable")]
    public Dictionary<int, string> LevelUpTable { get; set; } = new();

    [JsonPropertyName("levelUpPerks")]
    public Dictionary<int, string> LevelUpPerks { get; set; } = new();
}

public class CombatState
{
    [JsonPropertyName("combatId")]
    public string CombatId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("combatants")]
    public List<Combatant> Combatants { get; set; } = new();

    [JsonPropertyName("turnOrder")]
    public List<string> TurnOrder { get; set; } = new();

    [JsonPropertyName("currentTurnIndex")]
    public int CurrentTurnIndex { get; set; }

    [JsonPropertyName("roundNumber")]
    public int RoundNumber { get; set; } = 1;

    [JsonPropertyName("combatLog")]
    public List<string> CombatLog { get; set; } = new();

    [JsonPropertyName("environmentEffects")]
    public List<string> EnvironmentEffects { get; set; } = new();
}

public class Combatant
{
    [JsonPropertyName("combatantId")]
    public string CombatantId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public string Team { get; set; } = string.Empty;

    [JsonPropertyName("initiative")]
    public int Initiative { get; set; }

    [JsonPropertyName("isDefeated")]
    public bool IsDefeated { get; set; }

    [JsonPropertyName("temporaryEffects")]
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
public enum PokemonType
{
    None,
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
    GameSetup,
    WorldGeneration,
    Exploration,
    Combat,
    LevelUp
}
