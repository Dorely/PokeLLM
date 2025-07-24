using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.LLM.Interfaces;

public interface ILLMProvider
{
    public Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);
    public IAsyncEnumerable<string> GetCompletionStreamingAsync(string prompt, CancellationToken cancellationToken = default);
    public IEmbeddingGenerator GetEmbeddingGenerator();
}