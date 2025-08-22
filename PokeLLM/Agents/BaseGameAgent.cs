using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace PokeLLM.Agents;

public abstract class BaseGameAgent : IGameAgent
{
    protected readonly Kernel _kernel;
    protected readonly IChatCompletionService _chatService;
    protected readonly ILogger _logger;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Instructions { get; }

    protected BaseGameAgent(Kernel kernel, ILogger logger)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    public virtual async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        ChatHistory chat,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent {AgentId} ({AgentName}) invoked with {MessageCount} messages", 
            Id, Name, chat.Count);

        var systemMessage = new ChatMessageContent(AuthorRole.System, Instructions);
        
        var agentChat = new ChatHistory();
        agentChat.Add(systemMessage);
        
        // Add existing conversation context
        foreach (var message in chat)
        {
            agentChat.Add(message);
        }

        var responses = await _chatService.GetChatMessageContentsAsync(
            agentChat,
            executionSettings: GetExecutionSettings(),
            cancellationToken: cancellationToken);
        
        var response = responses.FirstOrDefault() ?? new ChatMessageContent(AuthorRole.Assistant, "No response generated.");

        _logger.LogInformation("Agent {AgentId} response: {ResponseLength} characters", 
            Id, response.Content?.Length ?? 0);

        yield return response;
    }

    protected virtual PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.7,
                ["max_tokens"] = 1000
            }
        };
    }
}