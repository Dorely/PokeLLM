using Microsoft.Extensions.VectorData;

namespace PokeLLM.Game.VectorStore.Models;

public class VectorStoreModel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string Embedding { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Type { get; set; } = string.Empty;

    // Flexible metadata for any additional fields
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [VectorStoreData(IsIndexed = true)]
    public string[] RelatedEntries { get; set; } = Array.Empty<string>();
}