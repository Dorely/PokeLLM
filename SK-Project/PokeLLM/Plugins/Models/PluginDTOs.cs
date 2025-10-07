using System.ComponentModel;

namespace PokeLLM.Game.Plugins.Models;

/// <summary>
/// DTO for LoreVectorRecord used in plugin parameters to avoid Gemini API issues with Guid fields
/// </summary>
public class LoreVectorRecordDto
{
    [Description("The unique, human-readable ID for the entry (e.g. 'quest_oaks_parcel', 'lore_legend_earth_sea').")]
    public string EntryId { get; set; } = string.Empty;

    [Description("The type of the entry, used for filtering. E.g., 'Legend', 'History', 'QuestInfo'.")]
    public string EntryType { get; set; } = string.Empty;

    [Description("The title of the lore entry.")]
    public string Title { get; set; } = string.Empty;

    [Description("The full text content of the entry. This text is used to generate the vector embedding.")]
    public string Content { get; set; } = string.Empty;

    [Description("A list of searchable keywords associated with the entry (e.g., 'history', 'legendary').")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// DTO for GameRuleVectorRecord used in plugin parameters to avoid Gemini API issues with Guid fields
/// </summary>
public class GameRuleVectorRecordDto
{
    [Description("The unique, human-readable ID for the entry (e.g., 'rule_poison_effect', 'class_ace_trainer').")]
    public string EntryId { get; set; } = string.Empty;

    [Description("The type of the entry, used for filtering. E.g., 'Rule', 'Class'.")]
    public string EntryType { get; set; } = string.Empty;

    [Description("The title of the rule entry.")]
    public string Title { get; set; } = string.Empty;

    [Description("The full text content of the entry. This text is used to generate the vector embedding. For Classes this should include a 20 level levelup chart.")]
    public string Content { get; set; } = string.Empty;

    [Description("A list of searchable keywords associated with the entry (e.g., 'combat', 'status_effect').")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// DTO for EntityVectorRecord used in plugin parameters to avoid Gemini API issues with Guid fields
/// </summary>
public class EntityVectorRecordDto
{
    [Description("The unique, human-readable ID for the entity (e.g., 'char_prof_oak', 'species_pikachu'). Essential for direct lookups.")]
    public string EntityId { get; set; } = string.Empty;

    [Description("The type of the entity, used for filtering. E.g., 'Character', 'PokemonSpecies', 'Item'.")]
    public string EntityType { get; set; } = string.Empty;

    [Description("The display name of the entity (e.g., 'Professor Oak').")]
    public string Name { get; set; } = string.Empty;

    [Description("The detailed, narrative description of the entity. This text is used to generate the vector embedding.")]
    public string Description { get; set; } = string.Empty;

    [Description("A serialized JSON string containing base stats or default attributes of the entity.")]
    public string PropertiesJson { get; set; } = "{}";
}

/// <summary>
/// DTO for LocationVectorRecord used in plugin parameters to avoid Gemini API issues with Guid fields
/// </summary>
public class LocationVectorRecordDto
{
    [Description("The unique, human-readable ID for the location (e.g., 'loc_pallet_town').")]
    public string LocationId { get; set; } = string.Empty;

    [Description("The display name of the location (e.g., 'Pallet Town').")]
    public string Name { get; set; } = string.Empty;

    [Description("The evocative, narrative description of the location. This text is used to generate the vector embedding.")]
    public string Description { get; set; } = string.Empty;

    [Description("The geographical region the location belongs to (e.g., 'Kanto').")]
    public string Region { get; set; } = string.Empty;

    [Description("A list of searchable keywords associated with the location (e.g., 'town', 'starting_area').")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// DTO for NarrativeLogVectorRecord used in plugin parameters to avoid Gemini API issues with Guid fields
/// </summary>
public class NarrativeLogVectorRecordDto
{
    [Description("The session ID this event belongs to. Critical for filtering memories by playthrough.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("The turn number on which the event occurred, used for chronological sorting.")]
    public int GameTurnNumber { get; set; }

    [Description("The type of event, used for filtering. E.g., 'Dialogue', 'CombatVictory', 'QuestStart'.")]
    public string EventType { get; set; } = string.Empty;

    [Description("A concise summary of the event. This text is used to generate the vector embedding.")]
    public string EventSummary { get; set; } = string.Empty;

    [Description("An optional field containing the full dialogue or a more detailed event description.")]
    public string FullTranscript { get; set; } = string.Empty;

    [Description("A list of all entity IDs involved in the event (e.g., 'player', 'char_gary_oak'). Allows for filtering by participant.")]
    public string[] InvolvedEntities { get; set; } = Array.Empty<string>();

    [Description("The ID of the location where the event took place.")]
    public string LocationId { get; set; } = string.Empty;
}

/// <summary>
/// DTO for Npc used in plugin parameters to avoid Gemini API issues with complex object serialization
/// </summary>
public class NpcDto
{
    [Description("Unique, descriptive character ID, e.g., 'char_gary_oak' or 'char_lance'.")]
    public string Id { get; set; } = string.Empty;

    [Description("The display name of the character.")]
    public string Name { get; set; } = string.Empty;

    [Description("The trainer class of this character.")]
    public string Class { get; set; } = string.Empty;

    [Description("Whether this character is a Pokemon trainer.")]
    public bool IsTrainer { get; set; } = false;

    [Description("JSON string containing the character's stats (Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma).")]
    public string StatsJson { get; set; } = "{}";

    [Description("The amount of money this character has.")]
    public int Money { get; set; } = 500;

    [Description("List of faction IDs this character belongs to.")]
    public string[] Factions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// DTO for Pokemon used in plugin parameters to avoid Gemini API issues with complex object serialization
/// </summary>
public class PokemonDto
{
    [Description("Unique, descriptive instance ID for this specific Pokemon, e.g., 'pkmn_inst_001_pidgey'.")]
    public string Id { get; set; } = string.Empty;

    [Description("The nickname for this Pokemon (optional).")]
    public string NickName { get; set; } = string.Empty;

    [Description("The species name, e.g., 'Pikachu'.")]
    public string Species { get; set; } = string.Empty;

    [Description("The Pokemon's current level.")]
    public int Level { get; set; } = 1;

    [Description("Primary type of this Pokemon.")]
    public string Type1 { get; set; } = "Normal";

    [Description("Secondary type of this Pokemon (optional).")]
    public string Type2 { get; set; } = string.Empty;

    [Description("JSON string containing the Pokemon's stats (Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma).")]
    public string StatsJson { get; set; } = "{}";

    [Description("List of abilities this Pokemon has.")]
    public string[] Abilities { get; set; } = Array.Empty<string>();

    [Description("List of faction IDs this Pokemon belongs to.")]
    public string[] Factions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// DTO for Location used in plugin parameters to avoid Gemini API issues with complex object serialization
/// </summary>
public class LocationDto
{
    [Description("Unique, descriptive location ID, e.g., 'loc_pallet_town'.")]
    public string Id { get; set; } = string.Empty;

    [Description("The display name of the location.")]
    public string Name { get; set; } = string.Empty;

    [Description("Detailed description of the location.")]
    public string Description { get; set; } = string.Empty;

    [Description("The region this location belongs to.")]
    public string Region { get; set; } = string.Empty;

    [Description("JSON string containing points of interest in this location.")]
    public string PointsOfInterestJson { get; set; } = "{}";

    [Description("JSON string containing exits from this location to other locations.")]
    public string ExitsJson { get; set; } = "{}";
}