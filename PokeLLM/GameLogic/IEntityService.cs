using PokeLLM.GameState.Models;
using System.Text.Json;

namespace PokeLLM.GameLogic;

/// <summary>
/// Generic entity service interface for managing game entities dynamically based on rulesets
/// </summary>
public interface IEntityService
{
    // Generic entity operations
    Task CreateEntity(string entityId, string entityType, object entityData);
    Task<T> GetEntity<T>(string entityId);
    Task<List<T>> GetEntitiesByType<T>(string entityType);
    Task UpdateEntity(string entityId, object entityData);
    Task DeleteEntity(string entityId);

    // Save/load operations
    Task SaveAllEntities();
    Task LoadAllEntities();

    // Entity queries
    Task<bool> EntityExists(string entityId);
    Task<List<string>> GetEntityIdsByType(string entityType);

    // Collection management
    Task<int> GetEntityCount();
    Task<int> GetEntityCountByType(string entityType);
    Task ClearAllEntities();
    Task ClearEntitiesByType(string entityType);
}