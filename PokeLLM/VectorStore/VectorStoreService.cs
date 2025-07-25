using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

namespace PokeLLM.Game.VectorStore;

public class VectorStoreService : IVectorStoreService
{
    private readonly QdrantVectorStore _vectorStore;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    // Define constants for our collection names
    private const string ENTITIES_COLLECTION = "entities";
    private const string LOCATIONS_COLLECTION = "locations";
    private const string LORE_COLLECTION = "lore_and_rules";
    private const string NARRATIVE_LOG_COLLECTION = "narrative_log";

    public VectorStoreService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, IOptions<QdrantConfig> qdrantOptions)
    {
        _embeddingGenerator = embeddingGenerator;
        
        _vectorStore = new QdrantVectorStore(
            new QdrantClient(qdrantOptions.Value.Host, qdrantOptions.Value.Port),
            ownsClient: true,
            new QdrantVectorStoreOptions
            {
                EmbeddingGenerator = _embeddingGenerator
            }
        );
    }

    #region Entity Queries
    public async Task<Guid> AddOrUpdateEntityAsync(EntityVectorRecord entity)
    {
        var collection = _vectorStore.GetCollection<Guid, EntityVectorRecord>(ENTITIES_COLLECTION);
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }
        await collection.UpsertAsync(entity);
        return entity.Id;
    }

    public async Task<EntityVectorRecord> GetEntityByIdAsync(string entityId)
    {
        var collection = _vectorStore.GetCollection<Guid, EntityVectorRecord>(ENTITIES_COLLECTION);

        // This is a filtered search to find an exact match, not a semantic search.
        var options = new VectorSearchOptions<EntityVectorRecord>
        {
            Filter = entity => entity.EntityId == entityId,
            Skip = 0,
            IncludeVectors = false
        };

        // We pass an empty query string because we only care about the filter.
        var results = collection.SearchAsync("", 1, options);

        await foreach (var item in results)
        {
            return item.Record;
        }

        return null;
    }

    #endregion


    #region Location and Lore Queries

    public async Task<Guid> AddOrUpdateLocationAsync(LocationVectorRecord location)
    {
        var collection = _vectorStore.GetCollection<Guid, LocationVectorRecord>(LOCATIONS_COLLECTION);
        if (location.Id == Guid.Empty)
        {
            location.Id = Guid.NewGuid();
        }
        await collection.UpsertAsync(location);
        return location.Id;
    }

    public async Task<Guid> AddOrUpdateLoreAsync(LoreVectorRecord lore)
    {
        var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
        if (lore.Id == Guid.Empty)
        {
            lore.Id = Guid.NewGuid();
        }
        await collection.UpsertAsync(lore);
        return lore.Id;
    }

    public async Task<IEnumerable<VectorSearchResult<LoreVectorRecord>>> SearchLoreAsync(string query, double minRelevanceScore = 0.75, int limit = 3)
    {
        // This is a standard semantic search, just like your example.
        var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
        var results = collection.SearchAsync(query, limit);

        var resultCollection = new List<VectorSearchResult<LoreVectorRecord>>();
        await foreach (var item in results)
        {
            if(item.Score >= minRelevanceScore)
                resultCollection.Add(item);
        }
        return resultCollection;
    }

    #endregion

    #region Narrative Log (Memory) Queries

    public async Task<Guid> LogNarrativeEventAsync(NarrativeLogVectorRecord narrativeLog)
    {
        var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
        if (narrativeLog.Id == Guid.Empty)
        {
            narrativeLog.Id = Guid.NewGuid();
        }
        await collection.UpsertAsync(narrativeLog);
        return narrativeLog.Id;
    }

    public async Task<IEnumerable<VectorSearchResult<NarrativeLogVectorRecord>>> FindMemoriesAsync(string sessionId, string query, string[] involvedEntities, double minRelevanceScore = 0.75, int limit = 5)
    {
        var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);

        // This is a HYBRID query: semantic search + filtering.
        var options = new VectorSearchOptions<NarrativeLogVectorRecord>
        {
            Filter = entry => entry.SessionId == sessionId &&
                             (involvedEntities == null || involvedEntities.Length == 0 ||
                              involvedEntities.Any(entity => entry.InvolvedEntities.Contains(entity))),
            Skip = 0,
            IncludeVectors = false
        };

        // Perform the hybrid search (semantic search + filtering)
        var results = collection.SearchAsync(query, limit, options);

        var resultCollection = new List<VectorSearchResult<NarrativeLogVectorRecord>>();
        await foreach (var item in results)
        {
            if (item.Score >= minRelevanceScore)
                resultCollection.Add(item);
        }

        return resultCollection.OrderByDescending(x => x.Score);
    }
    #endregion

}
