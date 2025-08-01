using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.Game.VectorStore.Models;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.GameState;
using Microsoft.Extensions.VectorData;

namespace PokeLLM.Game.GameLogic;

public interface IInformationManagementService
{
    // Entity methods
    Task<string> UpsertEntityAsync(string entityId, string entityType, string name, string description, string propertiesJson, Guid? id = null);
    Task<EntityVectorRecord> GetEntityAsync(string entityId);
    Task<IEnumerable<EntityVectorRecord>> SearchEntitiesAsync(string[] queries, string entityType = null);

    // Location methods  
    Task<string> UpsertLocationAsync(string locationId, string name, string description, string region, string[] tags = null, Guid? id = null);
    Task<LocationVectorRecord> GetLocationAsync(string locationId);

    // Lore methods
    Task<string> UpsertLoreAsync(string entryId, string entryType, string title, string content, string[] tags = null, Guid? id = null);
    Task<LoreVectorRecord> GetLoreAsync(string entryId);
    Task<IEnumerable<LoreVectorRecord>> SearchLoreAsync(string[] queries, string entryType = null);

    // Game rule methods
    Task<string> UpsertGameRuleAsync(string entryId, string entryType, string title, string content, string[] tags = null, Guid? id = null);
    Task<GameRuleVectorRecord> GetGameRuleAsync(string entryId);
    Task<IEnumerable<GameRuleVectorRecord>> SearchGameRulesAsync(string query, double minRelevanceScore = 0.75);

    // Narrative log methods
    Task<string> LogNarrativeEventAsync(string eventType, string eventSummary, string fullTranscript, string[] involvedEntities, string locationId, Guid? id = null);
    Task<NarrativeLogVectorRecord> GetNarrativeEventAsync(string sessionId, int gameTurnNumber);
    Task<IEnumerable<NarrativeLogVectorRecord>> FindMemoriesAsync(string sessionId, string query, string[] involvedEntities = null, double minRelevanceScore = 0.75);
}

