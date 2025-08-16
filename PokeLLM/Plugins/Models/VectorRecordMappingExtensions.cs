using PokeLLM.Game.VectorStore.Models;
using PokeLLM.GameState.Models;
using System.Text.Json;

namespace PokeLLM.Game.Plugins.Models;

/// <summary>
/// Extension methods for mapping between DTOs and VectorRecord models
/// </summary>
public static class VectorRecordMappingExtensions
{
    #region LoreVectorRecord Mappings
    
    public static LoreVectorRecord ToVectorRecord(this LoreVectorRecordDto dto, Guid? id = null)
    {
        return new LoreVectorRecord
        {
            Id = id ?? Guid.Empty,
            EntryId = dto.EntryId,
            EntryType = dto.EntryType,
            Title = dto.Title,
            Content = dto.Content,
            Tags = dto.Tags
        };
    }

    public static LoreVectorRecordDto ToDto(this LoreVectorRecord record)
    {
        return new LoreVectorRecordDto
        {
            EntryId = record.EntryId,
            EntryType = record.EntryType,
            Title = record.Title,
            Content = record.Content,
            Tags = record.Tags
        };
    }

    #endregion

    #region GameRuleVectorRecord Mappings

    public static GameRuleVectorRecord ToVectorRecord(this GameRuleVectorRecordDto dto, Guid? id = null)
    {
        return new GameRuleVectorRecord
        {
            Id = id ?? Guid.Empty,
            EntryId = dto.EntryId,
            EntryType = dto.EntryType,
            Title = dto.Title,
            Content = dto.Content,
            Tags = dto.Tags
        };
    }

    public static GameRuleVectorRecordDto ToDto(this GameRuleVectorRecord record)
    {
        return new GameRuleVectorRecordDto
        {
            EntryId = record.EntryId,
            EntryType = record.EntryType,
            Title = record.Title,
            Content = record.Content,
            Tags = record.Tags
        };
    }

    #endregion

    #region EntityVectorRecord Mappings

    public static EntityVectorRecord ToVectorRecord(this EntityVectorRecordDto dto, Guid? id = null)
    {
        return new EntityVectorRecord
        {
            Id = id ?? Guid.Empty,
            EntityId = dto.EntityId,
            EntityType = dto.EntityType,
            Name = dto.Name,
            Description = dto.Description,
            PropertiesJson = dto.PropertiesJson
        };
    }

    public static EntityVectorRecordDto ToDto(this EntityVectorRecord record)
    {
        return new EntityVectorRecordDto
        {
            EntityId = record.EntityId,
            EntityType = record.EntityType,
            Name = record.Name,
            Description = record.Description,
            PropertiesJson = record.PropertiesJson
        };
    }

    #endregion

    #region LocationVectorRecord Mappings

    public static LocationVectorRecord ToVectorRecord(this LocationVectorRecordDto dto, Guid? id = null)
    {
        return new LocationVectorRecord
        {
            Id = id ?? Guid.Empty,
            LocationId = dto.LocationId,
            Name = dto.Name,
            Description = dto.Description,
            Region = dto.Region,
            Tags = dto.Tags
        };
    }

    public static LocationVectorRecordDto ToDto(this LocationVectorRecord record)
    {
        return new LocationVectorRecordDto
        {
            LocationId = record.LocationId,
            Name = record.Name,
            Description = record.Description,
            Region = record.Region,
            Tags = record.Tags
        };
    }

    #endregion

    #region NarrativeLogVectorRecord Mappings

    public static NarrativeLogVectorRecord ToVectorRecord(this NarrativeLogVectorRecordDto dto, Guid? id = null)
    {
        return new NarrativeLogVectorRecord
        {
            Id = id ?? Guid.Empty,
            SessionId = dto.SessionId,
            GameTurnNumber = dto.GameTurnNumber,
            EventType = dto.EventType,
            EventSummary = dto.EventSummary,
            FullTranscript = dto.FullTranscript,
            InvolvedEntities = dto.InvolvedEntities,
            LocationId = dto.LocationId
        };
    }

    public static NarrativeLogVectorRecordDto ToDto(this NarrativeLogVectorRecord record)
    {
        return new NarrativeLogVectorRecordDto
        {
            SessionId = record.SessionId,
            GameTurnNumber = record.GameTurnNumber,
            EventType = record.EventType,
            EventSummary = record.EventSummary,
            FullTranscript = record.FullTranscript,
            InvolvedEntities = record.InvolvedEntities,
            LocationId = record.LocationId
        };
    }

    #endregion

    #region GameStateModel Mappings

    /// <summary>
    /// Convert NPC DTO to generic dictionary
    /// </summary>
    public static Dictionary<string, object> ToGameStateModel(this NpcDto dto)
    {
        return new Dictionary<string, object>
        {
            ["id"] = dto.Id,
            ["name"] = dto.Name,
            ["characterClass"] = dto.Class ?? "npc",
            ["isTrainer"] = dto.IsTrainer,
            ["money"] = dto.Money,
            ["factions"] = dto.Factions ?? Array.Empty<string>(),
            ["statsJson"] = dto.StatsJson ?? "{}",
            ["type"] = "npc"
        };
    }

    /// <summary>
    /// Convert Pokemon DTO to generic dictionary
    /// </summary>
    public static Dictionary<string, object> ToGameStateModel(this PokemonDto dto)
    {
        return new Dictionary<string, object>
        {
            ["id"] = dto.Id,
            ["species"] = dto.Species ?? "unknown",
            ["nickname"] = dto.NickName ?? "",
            ["level"] = dto.Level,
            ["type1"] = dto.Type1 ?? "Normal",
            ["type2"] = dto.Type2 ?? "",
            ["abilities"] = dto.Abilities?.ToList() ?? new List<string>(),
            ["factions"] = dto.Factions?.ToList() ?? new List<string>(),
            ["statsJson"] = dto.StatsJson ?? "{}",
            ["type"] = "pokemon"
        };
    }

    /// <summary>
    /// Convert Location DTO to generic dictionary
    /// </summary>
    public static Dictionary<string, object> ToGameStateModel(this LocationDto dto)
    {
        return new Dictionary<string, object>
        {
            ["id"] = dto.Id,
            ["name"] = dto.Name ?? "Unknown Location",
            ["description"] = dto.Description ?? "",
            ["exits"] = new Dictionary<string, string>(), // LocationDto doesn't have Exits property
            ["npcs"] = new List<string>(), // LocationDto doesn't have NPCs property
            ["type"] = "location"
        };
    }

    #endregion
}