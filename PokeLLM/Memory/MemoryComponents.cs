using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game.VectorStore;
using PokeLLM.Game.VectorStore.Models;
using System.Text.Json;

namespace PokeLLM.Memory;

/// <summary>
/// Base abstract class for memory components that can be attached to agent threads
/// </summary>
public abstract class MemoryComponent
{
    protected readonly Kernel _kernel;
    protected readonly IVectorStoreService _vectorStore;
    protected readonly ILogger _logger;

    protected MemoryComponent(Kernel kernel, IVectorStoreService vectorStore, ILogger logger)
    {
        _kernel = kernel;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    /// <summary>
    /// Called when a new message is added to the conversation
    /// </summary>
    public abstract Task OnNewMessageAsync(string sessionId, ChatMessageContent message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the agent is about to be invoked to provide additional context
    /// </summary>
    public abstract Task<string> OnModelInvokeAsync(string sessionId, ICollection<ChatMessageContent> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a conversation thread is created
    /// </summary>
    public virtual Task OnThreadCreatedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Memory component {ComponentType} initialized for session {SessionId}", 
            GetType().Name, sessionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a conversation thread is deleted
    /// </summary>
    public virtual Task OnThreadDeletedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Memory component {ComponentType} cleaned up for session {SessionId}", 
            GetType().Name, sessionId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Memory component that extracts and stores user facts from conversations
/// </summary>
public class UserFactsMemoryComponent : MemoryComponent
{
    private const int MaxRecentMessages = 10;
    private const double RelevanceThreshold = 0.75;

    public UserFactsMemoryComponent(Kernel kernel, IVectorStoreService vectorStore, ILogger<UserFactsMemoryComponent> logger)
        : base(kernel, vectorStore, logger)
    {
    }

    public override async Task OnNewMessageAsync(string sessionId, ChatMessageContent message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only process user messages for fact extraction
            if (message.Role != AuthorRole.User)
                return;

            // Extract potential facts from the user message
            var facts = await ExtractUserFactsAsync(message.Content!, cancellationToken);
            
            // Store each fact as a separate record
            foreach (var fact in facts)
            {
                if (!string.IsNullOrWhiteSpace(fact))
                {
                    await StoreUserFactAsync(sessionId, fact, message.Content!, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing new message for user facts in session {SessionId}", sessionId);
        }
    }

    public override async Task<string> OnModelInvokeAsync(string sessionId, ICollection<ChatMessageContent> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the latest user message for context
            var latestUserMessage = messages
                .Where(m => m.Role == AuthorRole.User)
                .LastOrDefault()?.Content;

            if (string.IsNullOrWhiteSpace(latestUserMessage))
                return string.Empty;

            // Search for relevant user facts
            var relevantFacts = await FindRelevantUserFactsAsync(sessionId, latestUserMessage, cancellationToken);

            if (!relevantFacts.Any())
                return string.Empty;

            // Format facts as additional context
            var factsContext = string.Join("\n", relevantFacts.Select(f => $"- {f}"));
            
            return $"""
                
                REMEMBERED USER INFORMATION:
                {factsContext}
                
                Use this information to provide more personalized and contextually relevant responses.
                """;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user facts for session {SessionId}", sessionId);
            return string.Empty;
        }
    }

    private async Task<IEnumerable<string>> ExtractUserFactsAsync(string userMessage, CancellationToken cancellationToken)
    {
        try
        {
            var extractionPrompt = $"""
                Extract factual information about the user from this message. 
                Focus on personal details, preferences, goals, background information, and game-specific choices.
                Return each fact as a separate line. If no facts are present, return an empty response.
                
                Message: "{userMessage}"
                
                Facts (one per line):
                """;

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a fact extraction assistant. Extract only clear, factual information about the user.");
            chatHistory.AddUserMessage(extractionPrompt);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var responses = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings: new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.1, ["max_tokens"] = 300 } },
                cancellationToken: cancellationToken);

            var response = responses.FirstOrDefault()?.Content ?? string.Empty;
            
            return response
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(fact => fact.Trim().TrimStart('-', ' '))
                .Where(fact => !string.IsNullOrWhiteSpace(fact) && fact.Length > 5)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting user facts from message");
            return Enumerable.Empty<string>();
        }
    }

    private async Task StoreUserFactAsync(string sessionId, string fact, string originalMessage, CancellationToken cancellationToken)
    {
        try
        {
            var factRecord = new LoreVectorRecord
            {
                Id = Guid.NewGuid(),
                EntryId = $"user_fact_{sessionId}_{Guid.NewGuid()}",
                EntryType = "UserFact",
                Title = "User Information",
                Content = fact,
                Tags = new[] { "user", "personal", sessionId },
                Embedding = "" // Will be generated by vector store
            };

            await _vectorStore.AddOrUpdateLoreAsync(factRecord);
            
            _logger.LogDebug("Stored user fact for session {SessionId}: {Fact}", sessionId, fact);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing user fact for session {SessionId}", sessionId);
        }
    }

    private async Task<IEnumerable<string>> FindRelevantUserFactsAsync(string sessionId, string query, CancellationToken cancellationToken)
    {
        try
        {
            var searchResults = await _vectorStore.SearchLoreAsync(query, RelevanceThreshold, 5);
            
            return searchResults
                .Where(r => r.Record.Tags.Contains(sessionId) && r.Record.EntryType == "UserFact")
                .OrderByDescending(r => r.Score)
                .Select(r => r.Record.Content)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for relevant user facts in session {SessionId}", sessionId);
            return Enumerable.Empty<string>();
        }
    }
}

/// <summary>
/// Memory component that manages event summaries and narrative compression
/// </summary>
public class EventSummaryMemoryComponent : MemoryComponent
{
    private const int MessagesBeforeCompression = 20;
    private const int MaxRecentEvents = 8;
    private const double RelevanceThreshold = 0.70;

    public EventSummaryMemoryComponent(Kernel kernel, IVectorStoreService vectorStore, ILogger<EventSummaryMemoryComponent> logger)
        : base(kernel, vectorStore, logger)
    {
    }

    public override async Task OnNewMessageAsync(string sessionId, ChatMessageContent message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Log all significant interactions for potential summarization
            if (message.Role == AuthorRole.Assistant && !string.IsNullOrWhiteSpace(message.Content))
            {
                await LogNarrativeEventAsync(sessionId, message.Content!, cancellationToken);
            }

            // Check if we need to perform compression
            await CheckAndPerformCompressionAsync(sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing new message for event summary in session {SessionId}", sessionId);
        }
    }

    public override async Task<string> OnModelInvokeAsync(string sessionId, ICollection<ChatMessageContent> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the latest user message for context
            var latestUserMessage = messages
                .Where(m => m.Role == AuthorRole.User)
                .LastOrDefault()?.Content;

            if (string.IsNullOrWhiteSpace(latestUserMessage))
                return string.Empty;

            // Find relevant past events
            var relevantEvents = await FindRelevantEventsAsync(sessionId, latestUserMessage, cancellationToken);

            if (!relevantEvents.Any())
                return string.Empty;

            // Format events as narrative context
            var eventsContext = string.Join("\n\n", relevantEvents.Select((evt, index) => 
                $"PAST EVENT {index + 1}: {evt}"));
            
            return $"""
                
                RELEVANT PAST EVENTS:
                {eventsContext}
                
                Reference these events to maintain story continuity and character development.
                """;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving event summaries for session {SessionId}", sessionId);
            return string.Empty;
        }
    }

    private async Task LogNarrativeEventAsync(string sessionId, string content, CancellationToken cancellationToken)
    {
        try
        {
            var eventRecord = new NarrativeLogVectorRecord
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                GameTurnNumber = await GetCurrentTurnNumberAsync(sessionId),
                EventType = "NarrativeEvent",
                EventSummary = await SummarizeEventAsync(content, cancellationToken),
                FullTranscript = content,
                InvolvedEntities = await ExtractEntitiesAsync(content, cancellationToken),
                LocationId = "unknown", // Will be enhanced when location tracking is implemented
                Embedding = "" // Will be generated by vector store
            };

            await _vectorStore.LogNarrativeEventAsync(eventRecord);
            
            _logger.LogDebug("Logged narrative event for session {SessionId} turn {Turn}", 
                sessionId, eventRecord.GameTurnNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging narrative event for session {SessionId}", sessionId);
        }
    }

    private async Task<string> SummarizeEventAsync(string content, CancellationToken cancellationToken)
    {
        try
        {
            var summaryPrompt = $"""
                Summarize this Pokemon RPG event in 1-2 concise sentences. 
                Focus on key actions, outcomes, and important story developments.
                
                Event: {content}
                
                Summary:
                """;

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a narrative summarization assistant for a Pokemon RPG. Create brief, informative summaries.");
            chatHistory.AddUserMessage(summaryPrompt);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var responses = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings: new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.2, ["max_tokens"] = 150 } },
                cancellationToken: cancellationToken);

