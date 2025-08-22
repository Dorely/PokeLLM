using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using PokeLLM.Memory;

namespace PokeLLM.Agents;

/// <summary>
/// Enhanced agent thread that supports memory components following Semantic Kernel patterns
/// </summary>
public class MemoryEnabledAgentThread : IDisposable
{
    private readonly List<MemoryComponent> _memoryComponents;
    private readonly ChatHistory _chatHistory;
    private readonly string _sessionId;
    private readonly ILogger<MemoryEnabledAgentThread> _logger;
    private bool _disposed;

    public string SessionId => _sessionId;
    public IReadOnlyList<ChatMessageContent> Messages => _chatHistory;
    public IReadOnlyList<MemoryComponent> MemoryComponents => _memoryComponents.AsReadOnly();

    public MemoryEnabledAgentThread(string sessionId, ILogger<MemoryEnabledAgentThread> logger)
    {
        _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryComponents = new List<MemoryComponent>();
        _chatHistory = new ChatHistory();
    }

    /// <summary>
    /// Adds a memory component to this thread
    /// </summary>
    public async Task AddMemoryComponentAsync(MemoryComponent memoryComponent, CancellationToken cancellationToken = default)
    {
        if (memoryComponent == null)
            throw new ArgumentNullException(nameof(memoryComponent));

        _memoryComponents.Add(memoryComponent);
        await memoryComponent.OnThreadCreatedAsync(_sessionId, cancellationToken);
        
        _logger.LogInformation("Added memory component {ComponentType} to thread {SessionId}", 
            memoryComponent.GetType().Name, _sessionId);
    }

    /// <summary>
    /// Adds a message to the conversation and notifies memory components
    /// </summary>
    public async Task AddMessageAsync(ChatMessageContent message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _chatHistory.Add(message);

        // Notify all memory components of the new message
        var tasks = _memoryComponents.Select(component => 
            component.OnNewMessageAsync(_sessionId, message, cancellationToken));
        
        await Task.WhenAll(tasks);

        _logger.LogDebug("Added message from {Role} to thread {SessionId} and notified {ComponentCount} memory components", 
            message.Role, _sessionId, _memoryComponents.Count);
    }

    /// <summary>
    /// Gets additional context from memory components before agent invocation
    /// </summary>
    public async Task<string> GetMemoryContextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var contextTasks = _memoryComponents.Select(component =>
                component.OnModelInvokeAsync(_sessionId, _chatHistory.ToList(), cancellationToken));

            var contexts = await Task.WhenAll(contextTasks);
            
            var combinedContext = string.Join("\n", contexts.Where(ctx => !string.IsNullOrWhiteSpace(ctx)));
            
            if (!string.IsNullOrWhiteSpace(combinedContext))
            {
                _logger.LogDebug("Retrieved memory context for thread {SessionId}: {ContextLength} characters", 
                    _sessionId, combinedContext.Length);
            }

            return combinedContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving memory context for thread {SessionId}", _sessionId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates a chat history with memory context injected for agent invocation
    /// </summary>
    public async Task<ChatHistory> CreateAgentChatHistoryAsync(string agentInstructions, CancellationToken cancellationToken = default)
    {
        var agentChat = new ChatHistory();
        
        // Get memory context
        var memoryContext = await GetMemoryContextAsync(cancellationToken);
        
        // Combine agent instructions with memory context
        var enhancedInstructions = string.IsNullOrWhiteSpace(memoryContext) 
            ? agentInstructions 
            : $"{agentInstructions}\n\n{memoryContext}";

        agentChat.AddSystemMessage(enhancedInstructions);
        
        // Add conversation history
        foreach (var message in _chatHistory)
        {
            agentChat.Add(message);
        }

        return agentChat;
    }

    /// <summary>
    /// Clears the conversation history but keeps memory components
    /// </summary>
    public void ClearHistory()
    {
        _chatHistory.Clear();
        _logger.LogInformation("Cleared conversation history for thread {SessionId}", _sessionId);
    }

    /// <summary>
    /// Gets a copy of the current chat history
    /// </summary>
    public ChatHistory GetChatHistory()
    {
        var copy = new ChatHistory();
        foreach (var message in _chatHistory)
        {
            copy.Add(message);
        }
        return copy;
    }

    /// <summary>
    /// Provides access to the underlying chat history for direct manipulation
    /// Use with caution - prefer AddMessageAsync for proper memory component notification
    /// </summary>
    public ChatHistory DirectChatHistory => _chatHistory;

    public void Dispose()
    {
        if (_disposed) return;

        // Notify memory components of thread deletion
        var deletionTasks = _memoryComponents.Select(component =>
            component.OnThreadDeletedAsync(_sessionId));

        try
        {
            Task.WaitAll(deletionTasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying memory components of thread deletion for session {SessionId}", _sessionId);
        }

        _memoryComponents.Clear();
        _chatHistory.Clear();
        _disposed = true;

        _logger.LogInformation("Disposed memory-enabled agent thread for session {SessionId}", _sessionId);
    }
}

/// <summary>
/// Factory for creating memory-enabled agent threads
/// </summary>
public class MemoryEnabledAgentThreadFactory
{
    private readonly MemoryComponentFactory _memoryComponentFactory;
    private readonly ILogger<MemoryEnabledAgentThread> _threadLogger;

    public MemoryEnabledAgentThreadFactory(
        MemoryComponentFactory memoryComponentFactory,
        ILogger<MemoryEnabledAgentThread> threadLogger)
    {
        _memoryComponentFactory = memoryComponentFactory;
        _threadLogger = threadLogger;
    }

    /// <summary>
    /// Creates a new thread with standard memory components for the Narrator Agent
    /// </summary>
    public async Task<MemoryEnabledAgentThread> CreateNarratorThreadAsync(
        string sessionId, 
        CancellationToken cancellationToken = default)
    {
        var thread = new MemoryEnabledAgentThread(sessionId, _threadLogger);
        
        // Add memory components that are relevant for the Narrator
        await thread.AddMemoryComponentAsync(_memoryComponentFactory.CreateUserFactsMemory(), cancellationToken);
        await thread.AddMemoryComponentAsync(_memoryComponentFactory.CreateEventSummaryMemory(), cancellationToken);
        
        return thread;
    }

    /// <summary>
    /// Creates a new thread with memory components for the GM Supervisor Agent
    /// </summary>
    public async Task<MemoryEnabledAgentThread> CreateSupervisorThreadAsync(
        string sessionId, 
        CancellationToken cancellationToken = default)
    {
        var thread = new MemoryEnabledAgentThread(sessionId, _threadLogger);
        
        // Add memory components relevant for coordination and oversight
        await thread.AddMemoryComponentAsync(_memoryComponentFactory.CreateUserFactsMemory(), cancellationToken);
        await thread.AddMemoryComponentAsync(_memoryComponentFactory.CreateEventSummaryMemory(), cancellationToken);
        
        return thread;
    }

    /// <summary>
    /// Creates a basic thread without memory components (for MechanicsAgent)
    /// </summary>
    public MemoryEnabledAgentThread CreateBasicThread(string sessionId)
    {
        // MechanicsAgent should not use memory to maintain deterministic behavior
        return new MemoryEnabledAgentThread(sessionId, _threadLogger);
    }
}