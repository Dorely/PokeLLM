using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.GameState.Models;
using System.Diagnostics;

namespace PokeLLM.Game.Orchestration;

public class ConversationHistoryManager : IConversationHistoryManager
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IPromptManager _promptManager;
    private readonly Dictionary<GamePhase, ChatHistory> _histories = new();
    
    // Configuration for chat history management
    private const int MAX_HISTORY_LENGTH = 20;
    private const int MESSAGES_TO_KEEP_RECENT = 6;

    public ConversationHistoryManager(
        IGameStateRepository gameStateRepository,
        IPromptManager promptManager)
    {
        _gameStateRepository = gameStateRepository;
        _promptManager = promptManager;
        
        // Initialize histories for all phases
        InitializeAllPhaseHistories();
    }

    public async Task<ChatHistory> GetHistoryAsync(GamePhase phase)
    {
        if (!_histories.ContainsKey(phase))
        {
            _histories[phase] = CreateHistory();
        }

        var history = _histories[phase];
        
        // Add system prompt if this is a new conversation
        if (history.Count == 0)
        {
            var systemPrompt = await _promptManager.LoadSystemPromptAsync(phase);
            history.AddSystemMessage(systemPrompt);
        }

        return history;
    }

    public async Task AddUserMessageAsync(GamePhase phase, string message)
    {
        var history = await GetHistoryAsync(phase);
        await SummarizeHistoryIfNeededAsync(phase);
        history.AddUserMessage(message);
    }

    public async Task AddAssistantMessageAsync(GamePhase phase, string message)
    {
        var history = await GetHistoryAsync(phase);
        history.AddAssistantMessage(message);
    }

    public async Task AddSystemMessageAsync(GamePhase phase, string message)
    {
        var history = await GetHistoryAsync(phase);
        history.AddSystemMessage(message);
    }

    public async Task SummarizeHistoryIfNeededAsync(GamePhase phase)
    {
        var history = _histories[phase];
        
        // Skip if history is too short or contains only system message
        if (history.Count <= MAX_HISTORY_LENGTH || history.Count <= 1)
            return;

        try
        {
            Debug.WriteLine($"Chat history getting long for phase {phase}, summarizing...");
            
            // Get messages to summarize (excluding system message and recent messages)
            var systemMessage = history.FirstOrDefault(m => m.Role == AuthorRole.System);
            var recentMessages = history.TakeLast(MESSAGES_TO_KEEP_RECENT).ToList();
            var messagesToSummarize = history
                .Skip(systemMessage != null ? 1 : 0) // Skip system message
                .Take(history.Count - (systemMessage != null ? 1 : 0) - MESSAGES_TO_KEEP_RECENT)
                .ToList();

            if (messagesToSummarize.Any())
            {
                // Create summary of old messages
                var summary = await SummarizeMessagesAsync(messagesToSummarize);
                
                // Store summary in vector store for future retrieval
                await StoreConversationChunkAsync(summary, messagesToSummarize);
                
                // Rebuild history with system message, summary, and recent messages
                history.Clear();
                
                if (systemMessage != null)
                {
                    history.Add(systemMessage);
                }
                
                // Add summary as a system message
                history.AddSystemMessage($"Previous conversation summary: {summary}");
                
                // Add recent messages
                foreach (var message in recentMessages)
                {
                    history.Add(message);
                }
                
                Debug.WriteLine($"History summarized for phase {phase}. Reduced from {messagesToSummarize.Count + recentMessages.Count + (systemMessage != null ? 1 : 0)} to {history.Count} messages.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to summarize chat history for phase {phase}: {ex.Message}");
        }
    }

    public async Task HandlePhaseTransitionAsync(GamePhase oldPhase, GamePhase newPhase)
    {
        try
        {
            if (!_histories.ContainsKey(oldPhase))
                return;

            var oldPhaseHistory = _histories[oldPhase];
            if (oldPhaseHistory != null && oldPhaseHistory.Count > 1) // More than just system message
            {
                Debug.WriteLine($"Generating conversation summary for phase transition from {oldPhase} to {newPhase}");
                
                // Generate conversation summary
                var conversationSummary = await GeneratePhaseTransitionSummaryAsync(oldPhaseHistory);
                
                // Update the game state with the conversation summary
                var gameState = await _gameStateRepository.LoadLatestStateAsync();
                if (gameState != null)
                {
                    gameState.PreviousPhaseConversationSummary = conversationSummary;
                    await _gameStateRepository.SaveStateAsync(gameState);
                    Debug.WriteLine($"Saved conversation summary for transition from {oldPhase} to {newPhase}");
                }
                
                // Store the conversation chunk in vector store for future reference
                await StorePhaseTransitionConversationAsync(conversationSummary, oldPhaseHistory, oldPhase, newPhase);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to handle phase transition summary: {ex.Message}");
        }
    }

    public ChatHistory CreateHistory()
    {
        return new ChatHistory();
    }

    private void InitializeAllPhaseHistories()
    {
        var allPhases = Enum.GetValues<GamePhase>();
        foreach (var phase in allPhases)
        {
            if (!_histories.ContainsKey(phase))
            {
                _histories[phase] = CreateHistory();
            }
        }
    }

    private async Task<string> SummarizeMessagesAsync(IEnumerable<ChatMessageContent> messages)
    {
        // TODO: This needs to be implemented with an LLM call
        // For now, create a simple summary
        var conversationText = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));
        return $"Summary of {messages.Count()} messages: [Conversation summary would be generated here]";
    }

    private async Task<string> GeneratePhaseTransitionSummaryAsync(ChatHistory history)
    {
        try
        {
            // Filter out system messages and get user/assistant conversation
            var conversationMessages = history
                .Where(m => m.Role == AuthorRole.User || m.Role == AuthorRole.Assistant)
                .ToList();

            if (!conversationMessages.Any())
                return string.Empty;

            // TODO: This needs to be implemented with an LLM call
            var conversationText = string.Join("\n", conversationMessages.Select(m => $"{m.Role}: {m.Content}"));
            return $"Phase transition summary: [Summary would be generated here for {conversationMessages.Count} messages]";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to generate phase transition summary: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task StoreConversationChunkAsync(string summary, IEnumerable<ChatMessageContent> originalMessages)
    {
        try
        {
            var chunkContent = string.Join("\n", originalMessages.Select(m => $"{m.Role}: {m.Content}"));
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            
            //TODO upload to vector store
            Debug.WriteLine($"Stored conversation chunk with {originalMessages.Count()} messages");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to store conversation chunk in vector store: {ex.Message}");
        }
    }

    private async Task StorePhaseTransitionConversationAsync(string summary, ChatHistory originalHistory, GamePhase oldPhase, GamePhase newPhase)
    {
        try
        {
            var conversationMessages = originalHistory
                .Where(m => m.Role == AuthorRole.User || m.Role == AuthorRole.Assistant)
                .ToList();
                
            var chunkContent = string.Join("\n", conversationMessages.Select(m => $"{m.Role}: {m.Content}"));
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            
            //TODO upload to vector store
            
            Debug.WriteLine($"Stored phase transition conversation in vector store: {oldPhase} -> {newPhase}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to store phase transition conversation in vector store: {ex.Message}");
        }
    }
}