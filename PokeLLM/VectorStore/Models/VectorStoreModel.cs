using Microsoft.Extensions.VectorData;
using System.ComponentModel;

namespace PokeLLM.Game.VectorStore.Models;

public abstract class VectorRecordBase
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string Embedding { get; set; } = string.Empty;

}

/// <summary>
/// Represents a canonical record for an entity (Character, Pokemon Species, or Item)
/// in the vector store.
/// </summary>
public class EntityVectorRecord : VectorRecordBase
{

    [VectorStoreData(IsIndexed = true)]
    [Description("The unique, human-readable ID for the entity (e.g., 'char_prof_oak', 'species_pikachu'). Essential for direct lookups.")]
    public string EntityId { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("The type of the entity, used for filtering. E.g., 'Character', 'PokemonSpecies', 'Item'.")]
    public string EntityType { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("The display name of the entity (e.g., 'Professor Oak').")]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("The detailed, narrative description of the entity. This text is used to generate the vector embedding.")]
    public string Description { get; set; } = string.Empty;

    [VectorStoreData]
    [Description("A serialized JSON string containing base stats or default attributes of the entity.")]
    public string PropertiesJson { get; set; } = "{}";
}

/// <summary>
/// Represents a canonical record for a location in the game world
/// in the vector store.
/// </summary>
public class LocationVectorRecord : VectorRecordBase
{
    [VectorStoreData(IsIndexed = true)]
    [Description("The unique, human-readable ID for the location (e.g., 'loc_pallet_town').")]
    public string LocationId { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("The display name of the location (e.g., 'Pallet Town').")]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("The evocative, narrative description of the location. This text is used to generate the vector embedding.")]
    public string Description { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("The geographical region the location belongs to (e.g., 'Kanto').")]
    public string Region { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("A list of searchable keywords associated with the location (e.g., 'town', 'starting_area').")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Represents a record for a piece of lore, a game rule, or a quest description
/// in the vector store.
/// </summary>
public class LoreVectorRecord : VectorRecordBase
{

    [VectorStoreData(IsIndexed = true)]
    [Description("The unique, human-readable ID for the entry (e.g., 'rule_poison_effect', 'quest_oaks_parcel').")]
    public string EntryId { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("The type of the entry, used for filtering. E.g., 'Rule', 'History', 'QuestInfo'.")]
    public string EntryType { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("The title of the lore or rule entry.")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("The full text content of the entry. This text is used to generate the vector embedding.")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("A list of searchable keywords associated with the entry (e.g., 'combat', 'status_effect').")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Represents a record of a significant gameplay event (a 'memory node')
/// in the vector store.
/// </summary>
public class NarrativeLogVectorRecord : VectorRecordBase
{
    [VectorStoreData(IsIndexed = true)]
    [Description("The session ID this event belongs to. Critical for filtering memories by playthrough.")]
    public string SessionId { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("The turn number on which the event occurred, used for chronological sorting.")]
    public int GameTurnNumber { get; set; }

    [VectorStoreData(IsIndexed = true)]
    [Description("The type of event, used for filtering. E.g., 'Dialogue', 'CombatVictory', 'QuestStart'.")]
    public string EventType { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [Description("A concise summary of the event. This text is used to generate the vector embedding.")]
    public string EventSummary { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = false)] // Not indexed for search, just for retrieval
    [Description("An optional field containing the full dialogue or a more detailed event description.")]
    public string FullTranscript { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [Description("A list of all entity IDs involved in the event (e.g., 'player', 'char_gary_oak'). Allows for filtering by participant.")]
    public string[] InvolvedEntities { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    [Description("The ID of the location where the event took place.")]
    public string LocationId { get; set; } = string.Empty;
}