using PokeLLM.GameLogic;

namespace PokeLLM.GameLogic.Services;

/// <summary>
/// Basic implementation of IEntityService for framework functionality
/// </summary>
public class BasicEntityService : IEntityService
{
    private readonly Dictionary<string, object> _entities = new();

    public async Task CreateEntity(string entityId, string entityType, object entityData)
    {
        await Task.Yield();
        _entities[entityId] = entityData;
    }

    public async Task<T> GetEntity<T>(string entityId)
    {
        await Task.Yield();
        if (_entities.TryGetValue(entityId, out var entity))
        {
            if (entity is T typedEntity)
                return typedEntity;
            
            // Try to convert if it's a compatible type
            try
            {
                return (T)entity;
            }
            catch
            {
                return default(T);
            }
        }
        return default(T);
    }

    public async Task<List<T>> GetEntitiesByType<T>(string entityType)
    {
        await Task.Yield();
        var result = new List<T>();
        
        foreach (var entity in _entities.Values)
        {
            if (entity is T typedEntity)
            {
                result.Add(typedEntity);
            }
        }
        
        return result;
    }

    public async Task UpdateEntity(string entityId, object entityData)
    {
        await Task.Yield();
        _entities[entityId] = entityData;
    }

    public async Task DeleteEntity(string entityId)
    {
        await Task.Yield();
        _entities.Remove(entityId);
    }

    public async Task SaveAllEntities()
    {
        await Task.Yield();
        // In a real implementation, this would persist to storage
    }

    public async Task LoadAllEntities()
    {
        await Task.Yield();
        // In a real implementation, this would load from storage
    }

    public async Task<bool> EntityExists(string entityId)
    {
        await Task.Yield();
        return _entities.ContainsKey(entityId);
    }

    public async Task<List<string>> GetEntityIdsByType(string entityType)
    {
        await Task.Yield();
        return _entities.Keys.ToList();
    }

    public async Task<int> GetEntityCount()
    {
        await Task.Yield();
        return _entities.Count;
    }

    public async Task<int> GetEntityCountByType(string entityType)
    {
        await Task.Yield();
        return _entities.Count; // Simplified for basic implementation
    }

    public async Task ClearAllEntities()
    {
        await Task.Yield();
        _entities.Clear();
    }

    public async Task ClearEntitiesByType(string entityType)
    {
        await Task.Yield();
        // Simplified for basic implementation
        _entities.Clear();
    }
}