            return responses.FirstOrDefault()?.Content?.Trim() ?? content.Substring(0, Math.Min(content.Length, 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing event");
            return content.Substring(0, Math.Min(content.Length, 200));
        }
    }

    private async Task<string[]> ExtractEntitiesAsync(string content, CancellationToken cancellationToken)
    {
        try
        {
            var extractionPrompt = $"""
                Extract key entities from this Pokemon RPG content. 
                Include Pokemon names, character names, location names, and important items.
                Return as a comma-separated list.
                
                Content: {content}
                
                Entities:
                """;

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are an entity extraction assistant. Extract only the most important Pokemon, characters, locations, and items.");
            chatHistory.AddUserMessage(extractionPrompt);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var responses = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings: new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.1, ["max_tokens"] = 200 } },
                cancellationToken: cancellationToken);

            var response = responses.FirstOrDefault()?.Content ?? string.Empty;
            
            return response
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(entity => entity.Trim())
                .Where(entity => !string.IsNullOrWhiteSpace(entity))
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities");
            return Array.Empty<string>();
        }
    }

    private async Task<IEnumerable<string>> FindRelevantEventsAsync(string sessionId, string query, CancellationToken cancellationToken)
    {
        try
        {
            var searchResults = await _vectorStore.FindMemoriesAsync(sessionId, query, Array.Empty<string>(), RelevanceThreshold, MaxRecentEvents);
            
            return searchResults
                .OrderByDescending(r => r.Score)
                .Select(r => r.Record.EventSummary)
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for relevant events in session {SessionId}", sessionId);
            return Enumerable.Empty<string>();
        }
    }

    private async Task CheckAndPerformCompressionAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            // This is a placeholder for compression logic
            // In a full implementation, you would:
            // 1. Count recent messages/events
            // 2. If threshold exceeded, summarize older events
            // 3. Replace detailed events with compressed summaries
            // 4. Maintain recent events for immediate context

            _logger.LogDebug("Compression check completed for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compression check for session {SessionId}", sessionId);
        }
    }

    private async Task<int> GetCurrentTurnNumberAsync(string sessionId)
    {
        try
        {
            // Simple implementation - count existing events for this session
            // In a full implementation, this would come from the game state
            return Environment.TickCount & 0xFFFF; // Simplified turn counter
        }
        catch
        {
            return 1;
        }
    }
}

/// <summary>
/// Factory for creating memory components
/// </summary>
public class MemoryComponentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MemoryComponentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public UserFactsMemoryComponent CreateUserFactsMemory()
    {
        return new UserFactsMemoryComponent(
            _serviceProvider.GetRequiredService<Kernel>(),
            _serviceProvider.GetRequiredService<IVectorStoreService>(),
            _serviceProvider.GetRequiredService<ILogger<UserFactsMemoryComponent>>());
    }

    public EventSummaryMemoryComponent CreateEventSummaryMemory()
    {
        return new EventSummaryMemoryComponent(
            _serviceProvider.GetRequiredService<Kernel>(),
            _serviceProvider.GetRequiredService<IVectorStoreService>(),
            _serviceProvider.GetRequiredService<ILogger<EventSummaryMemoryComponent>>());
    }
}

/// <summary>
/// Service extensions for registering memory components
/// </summary>
public static class MemoryServiceExtensions
{
    public static IServiceCollection AddMemoryComponents(this IServiceCollection services)
    {
        services.AddSingleton<MemoryComponentFactory>();
        services.AddTransient<UserFactsMemoryComponent>();
        services.AddTransient<EventSummaryMemoryComponent>();
        
        return services;
    }
}