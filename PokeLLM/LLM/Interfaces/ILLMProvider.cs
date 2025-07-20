using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.GameState.Interfaces;

namespace PokeLLM.Game.LLM.Interfaces;

public interface ILLMProvider
{
    public void RegisterPlugins(IVectorStoreService vectorStoreService);
    public Task<string> GetCompletionAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default);
    public IAsyncEnumerable<string> GetCompletionStreamingAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default);
    public IEmbeddingGenerator GetEmbeddingGenerator();
    public ChatHistory CreateHistory();
}