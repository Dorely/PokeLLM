using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using System.Diagnostics;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.VectorStore.Models;

namespace PokeLLM.Game.VectorStore;

public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantVectorStore _vectorStore;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly bool _isOllamaEmbeddings;
    
    // Define constants for our collection names
    private const string ENTITIES_COLLECTION = "entities";
    private const string LOCATIONS_COLLECTION = "locations";
    private const string LORE_COLLECTION = "lore";
    private const string RULE_COLLECTION = "rules";
    private const string NARRATIVE_LOG_COLLECTION = "narrative_log";

    public QdrantVectorStoreService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, 
        IOptions<QdrantConfig> qdrantOptions,
        IServiceProvider serviceProvider)
    {
        try
        {
            _embeddingGenerator = embeddingGenerator;
            
            // Determine if we're using Ollama embeddings (768 dimensions) by checking the available configs
            _isOllamaEmbeddings = DetermineEmbeddingProvider(serviceProvider);
            
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

    private bool DetermineEmbeddingProvider(IServiceProvider serviceProvider)
    {
        try
        {
            // First try to get hybrid config
            var hybridConfig = serviceProvider.GetService<IOptions<HybridConfig>>();
            if (hybridConfig?.Value != null)
            {
                return hybridConfig.Value.Embedding.Provider.ToLower() == "ollama";
            }

            // Fall back to checking ModelConfig
            var modelConfig = serviceProvider.GetService<IOptions<ModelConfig>>();
            if (modelConfig?.Value != null)
            {
                // If EmbeddingDimensions is set to 768, assume Ollama
                return modelConfig.Value.EmbeddingDimensions == 768;
            }

            // Default to assuming Ollama for safety (768 dimensions)
            return true;
        }
        catch
        {
            // If we can't determine, default to Ollama (768 dimensions)
            return true;
        }
    }

    #region Entity Queries
    public async Task<Guid> AddOrUpdateEntityAsync(EntityVectorRecord entity)
    {
        try
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (_isOllamaEmbeddings)
            {
                // Use Ollama-compatible models (768 dimensions)
                var collection = _vectorStore.GetCollection<Guid, EntityVectorRecord>(ENTITIES_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                if (entity.Id == Guid.Empty)
                {
                    entity.Id = Guid.NewGuid();
                }
                await collection.UpsertAsync(entity);
                return entity.Id;
            }
            else
            {
                // Use OpenAI-compatible models (1536 dimensions)
                var collection = _vectorStore.GetCollection<Guid, EntityVectorRecordOpenAI>(ENTITIES_COLLECTION);
                
                // Convert to OpenAI model
                var openAIEntity = new EntityVectorRecordOpenAI
                {
                    Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id,
                    EntityId = entity.EntityId,
                    EntityType = entity.EntityType,
                    Name = entity.Name,
                    Description = entity.Description,
                    PropertiesJson = entity.PropertiesJson,
                    Embedding = entity.Embedding
                };
                
                await collection.EnsureCollectionExistsAsync();
                await collection.UpsertAsync(openAIEntity);
                return openAIEntity.Id;
            }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, EntityVectorRecord>(ENTITIES_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<EntityVectorRecord>
                {
                    Filter = entity => entity.EntityId == entityId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return item.Record;
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, EntityVectorRecordOpenAI>(ENTITIES_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<EntityVectorRecordOpenAI>
                {
                    Filter = entity => entity.EntityId == entityId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    // Convert back to standard model
                    return new EntityVectorRecord
                    {
                        Id = item.Record.Id,
                        EntityId = item.Record.EntityId,
                        EntityType = item.Record.EntityType,
                        Name = item.Record.Name,
                        Description = item.Record.Description,
                        PropertiesJson = item.Record.PropertiesJson,
                        Embedding = item.Record.Embedding
                    };
                }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecord>(RULE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                if (rule.Id == Guid.Empty)
                {
                    rule.Id = Guid.NewGuid();
                }
                await collection.UpsertAsync(rule);
                return rule.Id;
            }
            else
            {
                var openAIRule = new GameRuleVectorRecordOpenAI
                {
                    Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id,
                    EntryId = rule.EntryId,
                    EntryType = rule.EntryType,
                    Title = rule.Title,
                    Content = rule.Content,
                    Tags = rule.Tags,
                    Embedding = rule.Embedding
                };
                
                var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecordOpenAI>(RULE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                await collection.UpsertAsync(openAIRule);
                return openAIRule.Id;
            }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecord>(RULE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<GameRuleVectorRecord>
                {
                    Filter = rule => rule.EntryId == entryId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return item.Record;
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecordOpenAI>(RULE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<GameRuleVectorRecordOpenAI>
                {
                    Filter = rule => rule.EntryId == entryId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return new GameRuleVectorRecord
                    {
                        Id = item.Record.Id,
                        EntryId = item.Record.EntryId,
                        EntryType = item.Record.EntryType,
                        Title = item.Record.Title,
                        Content = item.Record.Content,
                        Tags = item.Record.Tags,
                        Embedding = item.Record.Embedding
                    };
                }
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

            var resultCollection = new List<VectorSearchResult<GameRuleVectorRecord>>();

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecord>(RULE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                var results = collection.SearchAsync(query, limit);

                await foreach (var item in results)
                {
                    if (item.Score >= minRelevanceScore)
                        resultCollection.Add(item);
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, GameRuleVectorRecordOpenAI>(RULE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                var results = collection.SearchAsync(query, limit);

                await foreach (var item in results)
                {
                    if (item.Score >= minRelevanceScore)
                    {
                        var convertedRecord = new GameRuleVectorRecord
                        {
                            Id = item.Record.Id,
                            EntryId = item.Record.EntryId,
                            EntryType = item.Record.EntryType,
                            Title = item.Record.Title,
                            Content = item.Record.Content,
                            Tags = item.Record.Tags,
                            Embedding = item.Record.Embedding
                        };
                        resultCollection.Add(new VectorSearchResult<GameRuleVectorRecord>(convertedRecord, item.Score));
                    }
                }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, LocationVectorRecord>(LOCATIONS_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                if (location.Id == Guid.Empty)
                {
                    location.Id = Guid.NewGuid();
                }
                await collection.UpsertAsync(location);
                return location.Id;
            }
            else
            {
                var openAILocation = new LocationVectorRecordOpenAI
                {
                    Id = location.Id == Guid.Empty ? Guid.NewGuid() : location.Id,
                    LocationId = location.LocationId,
                    Name = location.Name,
                    Description = location.Description,
                    Region = location.Region,
                    Tags = location.Tags,
                    Embedding = location.Embedding
                };
                
                var collection = _vectorStore.GetCollection<Guid, LocationVectorRecordOpenAI>(LOCATIONS_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                await collection.UpsertAsync(openAILocation);
                return openAILocation.Id;
            }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, LocationVectorRecord>(LOCATIONS_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<LocationVectorRecord>
                {
                    Filter = location => location.LocationId == locationId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return item.Record;
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, LocationVectorRecordOpenAI>(LOCATIONS_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<LocationVectorRecordOpenAI>
                {
                    Filter = location => location.LocationId == locationId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return new LocationVectorRecord
                    {
                        Id = item.Record.Id,
                        LocationId = item.Record.LocationId,
                        Name = item.Record.Name,
                        Description = item.Record.Description,
                        Region = item.Record.Region,
                        Tags = item.Record.Tags,
                        Embedding = item.Record.Embedding
                    };
                }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                if (lore.Id == Guid.Empty)
                {
                    lore.Id = Guid.NewGuid();
                }
                await collection.UpsertAsync(lore);
                return lore.Id;
            }
            else
            {
                var openAILore = new LoreVectorRecordOpenAI
                {
                    Id = lore.Id == Guid.Empty ? Guid.NewGuid() : lore.Id,
                    EntryId = lore.EntryId,
                    EntryType = lore.EntryType,
                    Title = lore.Title,
                    Content = lore.Content,
                    Tags = lore.Tags,
                    Embedding = lore.Embedding
                };
                
                var collection = _vectorStore.GetCollection<Guid, LoreVectorRecordOpenAI>(LORE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                await collection.UpsertAsync(openAILore);
                return openAILore.Id;
            }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<LoreVectorRecord>
                {
                    Filter = lore => lore.EntryId == entryId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return item.Record;
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, LoreVectorRecordOpenAI>(LORE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<LoreVectorRecordOpenAI>
                {
                    Filter = lore => lore.EntryId == entryId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return new LoreVectorRecord
                    {
                        Id = item.Record.Id,
                        EntryId = item.Record.EntryId,
                        EntryType = item.Record.EntryType,
                        Title = item.Record.Title,
                        Content = item.Record.Content,
                        Tags = item.Record.Tags,
                        Embedding = item.Record.Embedding
                    };
                }
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

            var resultCollection = new List<VectorSearchResult<LoreVectorRecord>>();

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, LoreVectorRecord>(LORE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                var results = collection.SearchAsync(query, limit);

                await foreach (var item in results)
                {
                    if (item.Score >= minRelevanceScore)
                        resultCollection.Add(item);
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, LoreVectorRecordOpenAI>(LORE_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                var results = collection.SearchAsync(query, limit);

                await foreach (var item in results)
                {
                    if (item.Score >= minRelevanceScore)
                    {
                        var convertedRecord = new LoreVectorRecord
                        {
                            Id = item.Record.Id,
                            EntryId = item.Record.EntryId,
                            EntryType = item.Record.EntryType,
                            Title = item.Record.Title,
                            Content = item.Record.Content,
                            Tags = item.Record.Tags,
                            Embedding = item.Record.Embedding
                        };
                        resultCollection.Add(new VectorSearchResult<LoreVectorRecord>(convertedRecord, item.Score));
                    }
                }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                if (narrativeLog.Id == Guid.Empty)
                {
                    narrativeLog.Id = Guid.NewGuid();
                }
                await collection.UpsertAsync(narrativeLog);
                return narrativeLog.Id;
            }
            else
            {
                var openAINarrative = new NarrativeLogVectorRecordOpenAI
                {
                    Id = narrativeLog.Id == Guid.Empty ? Guid.NewGuid() : narrativeLog.Id,
                    SessionId = narrativeLog.SessionId,
                    GameTurnNumber = narrativeLog.GameTurnNumber,
                    EventType = narrativeLog.EventType,
                    EventSummary = narrativeLog.EventSummary,
                    FullTranscript = narrativeLog.FullTranscript,
                    InvolvedEntities = narrativeLog.InvolvedEntities,
                    LocationId = narrativeLog.LocationId,
                    Embedding = narrativeLog.Embedding
                };
                
                var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecordOpenAI>(NARRATIVE_LOG_COLLECTION);
                await collection.EnsureCollectionExistsAsync();
                await collection.UpsertAsync(openAINarrative);
                return openAINarrative.Id;
            }
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

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<NarrativeLogVectorRecord>
                {
                    Filter = entry => entry.SessionId == sessionId && entry.GameTurnNumber == gameTurnNumber,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return item.Record;
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecordOpenAI>(NARRATIVE_LOG_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<NarrativeLogVectorRecordOpenAI>
                {
                    Filter = entry => entry.SessionId == sessionId && entry.GameTurnNumber == gameTurnNumber,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync("", 1, options);
                await foreach (var item in results)
                {
                    return new NarrativeLogVectorRecord
                    {
                        Id = item.Record.Id,
                        SessionId = item.Record.SessionId,
                        GameTurnNumber = item.Record.GameTurnNumber,
                        EventType = item.Record.EventType,
                        EventSummary = item.Record.EventSummary,
                        FullTranscript = item.Record.FullTranscript,
                        InvolvedEntities = item.Record.InvolvedEntities,
                        LocationId = item.Record.LocationId,
                        Embedding = item.Record.Embedding
                    };
                }
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

            var resultCollection = new List<VectorSearchResult<NarrativeLogVectorRecord>>();

            if (_isOllamaEmbeddings)
            {
                var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecord>(NARRATIVE_LOG_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<NarrativeLogVectorRecord>
                {
                    Filter = entry => entry.SessionId == sessionId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync(query, limit * 2, options);
                await foreach (var item in results)
                {
                    if (item.Score >= minRelevanceScore)
                    {
                        if (involvedEntities == null || involvedEntities.Length == 0 ||
                            involvedEntities.Any(entity => item.Record.InvolvedEntities.Contains(entity)))
                        {
                            resultCollection.Add(item);
                            if (resultCollection.Count >= limit)
                                break;
                        }
                    }
                }
            }
            else
            {
                var collection = _vectorStore.GetCollection<Guid, NarrativeLogVectorRecordOpenAI>(NARRATIVE_LOG_COLLECTION);
                await collection.EnsureCollectionExistsAsync();

                var options = new VectorSearchOptions<NarrativeLogVectorRecordOpenAI>
                {
                    Filter = entry => entry.SessionId == sessionId,
                    Skip = 0,
                    IncludeVectors = false
                };

                var results = collection.SearchAsync(query, limit * 2, options);
                await foreach (var item in results)
                {
                    if (item.Score >= minRelevanceScore)
                    {
                        if (involvedEntities == null || involvedEntities.Length == 0 ||
                            involvedEntities.Any(entity => item.Record.InvolvedEntities.Contains(entity)))
                        {
                            var convertedRecord = new NarrativeLogVectorRecord
                            {
                                Id = item.Record.Id,
                                SessionId = item.Record.SessionId,
                                GameTurnNumber = item.Record.GameTurnNumber,
                                EventType = item.Record.EventType,
                                EventSummary = item.Record.EventSummary,
                                FullTranscript = item.Record.FullTranscript,
                                InvolvedEntities = item.Record.InvolvedEntities,
                                LocationId = item.Record.LocationId,
                                Embedding = item.Record.Embedding
                            };
                            resultCollection.Add(new VectorSearchResult<NarrativeLogVectorRecord>(convertedRecord, item.Score));
                            if (resultCollection.Count >= limit)
                                break;
                        }
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
