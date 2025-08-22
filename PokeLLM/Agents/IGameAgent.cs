using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace PokeLLM.Agents;

public interface IGameAgent
{
    string Id { get; }
    string Name { get; }
    string Instructions { get; }
    
    IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        ChatHistory chat,
        CancellationToken cancellationToken = default);
}