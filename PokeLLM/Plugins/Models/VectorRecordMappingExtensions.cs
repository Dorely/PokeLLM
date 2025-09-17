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

    #region Adventure Session Mappings

    /// <summary>
    /// Convert NpcDto to Npc
    /// </summary>
    public static Npc ToAdventureNpc(this NpcDto dto)
    {
        var npc = new Npc
        {
            Id = dto.Id,
            Name = dto.Name,
            IsTrainer = dto.IsTrainer,
            Factions = dto.Factions.ToList(),
            CharacterDetails = new CharacterDetails
            {
                Class = dto.Class,
                Money = dto.Money
            }
        };

        // Deserialize stats from JSON if provided
        if (!string.IsNullOrEmpty(dto.StatsJson) && dto.StatsJson != "{}")
        {
            try
            {
                npc.Stats = JsonSerializer.Deserialize<Stats>(dto.StatsJson) ?? new Stats();
            }
            catch
            {
                npc.Stats = new Stats();
            }
        }

        return npc;
    }

    /// <summary>
    /// Convert PokemonDto to Pokemon
    /// </summary>
    public static Pokemon ToAdventurePokemon(this PokemonDto dto)
    {
        var pokemon = new Pokemon
        {
            Id = dto.Id,
            NickName = dto.NickName,
            Species = dto.Species,
            Level = dto.Level,
            Abilities = dto.Abilities.ToList(),
            Factions = dto.Factions.ToList()
        };

        // Parse types
        if (Enum.TryParse<PokemonType>(dto.Type1, true, out var type1))
        {
            pokemon.Type1 = type1;
        }

        if (!string.IsNullOrEmpty(dto.Type2) && Enum.TryParse<PokemonType>(dto.Type2, true, out var type2))
        {
            pokemon.Type2 = type2;
        }

        // Deserialize stats from JSON if provided
        if (!string.IsNullOrEmpty(dto.StatsJson) && dto.StatsJson != "{}")
        {
            try
            {
                pokemon.Stats = JsonSerializer.Deserialize<Stats>(dto.StatsJson) ?? new Stats();
            }
            catch
            {
                pokemon.Stats = new Stats();
            }
        }

        return pokemon;
    }

    /// <summary>
    /// Convert LocationDto to Location
    /// </summary>
    public static Location ToAdventureLocation(this LocationDto dto)
    {
        var location = new Location
        {
            Id = dto.Id,
            Name = dto.Name,
            DescriptionVectorId = dto.Id // Use the location ID as the vector ID for lookup
        };

        // Deserialize points of interest from JSON if provided
        if (!string.IsNullOrEmpty(dto.PointsOfInterestJson) && dto.PointsOfInterestJson != "{}")
        {
            try
            {
                location.PointsOfInterest = JsonSerializer.Deserialize<Dictionary<string, string>>(dto.PointsOfInterestJson) ?? new Dictionary<string, string>();
            }
            catch
            {
                location.PointsOfInterest = new Dictionary<string, string>();
            }
        }

        // Deserialize exits from JSON if provided
        if (!string.IsNullOrEmpty(dto.ExitsJson) && dto.ExitsJson != "{}")
        {
            try
            {
                location.Exits = JsonSerializer.Deserialize<Dictionary<string, string>>(dto.ExitsJson) ?? new Dictionary<string, string>();
            }
            catch
            {
                location.Exits = new Dictionary<string, string>();
            }
        }

        return location;
    }

    #endregion
}