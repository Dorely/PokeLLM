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

    public async Task<QdrantCollection<Guid, GameHistoryModel>> GetGameHistory()
    {
        var collection = _vectorStore.GetCollection<Guid, GameHistoryModel>("game_history");
        await collection.EnsureCollectionExistsAsync();

        return collection;
    }

    public async Task Upsert(QdrantCollection<Guid, GameHistoryModel> collection, string entryName, string description, string[] tags = null)
    {
        await collection.UpsertAsync(
            new GameHistoryModel
            {
                EntryId = Guid.NewGuid(),
                EntryName = entryName,
                Description = description,
                DescriptionEmbedding = description,
                Tags = tags ?? Array.Empty<string>()
            }
        );
    }
}
