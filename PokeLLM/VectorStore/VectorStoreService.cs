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

    public async Task<Guid> StoreLocationAsync(string name, string description, string type, string environment = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedPointsOfInterest = null)
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
            Environment = environment,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedPointsOfInterest = relatedPointsOfInterest ?? Array.Empty<string>()
        };

        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreNPCAsync(string name, string description, string role, string location, string faction = "", string motivations = "", string abilities = "", string challengeLevel = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStorylines = null)
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
            Faction = faction,
            Motivations = motivations,
            Abilities = abilities,
            ChallengeLevel = challengeLevel,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedStorylines = relatedStorylines ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreItemAsync(string name, string description, string category, string rarity, string mechanicalEffects = "", string requirements = "", int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
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
            MechanicalEffects = mechanicalEffects,
            Requirements = requirements,
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

    public async Task<Guid> StoreStorylinesAsync(string name, string description, string plothooks = "", string potentialOutcomes = "", int complexityLevel = 3, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStorylines = null)
    {
        var collection = _vectorStore.GetCollection<Guid, StorylineVectorModel>(VectorCollections.STORYLINES);
        await collection.EnsureCollectionExistsAsync();
        var model = new StorylineVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            PlotHooks = plothooks,
            PotentialOutcomes = potentialOutcomes,
            ComplexityLevel = complexityLevel,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedStorylines = relatedStorylines ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreEventHistoryAsync(string name, string description, string type, string consequences = "", string playerChoices = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null)
    {
        var collection = _vectorStore.GetCollection<Guid, EventHistoryVectorModel>(VectorCollections.EVENTS);
        await collection.EnsureCollectionExistsAsync();
        var model = new EventHistoryVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Type = type,
            Consequences = consequences,
            PlayerChoices = playerChoices,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedStoryLines = relatedStoryLines ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreDialogueHistoryAsync(string speaker, string content, string topic, string context = "", string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedStoryLines = null)
    {
        var collection = _vectorStore.GetCollection<Guid, DialogueHistoryVectorModel>(VectorCollections.DIALOGUE);
        await collection.EnsureCollectionExistsAsync();
        var model = new DialogueHistoryVectorModel
        {
            Id = Guid.NewGuid(),
            Speaker = speaker,
            Content = content,
            ContentEmbedding = content,
            Topic = topic,
            Context = context,
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedStoryLines = relatedStoryLines ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StorePointOfInterestAsync(string name, string description, string challengeType, int difficultyClass, string requiredSkills, string potentialOutcomes, string environmentalFactors, string rewards, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null)
    {
        var collection = _vectorStore.GetCollection<Guid, PointOfInterestVectorModel>(VectorCollections.POINTS_OF_INTEREST);
        await collection.EnsureCollectionExistsAsync();
        var model = new PointOfInterestVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            ChallengeType = challengeType,
            DifficultyClass = difficultyClass,
            RequiredSkills = requiredSkills,
            PotentialOutcomes = potentialOutcomes,
            EnvironmentalFactors = environmentalFactors,
            Rewards = rewards,
            RelatedLocations = relatedLocations ?? Array.Empty<string>(),
            RelatedNpcs = relatedNpcs ?? Array.Empty<string>(),
            RelatedItems = relatedItems ?? Array.Empty<string>(),
            RelatedStoryLines = relatedStoryLines ?? Array.Empty<string>()
        };
        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<Guid> StoreRulesMechanicsAsync(string name, string description, string category, string ruleSet, string usage, string examples, string[] relatedRules = null)
    {
        var collection = _vectorStore.GetCollection<Guid, RulesMechanicsVectorModel>(VectorCollections.RULES_MECHANICS);
        await collection.EnsureCollectionExistsAsync();
        var model = new RulesMechanicsVectorModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DescriptionEmbedding = description,
            Category = category,
            RuleSet = ruleSet,
            Usage = usage,
            Examples = examples,
            RelatedRules = relatedRules ?? Array.Empty<string>()
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
                    ["Faction"] = item.Record.Faction,
                    ["Motivations"] = item.Record.Motivations,
                    ["Abilities"] = item.Record.Abilities,
                    ["ChallengeLevel"] = item.Record.ChallengeLevel,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems,
                    ["RelatedStorylines"] = item.Record.RelatedStorylines
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

    public async Task<IEnumerable<VectorSearchResult>> SearchStorylinesAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, StorylineVectorModel>(VectorCollections.STORYLINES);
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
                Type = "Storyline",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["PlotHooks"] = item.Record.PlotHooks,
                    ["PotentialOutcomes"] = item.Record.PotentialOutcomes,
                    ["ComplexityLevel"] = item.Record.ComplexityLevel,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems,
                    ["RelatedStorylines"] = item.Record.RelatedStorylines
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchEventsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, EventHistoryVectorModel>(VectorCollections.EVENTS);
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
                    ["Consequences"] = item.Record.Consequences,
                    ["PlayerChoices"] = item.Record.PlayerChoices,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems,
                    ["RelatedStoryLines"] = item.Record.RelatedStoryLines
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchDialogueAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, DialogueHistoryVectorModel>(VectorCollections.DIALOGUE);
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
                    ["Context"] = item.Record.Context,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedStoryLines"] = item.Record.RelatedStoryLines
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchPointsOfInterestAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, PointOfInterestVectorModel>(VectorCollections.POINTS_OF_INTEREST);
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
                Type = "PointOfInterest",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["ChallengeType"] = item.Record.ChallengeType,
                    ["DifficultyClass"] = item.Record.DifficultyClass,
                    ["RequiredSkills"] = item.Record.RequiredSkills,
                    ["PotentialOutcomes"] = item.Record.PotentialOutcomes,
                    ["EnvironmentalFactors"] = item.Record.EnvironmentalFactors,
                    ["Rewards"] = item.Record.Rewards,
                    ["RelatedLocations"] = item.Record.RelatedLocations,
                    ["RelatedNpcs"] = item.Record.RelatedNpcs,
                    ["RelatedItems"] = item.Record.RelatedItems,
                    ["RelatedStoryLines"] = item.Record.RelatedStoryLines
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchRulesMechanicsAsync(string query, int limit = 10)
    {
        var collection = _vectorStore.GetCollection<Guid, RulesMechanicsVectorModel>(VectorCollections.RULES_MECHANICS);
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
                Type = "RulesMechanics",
                Score = item.Score,
                Metadata = new Dictionary<string, object>
                {
                    ["Category"] = item.Record.Category,
                    ["RuleSet"] = item.Record.RuleSet,
                    ["Usage"] = item.Record.Usage,
                    ["Examples"] = item.Record.Examples,
                    ["RelatedRules"] = item.Record.RelatedRules
                }
            });
        }
        return resultCollection;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAllAsync(string query, int limit = 10)
    {
        var locationResult = await SearchLocationsAsync(query, limit);
        var npcResult = await SearchNPCsAsync(query, limit);
        var itemResult = await SearchItemsAsync(query, limit);
        var loreResult = await SearchLoreAsync(query, limit);
        var storylineResult = await SearchStorylinesAsync(query, limit);
        var eventResult = await SearchEventsAsync(query, limit);
        var dialogueResult = await SearchDialogueAsync(query, limit);
        var poiResult = await SearchPointsOfInterestAsync(query, limit);
        var rulesResult = await SearchRulesMechanicsAsync(query, limit);


        var allResults = new List<VectorSearchResult>();
        allResults.AddRange(locationResult);
        allResults.AddRange(npcResult);
        allResults.AddRange(itemResult);
        allResults.AddRange(loreResult);
        allResults.AddRange(storylineResult);
        allResults.AddRange(eventResult);
        allResults.AddRange(dialogueResult);
        allResults.AddRange(poiResult);
        allResults.AddRange(rulesResult);

        return allResults;
    }

}
