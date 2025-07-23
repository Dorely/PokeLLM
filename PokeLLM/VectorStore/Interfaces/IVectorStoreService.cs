using Microsoft.Extensions.VectorData;

namespace PokeLLM.Game.VectorStore.Interfaces;

public interface IVectorStoreService
{
    public Task<Guid> UpsertInformationAsync(string name, string description, string content, string type, string[] tags = null, string[] relatedEntries = null);
    public Task<IEnumerable<VectorSearchResult<VectorStoreModel>>> SearchInformationAsync(string query, int limit = 5);
    
}
