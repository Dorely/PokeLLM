using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.Data;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.VectorStore.Interfaces;
using Qdrant.Client;

namespace PokeLLM.Game.VectorStore;

public class VectorStoreService : IVectorStoreService
{
    private readonly QdrantVectorStore _vectorStore;

    public VectorStoreService(ILLMProvider llmProvider, IOptions<QdrantConfig> options)
    {
        var embeddingGenerator = llmProvider.GetEmbeddingGenerator();

        _vectorStore = new QdrantVectorStore(
            new QdrantClient(options.Value.Host, options.Value.Port), 
            ownsClient: true,
            new QdrantVectorStoreOptions
            {
                EmbeddingGenerator = embeddingGenerator
            }
        );
    }

    public async Task<Guid> StoreLocationAsync(string name, string description, string type, 
        string[] connectedLocations = null, string[] tags = null, string[] connectedNpcs = null,
        string[] connectedItems = null)
    {
        var collection = _vectorStore.GetCollection<Guid, LocationVectorModel>(VectorCollections.LOCATIONS);
        await collection.EnsureCollectionExistsAsync();

        var model = new LocationVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Type = type,
            RelatedLocations = connectedLocations ?? Array.Empty<string>(),
            RelatedNpcs = connectedNpcs ?? Array.Empty<string>(),
            RelatedItems = connectedItems ?? Array.Empty<string>()
        };

        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreNPCAsync(string name, string description, string role, string location, int level = 1, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedQuests = null)
    {
        var collection = _vectorStore.GetCollection<Guid, NPCVectorModel>(VectorCollections.NPCS);
        await collection.EnsureCollectionExistsAsync();
        var model = new NPCVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Role = role,
            Location = location,
            Level = level,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedQuests = relatedQuests ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreItemAsync(string name, string description, string category, string rarity, int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
    {
        var collection = _vectorStore.GetCollection<Guid, ItemVectorModel>(VectorCollections.ITEMS);
        await collection.EnsureCollectionExistsAsync();
        var model = new ItemVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Category = category,
            Rarity = rarity,
            Value = value,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreLoreAsync(string name, string description, string category, string timePeriod, string importance, string region, string[] relatedEvents = null, string[] relatedNpcs = null, string[] relatedLocations = null)
    {
        var collection = _vectorStore.GetCollection<Guid, LoreVectorModel>(VectorCollections.LORE);
        await collection.EnsureCollectionExistsAsync();
        var model = new LoreVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Category = category,
            TimePeriod = timePeriod,
            Importance = importance,
            Region = region,
            RelatedEvents = relatedEvents ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedLocations = relatedLocations ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreQuestAsync(string name, string description, string type, string status, string giverNPC, int level = 1, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
    {
        var collection = _vectorStore.GetCollection<Guid, QuestVectorModel>(VectorCollections.QUESTS);
        await collection.EnsureCollectionExistsAsync();
        var model = new QuestVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Type = type,
            Status = status,
            GiverNPC = giverNPC,
            Level = level,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreEventAsync(string name, string description, string type, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedEvents = null)
    {
        var collection = _vectorStore.GetCollection<Guid, EventVectorModel>(VectorCollections.EVENTS);
        await collection.EnsureCollectionExistsAsync();
        var model = new EventVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Type = type,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedEvents = relatedEvents ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreDialogueAsync(string speaker, string content, string topic, string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedQuests = null)
    {
        var collection = _vectorStore.GetCollection<Guid, DialogueVectorModel>(VectorCollections.DIALOGUE);
        await collection.EnsureCollectionExistsAsync();
        var model = new DialogueVectorModel
        {
            Id = Guid.NewGuid(),
            Speaker = speaker,
            Content = content,
            ContentEmbedding = content,
            Topic = topic,
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedQuests = relatedQuests ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchLocationsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, LocationVectorModel>(VectorCollections.LOCATIONS);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(
                new VectorSearchResult
                {
                    Id = item.Record.Id,
                    Name = item.Record.Name,
                    Content = item.Record.Description,
                    Type = "Location",
                    Score = item.Score,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Type"] = item.Record.Type,
                        ["RelatedLocations"] = item.Record.RelatedLocations,
                        ["RelatedNpcs"] = item.Record.RelatedNpcs,
                        ["RelatedItems"] = item.Record.RelatedItems
                    }
                }
            );
        }

        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchNPCsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, NPCVectorModel>(VectorCollections.NPCS);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(new VectorSearchResult
            {
                Id = item.Record.Id,
                Name = item.Record.Name,
                Content = item.Record.Description,
                Type = "NPC",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Role"] = item.Record.Role,
                    ["Location"] = item.Record.Location,
                    ["Level"] = item.Record.Level,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems,
                    ["RelatedQuests"] = item.Record.RelatedQuests
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchItemsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, ItemVectorModel>(VectorCollections.ITEMS);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(new VectorSearchResult
            {
                Id = item.Record.Id,
                Name = item.Record.Name,
                Content = item.Record.Description,
                Type = "Item",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Category"] = item.Record.Category,
                    ["Rarity"] = item.Record.Rarity,
                    ["Value"] = item.Record.Value,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchLoreAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, LoreVectorModel>(VectorCollections.LORE);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(new VectorSearchResult
            {
                Id = item.Record.Id,
                Name = item.Record.Name,
                Content = item.Record.Description,
                Type = "Lore",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Category"] = item.Record.Category,
                    ["TimePeriod"] = item.Record.TimePeriod,
                    ["Importance"] = item.Record.Importance,
                    ["Region"] = item.Record.Region,
                    ["RelatedEvents"] = item.Record.RelatedEvents,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedLocations"] = item.Record.RelatedLocations
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchQuestsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, QuestVectorModel>(VectorCollections.QUESTS);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(new VectorSearchResult
            {
                Id = item.Record.Id,
                Name = item.Record.Name,
                Content = item.Record.Description,
                Type = "Quest",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Type"] = item.Record.Type,
                    ["Status"] = item.Record.Status,
                    ["GiverNPC"] = item.Record.GiverNPC,
                    ["Level"] = item.Record.Level,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchEventsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, EventVectorModel>(VectorCollections.EVENTS);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(new VectorSearchResult
            {
                Id = item.Record.Id,
                Name = item.Record.Name,
                Content = item.Record.Description,
                Type = "Event",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Type"] = item.Record.Type,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems,
                    ["RelatedEvents"] = item.Record.RelatedEvents
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchDialogueAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, DialogueVectorModel>(VectorCollections.DIALOGUE);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        var resultCollection = new List<VectorSearchResult>();
        await foreach (var item in results)
        {
            resultCollection.Add(new VectorSearchResult
            {
                Id = item.Record.Id,
                Name = item.Record.Speaker,
                Content = item.Record.Content,
                Type = "Dialogue",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Topic"] = item.Record.Topic,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedQuests"] = item.Record.RelatedQuests
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAllAsync(string query, int limit = 10)
    {
        var locationResult = await SearchLocationsAsync(query, limit);
        var npcResult = await SearchNPCsAsync(query, limit);
        var itemResult = await  SearchItemsAsync(query, limit);
        var loreResult = await SearchLoreAsync(query, limit);
        var questResult = await SearchQuestsAsync(query, limit);
        var eventResult = await SearchEventsAsync(query, limit);
        var dialogueResult = await SearchDialogueAsync(query, limit);


        var allResults = new List<VectorSearchResult>();
        allResults.AddRange(locationResult);
        allResults.AddRange(npcResult);
        allResults.AddRange(itemResult);
        allResults.AddRange(loreResult);
        allResults.AddRange(questResult);
        allResults.AddRange(eventResult);
        allResults.AddRange(dialogueResult);

        return allResults;
    }

}