/// <summary>
/// This service contains methods for querying, inserting, and updating the vector store
/// </summary>
public class InformationManagementService : IInformationManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IVectorStoreService _vectorStoreService;

    public InformationManagementService(IGameStateRepository gameStateRepository, IVectorStoreService vectorStoreService)
    {
        _gameStateRepository = gameStateRepository;
        _vectorStoreService = vectorStoreService;
    }

    #region Entity Methods

    public async Task<string> UpsertEntityAsync(string entityId, string entityType, string name, string description, string propertiesJson, Guid? id = null)
    {
        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingEntity = await _vectorStoreService.GetEntityByIdAsync(entityId);
            if (existingEntity != null)
            {
                // Update existing record - use its Guid
                actualId = existingEntity.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var entity = new EntityVectorRecord
        {
            Id = actualId,
            EntityId = entityId,
            EntityType = entityType,
            Name = name,
            Description = description,
            PropertiesJson = propertiesJson ?? "{}",
            Embedding = description // Set embedding to the description text
        };

        var vectorId = await _vectorStoreService.AddOrUpdateEntityAsync(entity);
        return $"Entity {entityId} upserted with ID {vectorId}";
    }

    public async Task<EntityVectorRecord> GetEntityAsync(string entityId)
    {
        return await _vectorStoreService.GetEntityByIdAsync(entityId);
    }

    public async Task<IEnumerable<EntityVectorRecord>> SearchEntitiesAsync(string[] queries, string entityType = null)
    {
        var results = new List<EntityVectorRecord>();
        
        foreach (var query in queries)
        {
            // For entity search, we'll use GetEntityByIdAsync for exact matches since entities use specific IDs
            var entity = await _vectorStoreService.GetEntityByIdAsync(query);
            if (entity != null && (entityType == null || entity.EntityType == entityType))
            {
                results.Add(entity);
            }
        }
        
        return results.Distinct();
    }

    #endregion

    #region Location Methods

    public async Task<string> UpsertLocationAsync(string locationId, string name, string description, string region, string[] tags = null, Guid? id = null)
    {
        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingLocation = await _vectorStoreService.GetLocationByIdAsync(locationId);
            if (existingLocation != null)
            {
                // Update existing record - use its Guid
                actualId = existingLocation.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var location = new LocationVectorRecord
        {
            Id = actualId,
            LocationId = locationId,
            Name = name,
            Description = description,
            Region = region,
            Tags = tags ?? Array.Empty<string>(),
            Embedding = description // Set embedding to the description text
        };

        var vectorId = await _vectorStoreService.AddOrUpdateLocationAsync(location);
        return $"Location {locationId} upserted with ID {vectorId}";
    }

    public async Task<LocationVectorRecord> GetLocationAsync(string locationId)
    {
        return await _vectorStoreService.GetLocationByIdAsync(locationId);
    }

    #endregion

    #region Lore Methods

    public async Task<string> UpsertLoreAsync(string entryId, string entryType, string title, string content, string[] tags = null, Guid? id = null)
    {
        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingLore = await _vectorStoreService.GetLoreByIdAsync(entryId);
            if (existingLore != null)
            {
                // Update existing record - use its Guid
                actualId = existingLore.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var lore = new LoreVectorRecord
        {
            Id = actualId,
            EntryId = entryId,
            EntryType = entryType,
            Title = title,
            Content = content,
            Tags = tags ?? Array.Empty<string>(),
            Embedding = content // Set embedding to the content text
        };

        var vectorId = await _vectorStoreService.AddOrUpdateLoreAsync(lore);
        return $"Lore {entryId} upserted with ID {vectorId}";
    }

    public async Task<LoreVectorRecord> GetLoreAsync(string entryId)
    {
        return await _vectorStoreService.GetLoreByIdAsync(entryId);
    }

    public async Task<IEnumerable<LoreVectorRecord>> SearchLoreAsync(string[] queries, string entryType = null)
    {
        var results = new List<LoreVectorRecord>();
        
        foreach (var query in queries)
        {
            var searchResults = await _vectorStoreService.SearchLoreAsync(query);
            foreach (var result in searchResults)
            {
                if (entryType == null || result.Record.EntryType == entryType)
                {
                    results.Add(result.Record);
                }
            }
        }
        
        return results.Distinct();
    }

    #endregion

    #region Game Rule Methods

    public async Task<string> UpsertGameRuleAsync(string entryId, string entryType, string title, string content, string[] tags = null, Guid? id = null)
    {
        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingRule = await _vectorStoreService.GetGameRuleByIdAsync(entryId);
            if (existingRule != null)
            {
                // Update existing record - use its Guid
                actualId = existingRule.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var rule = new GameRuleVectorRecord
        {
            Id = actualId,
            EntryId = entryId,
            EntryType = entryType,
            Title = title,
            Content = content,
            Tags = tags ?? Array.Empty<string>(),
            Embedding = content // Set embedding to the content text
        };

        var vectorId = await _vectorStoreService.AddOrUpdateGameRuleAsync(rule);
        return $"Game rule {entryId} upserted with ID {vectorId}";
    }

    public async Task<GameRuleVectorRecord> GetGameRuleAsync(string entryId)
    {
        return await _vectorStoreService.GetGameRuleByIdAsync(entryId);
    }

    public async Task<IEnumerable<GameRuleVectorRecord>> SearchGameRulesAsync(string query, double minRelevanceScore = 0.75)
    {
        var searchResults = await _vectorStoreService.SearchGameRulesAsync(query, minRelevanceScore);
        return searchResults.Select(r => r.Record);
    }

    #endregion

    #region Narrative Log Methods

    public async Task<string> LogNarrativeEventAsync(string eventType, string eventSummary, string fullTranscript, string[] involvedEntities, string locationId, Guid? id = null)
    {
        // Load current game state to get sessionId and gameTurnNumber
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var sessionId = gameState.SessionId;
        var gameTurnNumber = gameState.GameTurnNumber;
        
        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the sessionId and gameTurnNumber
            var existingEvent = await _vectorStoreService.GetNarrativeEventAsync(sessionId, gameTurnNumber);
            if (existingEvent != null)
            {
                // Update existing record - use its Guid
                actualId = existingEvent.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var narrativeLog = new NarrativeLogVectorRecord
        {
            Id = actualId,
            SessionId = sessionId,
            GameTurnNumber = gameTurnNumber,
            EventType = eventType,
            EventSummary = eventSummary,
            FullTranscript = fullTranscript ?? string.Empty,
            InvolvedEntities = involvedEntities ?? Array.Empty<string>(),
            LocationId = locationId,
            Embedding = eventSummary // Set embedding to the event summary text
        };

        var vectorId = await _vectorStoreService.LogNarrativeEventAsync(narrativeLog);
        return $"Narrative event logged with ID {vectorId}";
    }

    public async Task<NarrativeLogVectorRecord> GetNarrativeEventAsync(string sessionId, int gameTurnNumber)
    {
        return await _vectorStoreService.GetNarrativeEventAsync(sessionId, gameTurnNumber);
    }

    public async Task<IEnumerable<NarrativeLogVectorRecord>> FindMemoriesAsync(string sessionId, string query, string[] involvedEntities = null, double minRelevanceScore = 0.75)
    {
        var searchResults = await _vectorStoreService.FindMemoriesAsync(sessionId, query, involvedEntities, minRelevanceScore);
        return searchResults.Select(r => r.Record);
    }

    #endregion
}
