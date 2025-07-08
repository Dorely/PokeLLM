using Microsoft.SemanticKernel.Connectors.Qdrant;
using PokeLLM.Game.Data;

namespace PokeLLM.Game.VectorStore.Interfaces;

public interface IVectorStoreService
{
    public Task<QdrantCollection<Guid, GameHistoryModel>> GetGameHistory();
    public Task Upsert(QdrantCollection<Guid, GameHistoryModel> collection, string entryName, string description, string[] tags = null);
}
