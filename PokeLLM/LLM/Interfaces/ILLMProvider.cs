using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.LLM.Interfaces;

public interface ILLMProvider
{
    public void RegisterPlugins(IVectorStoreService vectorStoreService, IGameStateRepository gameStateRepository);
    public Task<string> GetCompletionAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default);
    public IAsyncEnumerable<string> GetCompletionStreamingAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default);
    public IEmbeddingGenerator GetEmbeddingGenerator();
    public ChatHistory CreateHistory();
    public Task RefreshPhaseAsync(); // New method to refresh the current phase
}