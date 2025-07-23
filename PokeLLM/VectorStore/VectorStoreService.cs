using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

namespace PokeLLM.Game.VectorStore;

public class VectorStoreService : IVectorStoreService
{
    private readonly QdrantVectorStore _vectorStore;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private const string COLLECTION = "adventure";

    public VectorStoreService(ILLMProvider llmProvider, IOptions<QdrantConfig> options)
    {
        _embeddingGenerator = llmProvider.GetEmbeddingGenerator();
        _vectorStore = new QdrantVectorStore(
            new QdrantClient(options.Value.Host, options.Value.Port),
            ownsClient: true,
            new QdrantVectorStoreOptions
            {
                EmbeddingGenerator = _embeddingGenerator
            }
        );
    }

    public async Task<Guid> UpsertInformationAsync(string name, string description, string content, string type, string[] tags = null, string[] relatedEntries = null)
    {
        var collection = _vectorStore.GetCollection<Guid, VectorStoreModel>(COLLECTION);
        await collection.EnsureCollectionExistsAsync();

        var model = new VectorStoreModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Embedding = description + " " + content,
            Type = type,
            Content = content,
            Tags = tags ?? Array.Empty<string>(),
            RelatedEntries = relatedEntries ?? Array.Empty<string>(),
        };

        await collection.UpsertAsync(model);
        return model.Id;
    }

    public async Task<IEnumerable<VectorSearchResult<VectorStoreModel>>> SearchInformationAsync(string query, int limit = 5)
    {
        var collection = _vectorStore.GetCollection<Guid, VectorStoreModel>(COLLECTION);
        await collection.EnsureCollectionExistsAsync();
        var results = collection.SearchAsync<string>(query, limit);
        
        var resultCollection = new List<VectorSearchResult<VectorStoreModel>>();
        await foreach (var item in results)
        {
            resultCollection.Add(item);
        }

        return resultCollection.OrderByDescending(x => x.Score);
    }

}
