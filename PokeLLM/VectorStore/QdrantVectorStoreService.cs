using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using System.Diagnostics;

namespace PokeLLM.Game.VectorStore;

public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantVectorStore _vectorStore;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    // Define constants for our collection names
    private const string ENTITIES_COLLECTION = "entities";
    private const string LOCATIONS_COLLECTION = "locations";
    private const string LORE_COLLECTION = "lore";
    private const string RULE_COLLECTION = "rules";
    private const string NARRATIVE_LOG_COLLECTION = "narrative_log";

    public QdrantVectorStoreService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, IOptions<QdrantConfig> qdrantOptions)
    {
        try
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in constructor: {ex.Message}");
            throw;
        }
    }

    #region Entity Queries
    public async Task<Guid> AddOrUpdateEntityAsync(EntityVectorRecord entity)
    {
        try
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var collection = _vectorStore.GetCollection<Guid, EntityVectorRecord>(ENTITIES_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }
            await collection.UpsertAsync(entity);
            return entity.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in AddOrUpdateEntityAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<EntityVectorRecord> GetEntityByIdAsync(string entityId)
    {
        try
        {
            if (string.IsNullOrEmpty(entityId))
                throw new ArgumentException("EntityId cannot be null or empty", nameof(entityId));

            var collection = _vectorStore.GetCollection<Guid, EntityVectorRecord>(ENTITIES_COLLECTION);
            await collection.EnsureCollectionExistsAsync();

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
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in GetEntityByIdAsync: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region GameRule Queries

    public async Task<Guid> AddOrUpdateGameRuleAsync(GameRuleVectorRecord rule)
    {
        try
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecord>(RULE_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            if (rule.Id == Guid.Empty)
            {
                rule.Id = Guid.NewGuid();
            }
            await collection.UpsertAsync(rule);
            return rule.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in AddOrUpdateGameRuleAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<GameRuleVectorRecord> GetGameRuleByIdAsync(string entryId)
    {
        try
        {
            if (string.IsNullOrEmpty(entryId))
                throw new ArgumentException("EntryId cannot be null or empty", nameof(entryId));

            var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecord>(RULE_COLLECTION);
            await collection.EnsureCollectionExistsAsync();

            // This is a filtered search to find an exact match, not a semantic search.
            var options = new VectorSearchOptions<GameRuleVectorRecord>
            {
                Filter = rule => rule.EntryId == entryId,
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in GetGameRuleByIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<VectorSearchResult<GameRuleVectorRecord>>> SearchGameRulesAsync(string query, double minRelevanceScore = 0.75, int limit = 3)
    {
        try
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));

            // This is a standard semantic search, just like your example.
            var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecord>(RULE_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            var results = collection.SearchAsync(query, limit);

            var resultCollection = new List<VectorSearchResult<GameRuleVectorRecord>>();
            await foreach (var item in results)
            {
                if(item.Score >= minRelevanceScore)
                    resultCollection.Add(item);
            }
            return resultCollection;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in SearchGameRulesAsync: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Location and Lore Queries

    public async Task<Guid> AddOrUpdateLocationAsync(LocationVectorRecord location)
    {
        try
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var collection = _vectorStore.GetCollection<Guid, LocationVectorRecord>(LOCATIONS_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            if (location.Id == Guid.Empty)
            {
                location.Id = Guid.NewGuid();
            }
            await collection.UpsertAsync(location);
            return location.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in AddOrUpdateLocationAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<LocationVectorRecord> GetLocationByIdAsync(string locationId)
    {
        try
        {
            if (string.IsNullOrEmpty(locationId))
                throw new ArgumentException("LocationId cannot be null or empty", nameof(locationId));

            var collection = _vectorStore.GetCollection<Guid, LocationVectorRecord>(LOCATIONS_COLLECTION);
            await collection.EnsureCollectionExistsAsync();

            // This is a filtered search to find an exact match, not a semantic search.
            var options = new VectorSearchOptions<LocationVectorRecord>
            {
                Filter = location => location.LocationId == locationId,
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in GetLocationByIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<Guid> AddOrUpdateLoreAsync(LoreVectorRecord lore)
    {
        try
        {
            if (lore == null)
                throw new ArgumentNullException(nameof(lore));

            var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            if (lore.Id == Guid.Empty)
            {
                lore.Id = Guid.NewGuid();
            }
            await collection.UpsertAsync(lore);
            return lore.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in AddOrUpdateLoreAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<LoreVectorRecord> GetLoreByIdAsync(string entryId)
    {
        try
        {
            if (string.IsNullOrEmpty(entryId))
                throw new ArgumentException("EntryId cannot be null or empty", nameof(entryId));

            var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
            await collection.EnsureCollectionExistsAsync();

            // This is a filtered search to find an exact match, not a semantic search.
            var options = new VectorSearchOptions<LoreVectorRecord>
            {
                Filter = lore => lore.EntryId == entryId,
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in GetLoreByIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<VectorSearchResult<LoreVectorRecord>>> SearchLoreAsync(string query, double minRelevanceScore = 0.75, int limit = 3)
    {
        try
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));

            // This is a standard semantic search, just like your example.
            var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            var results = collection.SearchAsync(query, limit);

            var resultCollection = new List<VectorSearchResult<LoreVectorRecord>>();
            await foreach (var item in results)
            {
                if (item.Score >= minRelevanceScore)
                    resultCollection.Add(item);
            }
            return resultCollection;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in SearchLoreAsync: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Narrative Log (Memory) Queries

    public async Task<Guid> LogNarrativeEventAsync(NarrativeLogVectorRecord narrativeLog)
    {
        try
        {
            if (narrativeLog == null)
                throw new ArgumentNullException(nameof(narrativeLog));

            var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
            await collection.EnsureCollectionExistsAsync();
            if (narrativeLog.Id == Guid.Empty)
            {
                narrativeLog.Id = Guid.NewGuid();
            }
            await collection.UpsertAsync(narrativeLog);
            return narrativeLog.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in LogNarrativeEventAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<NarrativeLogVectorRecord> GetNarrativeEventAsync(string sessionId, int gameTurnNumber)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("SessionId cannot be null or empty", nameof(sessionId));

            var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
            await collection.EnsureCollectionExistsAsync();

            // This is a filtered search to find an exact match using session ID and game turn number.
            var options = new VectorSearchOptions<NarrativeLogVectorRecord>
            {
                Filter = entry => entry.SessionId == sessionId && entry.GameTurnNumber == gameTurnNumber,
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in GetNarrativeEventAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<VectorSearchResult<NarrativeLogVectorRecord>>> FindMemoriesAsync(string sessionId, string query, string[] involvedEntities, double minRelevanceScore = 0.75, int limit = 5)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("SessionId cannot be null or empty", nameof(sessionId));
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));

            var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
            await collection.EnsureCollectionExistsAsync();

            // Simplify the filter to avoid complex LINQ expressions that cause translation issues
            VectorSearchOptions<NarrativeLogVectorRecord> options;
            
            if (involvedEntities == null || involvedEntities.Length == 0)
            {
                // Simple session filter only
                options = new VectorSearchOptions<NarrativeLogVectorRecord>
                {
                    Filter = entry => entry.SessionId == sessionId,
                    Skip = 0,
                    IncludeVectors = false
                };
            }
            else
            {
                // For entity filtering, we'll filter manually after the search
                // to avoid complex LINQ translation issues
                options = new VectorSearchOptions<NarrativeLogVectorRecord>
                {
                    Filter = entry => entry.SessionId == sessionId,
                    Skip = 0,
                    IncludeVectors = false
                };
            }

            // Perform the search
            var results = collection.SearchAsync(query, limit * 2, options); // Get more results for entity filtering

            var resultCollection = new List<VectorSearchResult<NarrativeLogVectorRecord>>();
            await foreach (var item in results)
            {
                if (item.Score >= minRelevanceScore)
                {
                    // Manual entity filtering if needed
                    if (involvedEntities == null || involvedEntities.Length == 0 ||
                        involvedEntities.Any(entity => item.Record.InvolvedEntities.Contains(entity)))
                    {
                        resultCollection.Add(item);
                        if (resultCollection.Count >= limit)
                            break;
                    }
                }
            }

            return resultCollection.OrderByDescending(x => x.Score);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QdrantVectorStoreService] Error in FindMemoriesAsync: {ex.Message}");
            throw;
        }
    }
    #endregion

}
