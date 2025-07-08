using Microsoft.Extensions.VectorData;

namespace PokeLLM.Game.Data;

public class GameHistoryModel
{
    [VectorStoreKey]
    public Guid EntryId { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string EntryName { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Description { get; set; }

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public string DescriptionEmbedding { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string[] Tags { get; set; }
}
