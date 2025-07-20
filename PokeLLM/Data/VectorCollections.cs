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
    public const string STORYLINES = "adventure_storylines";
    public const string EVENTS = "adventure_event_history";
    public const string DIALOGUE = "adventure_dialogue_history";
    public const string POINTS_OF_INTEREST = "adventure_points_of_interest";
    public const string RULES_MECHANICS = "adventure_rules_mechanics";
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
    public string Environment { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedPointsOfInterest { get; set; } = Array.Empty<string>();
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
    public string Faction { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Motivations { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Abilities { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string ChallengeLevel { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedStorylines { get; set; } = Array.Empty<string>();
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

    [VectorStoreData(IsFullTextIndexed = true)]
    public string MechanicalEffects { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Requirements { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Value { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; } = Array.Empty<string>();
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
    public string[] RelatedEvents { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();
}

public class StorylineVectorModel
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

    [VectorStoreData(IsFullTextIndexed = true)]
    public string PlotHooks { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string PotentialOutcomes { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int ComplexityLevel { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedStorylines { get; set; } = Array.Empty<string>();
}

public class EventHistoryVectorModel
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

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Consequences { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string PlayerChoices { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedStoryLines { get; set; } = Array.Empty<string>();
}

public class DialogueHistoryVectorModel
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

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Context { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedStoryLines { get; set; } = Array.Empty<string>();
}

public class PointOfInterestVectorModel
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
    public string ChallengeType { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int DifficultyClass { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string RequiredSkills { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string PotentialOutcomes { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string EnvironmentalFactors { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Rewards { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedLocations { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedNpcs { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedItems { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedStoryLines { get; set; } = Array.Empty<string>();
}

public class RulesMechanicsVectorModel
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
    public string RuleSet { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Usage { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Examples { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedRules { get; set; } = Array.Empty<string>();
}