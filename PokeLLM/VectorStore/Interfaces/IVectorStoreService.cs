using Microsoft.Extensions.VectorData;
using OpenAI.VectorStores;

namespace PokeLLM.Game.VectorStore.Interfaces;

public interface IVectorStoreService
{
    public Task<Guid> AddOrUpdateEntityAsync(EntityVectorRecord entity);
    public Task<EntityVectorRecord> GetEntityByIdAsync(string entityId);
    public Task<Guid> AddOrUpdateLocationAsync(LocationVectorRecord location);
    public Task<Guid> AddOrUpdateLoreAsync(LoreVectorRecord lore);
    public Task<IEnumerable<VectorSearchResult<LoreVectorRecord>>> SearchLoreAsync(string query, double minRelevanceScore = 0.75, int limit = 3);
    public Task<Guid> LogNarrativeEventAsync(NarrativeLogVectorRecord narrativeLog);
    public Task<IEnumerable<VectorSearchResult<NarrativeLogVectorRecord>>> FindMemoriesAsync(string sessionId, string query, string[] involvedEntities, double minRelevanceScore = 0.75, int limit = 5);
}
