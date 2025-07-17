using Microsoft.Extensions.VectorData;

namespace PokeLLM.Game.Data;

// Vector search result model
public class VectorSearchResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double? Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public static class VectorCollections
{
    // Collection names
    public const string LOCATIONS = "adventure_locations";
    public const string NPCS = "adventure_npcs";
    public const string ITEMS = "adventure_items";
    public const string LORE = "adventure_lore";
    public const string QUESTS = "adventure_quests";
    public const string EVENTS = "adventure_events";
    public const string DIALOGUE = "adventure_dialogue";
}

public class LocationVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Type { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; }
}

public class NPCVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Role { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Location { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Level { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedQuests { get; set; }
}

public class ItemVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Rarity { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Value { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; }
}

public class LoreVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string TimePeriod { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Importance { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Region { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedEvents { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }
}

public class QuestVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Type { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Status { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string GiverNPC { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Level { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; }
}

public class EventVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Type { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedEvents { get; set; }
}

public class DialogueVectorModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Speaker { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string ContentEmbedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Topic { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedQuests { get; set; }
}