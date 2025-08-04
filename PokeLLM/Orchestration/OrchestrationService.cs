using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace PokeLLM.Game.Orchestration;
public interface IOrchestrationService
{
    public IAsyncEnumerable<string> OrchestrateAsync(string inputMessage, CancellationToken cancellationToken = default);
    public Task<string> RunContextManagement(string directive, CancellationToken cancellationToken = default);
}

public class OrchestrationService : IOrchestrationService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private Dictionary<string, Kernel> _kernels;
    private Dictionary<string, ChatHistory> _histories;
    
    private GamePhase _currentPhase;

    public OrchestrationService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
        _kernels = new Dictionary<string, Kernel>();
        _histories = new Dictionary<string, ChatHistory>();
        SetupPromptsAndPlugins();
    }

    private void SetupPromptsAndPlugins()
    {
        // Initialize kernels and chat histories for each game phase using DI
        SetupGamePhaseKernel<GameCreationPhasePlugin>("GameCreation");
        SetupGamePhaseKernel<CharacterCreationPhasePlugin>("CharacterCreation");
        SetupGamePhaseKernel<WorldGenerationPhasePlugin>("WorldGeneration");
        SetupGamePhaseKernel<ExplorationPhasePlugin>("Exploration");
        SetupGamePhaseKernel<CombatPhasePlugin>("Combat");
        SetupGamePhaseKernel<LevelUpPhasePlugin>("LevelUp");
        
        // Setup context gathering subroutine kernel for lightweight context assembly
        SetupGamePhaseKernel<ContextGatheringPlugin>("ContextGathering");
        
        // Setup context management subroutine kernel for comprehensive context management
        SetupGamePhaseKernel<ContextManagementPlugin>("ContextManagement");
        
        // Setup chat management kernel for general chat functions
        SetupGamePhaseKernel<ChatManagementPlugin>("ChatManagement");
    }

    private void SetupGamePhaseKernel<T>(string phaseName) where T : class
    {
        try
        {
            var kernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
            kernel.Plugins.AddFromType<T>(phaseName, _serviceProvider);
            _kernels[phaseName] = kernel;
            _histories[phaseName] = new ChatHistory();
            var systemprompt = LoadSystemPromptAsync(phaseName).GetAwaiter().GetResult();
            _histories[phaseName].AddSystemMessage(systemprompt);

            Debug.WriteLine($"[OrchestrationService] Setup completed for phase: {phaseName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error setting up {phaseName}: {ex.Message}");
            throw;
        }
    }

    public async IAsyncEnumerable<string> OrchestrateAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        GameStateModel gameState = null;
        GamePhase initialPhase = GamePhase.GameCreation; // Initialize with default
        Kernel kernel = null;
        ChatHistory history = null;
        string contextResult = null;
        StringBuilder responseBuilder = null;
        bool setupSuccessful = false;
        string errorMessage = null;
        
        try
        {
            // Load current game state to determine phase and track changes and to increment turn number
            gameState = await _gameStateRepository.LoadLatestStateAsync();
            gameState.GameTurnNumber++;
            await _gameStateRepository.SaveStateAsync(gameState);
            initialPhase = gameState.CurrentPhase;
            
            // Update current phase tracker if needed
            if (_currentPhase != initialPhase)
            {
                Debug.WriteLine($"[OrchestrationService] Phase synchronization: {_currentPhase} -> {initialPhase}");
                _currentPhase = initialPhase;
            }
            
            // Get the correct kernel and history for the current phase
            var phaseKernelName = GetPhaseKernelName(_currentPhase);
            kernel = _kernels[phaseKernelName];
            history = _histories[phaseKernelName];
            
            Debug.WriteLine($"[OrchestrationService] Processing input for phase: {_currentPhase}");
            
            // Run context gathering if NOT in GameCreation or WorldGeneration phases
            if (_currentPhase != GamePhase.GameCreation && _currentPhase != GamePhase.WorldGeneration)
            {
                Debug.WriteLine($"[OrchestrationService] Running context gathering for phase: {_currentPhase}");
                contextResult = await RunContextGathering(history, cancellationToken);
                
                // Add the gathered context as a system message if successful
                if (!string.IsNullOrEmpty(contextResult) && !contextResult.Contains("Context gathering failed"))
                {
                    history.AddSystemMessage($"TURN NUMBER: {gameState.GameTurnNumber}; GATHERED CONTEXT: {contextResult}");
                    Debug.WriteLine($"[OrchestrationService] Added context: {contextResult.Length} characters");
                }
                else if (!string.IsNullOrEmpty(contextResult))
                {
                    Debug.WriteLine($"[OrchestrationService] Context gathering failed: {contextResult}");
                }
            }
            else
            {
                Debug.WriteLine($"[OrchestrationService] Skipping context gathering for phase: {_currentPhase}");
            }
            
            // Add the user's input message to the history
            history.AddUserMessage(inputMessage);
            
            // Prepare for streaming response
            responseBuilder = new StringBuilder();
            setupSuccessful = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in OrchestrateAsync setup: {ex.Message}");
            errorMessage = $"I encountered an error while processing your request: {ex.Message}. Please try again.";
        }
        
        // Handle setup error
        if (!setupSuccessful)
        {
            yield return errorMessage;
            yield break;
        }
        
        // Stream the response from the correct kernel
        await foreach (var chunk in ExecutePromptStreamingAsync(history, kernel, cancellationToken))
        {
            var chunkText = chunk.ToString();
            responseBuilder.Append(chunkText);
            yield return chunkText;
        }
        
        // Post-processing: Check for phase transitions and manage context
        string phaseTransitionMessage = null;
        bool hasPhaseTransition = false;
        GamePhase finalPhase = initialPhase;
        
        try
        {
            var fullResponse = responseBuilder.ToString();
            
            // Reload game state to check if phase was changed by plugin functions
            var updatedGameState = await _gameStateRepository.LoadLatestStateAsync();
            finalPhase = updatedGameState.CurrentPhase;
            
            // Check if phase has changed during processing
            if (initialPhase != finalPhase)
            {
                Debug.WriteLine($"[OrchestrationService] Phase transition detected: {initialPhase} -> {finalPhase}");
                
                // Update our current phase tracker
                _currentPhase = finalPhase;
                
                // Run Context Management subroutine for the phase transition
                var contextManagementDirective = $@"Phase transition completed from {initialPhase} to {finalPhase}.
Please ensure all game state is consistent and properly managed for the new phase.
Review the recent conversation and game state changes to maintain continuity.

Recent Response: {fullResponse}

Please validate and update:
1. Entity consistency across systems
2. Game state integrity for the new phase
3. Context synchronization between vector DB and game state
4. Any narrative events that should be logged";
                
                var contextManagementResult = await RunContextManagement(contextManagementDirective, cancellationToken);
                
                // Add the context management result to the new phase's history
                var newPhaseKernelName = GetPhaseKernelName(finalPhase);
                var newPhaseHistory = _histories[newPhaseKernelName];
                newPhaseHistory.AddSystemMessage($"PHASE TRANSITION CONTEXT: {contextManagementResult}");
                
                Debug.WriteLine($"[OrchestrationService] Context management completed for phase transition to {finalPhase}");
                
                // Prepare for recursive call
                phaseTransitionMessage = CreatePhaseTransitionMessage(initialPhase, finalPhase, updatedGameState.PhaseChangeSummary);
                hasPhaseTransition = true;
                
                Debug.WriteLine($"[OrchestrationService] Prepared recursive call to new phase {finalPhase}");
            }
            else
            {
                Debug.WriteLine($"[OrchestrationService] No phase transition detected, staying in {_currentPhase}");
            }
            
            // Save the final game state
            await _gameStateRepository.SaveStateAsync(updatedGameState);
            
            Debug.WriteLine($"[OrchestrationService] Orchestration completed. Response length: {fullResponse.Length}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in OrchestrateAsync post-processing: {ex.Message}");
            // Note: Continue even if there's an error in post-processing
        }
        
        // Handle phase transition outside of try-catch to avoid yield return restrictions
        if (hasPhaseTransition)
        {
            // Yield a separator to indicate phase transition
            yield return $"\n\n--- Phase Transition: {initialPhase} ? {finalPhase} ---\n\n";
            
            Debug.WriteLine($"[OrchestrationService] Recursively calling new phase {finalPhase} with transition message");
            
            // Recursively call OrchestrateAsync with the new phase continuation message
            await foreach (var chunk in OrchestrateAsync(phaseTransitionMessage, cancellationToken))
            {
                yield return chunk;
            }
        }
    }
    
    /// <summary>
    /// Creates an appropriate continuation message for the new phase based on the transition context
    /// </summary>
    private string CreatePhaseTransitionMessage(GamePhase fromPhase, GamePhase toPhase, string phaseChangeSummary)
    {
        var baseMessage = $"The adventure has just transitioned from {fromPhase} to {toPhase} phase.";
        
        if (!string.IsNullOrEmpty(phaseChangeSummary))
        {
            baseMessage += $" {phaseChangeSummary}";
        }

        return toPhase switch
        {
            GamePhase.CharacterCreation => $"{baseMessage} Please continue by helping the player create their character and choose their starting abilities.",
            
            GamePhase.WorldGeneration => $"{baseMessage} Please continue by generating the world and setting up the initial adventure location.",
            
            GamePhase.Exploration => $"{baseMessage} Please continue the adventure in exploration mode. The player can now explore the world, interact with NPCs, and discover new locations.",
            
            GamePhase.Combat => $"{baseMessage} Please continue managing the combat encounter that has just begun. Describe the battle situation and guide the player through their combat options.",
            
            GamePhase.LevelUp => $"{baseMessage} Please guide the player through the level up process, allowing them to improve their abilities and grow stronger.",
            
            GamePhase.GameCreation => $"{baseMessage} Please help set up a new game session.",
            
            _ => $"{baseMessage} Please continue the adventure in the new {toPhase} phase."
        };
    }

    private string GetPhaseKernelName(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.GameCreation => "GameCreation",
            GamePhase.CharacterCreation => "CharacterCreation",
            GamePhase.WorldGeneration => "WorldGeneration",
            GamePhase.Exploration => "Exploration",
            GamePhase.Combat => "Combat",
            GamePhase.LevelUp => "LevelUp",
            _ => "GameCreation" // Default fallback
        };
    }

    private async Task<string> ExecutePromptAsync(ChatHistory history, Kernel kernel, CancellationToken cancellationToken = default)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        var result = await chatService.GetChatMessageContentAsync(
            history,
            executionSettings,
            kernel,
            cancellationToken
        );
        
        var response = result.ToString();
        await AddResponseToHistory(response, history);
        
        return response;
    }

    private async Task<string> ExecuteSubroutinePromptAsync(ChatHistory history, Kernel kernel, CancellationToken cancellationToken = default)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        var result = await chatService.GetChatMessageContentAsync(
            history,
            executionSettings,
            kernel,
            cancellationToken
        );
        
        var response = result.ToString();
        
        return response;
    }

    private async IAsyncEnumerable<string> ExecutePromptStreamingAsync(ChatHistory history, Kernel kernel, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);
        
        var responseBuilder = new StringBuilder();
        
        // Pre-validate and clean the history if needed
        var historyToUse = await ValidateAndCleanHistory(history, chatService, executionSettings, kernel, cancellationToken);
        
        var result = chatService.GetStreamingChatMessageContentsAsync(
            historyToUse,
            executionSettings,
            kernel,
            cancellationToken
        );
        
        await foreach (var chunk in result)
        {
            var chunkText = chunk.ToString();
            responseBuilder.Append(chunkText);
            yield return chunkText;
        }
        
        await AddResponseToHistory(responseBuilder.ToString(), history);
    }

    /// <summary>
    /// Validates the chat history and cleans it if tool sequence errors are detected
    /// </summary>
    private async Task<ChatHistory> ValidateAndCleanHistory(ChatHistory history, IChatCompletionService chatService, PromptExecutionSettings executionSettings, Kernel kernel, CancellationToken cancellationToken)
    {
        try
        {
            // Test the history with a minimal non-streaming call to detect tool sequence issues
            var testResult = await chatService.GetChatMessageContentAsync(
                history,
                executionSettings,
                kernel,
                cancellationToken
            );
            
            // If we get here, the history is valid
            return history;
        }
        catch (Exception ex) when (ex.Message.Contains("tool") && ex.Message.Contains("must be a response to a preceeding message"))
        {
            Debug.WriteLine($"[OrchestrationService] Tool call sequence error detected during validation: {ex.Message}");
            Debug.WriteLine($"[OrchestrationService] Cleaning chat history...");
            
            var cleanedHistory = CleanChatHistoryToolSequences(history);
            
            try
            {
                // Test the cleaned history
                var testCleanedResult = await chatService.GetChatMessageContentAsync(
                    cleanedHistory,
                    executionSettings,
                    kernel,
                    cancellationToken
                );
                
                Debug.WriteLine($"[OrchestrationService] Cleaned history validation succeeded");
                return cleanedHistory;
            }
            catch (Exception cleanEx)
            {
                Debug.WriteLine($"[OrchestrationService] Cleaned history validation failed: {cleanEx.Message}");
                // Return the cleaned history anyway, as it's the best we can do
                return cleanedHistory;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Unexpected error during history validation: {ex.Message}");
            // Return original history for other types of errors
            return history;
        }
    }

    /// <summary>
    /// Cleans chat history by removing orphaned tool messages that don't have proper tool_calls predecessors
    /// </summary>
    private ChatHistory CleanChatHistoryToolSequences(ChatHistory originalHistory)
    {
        var cleanedHistory = new ChatHistory();
        var messages = originalHistory.ToList();
        
        for (int i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            
            // Always include system and user messages
            if (message.Role == AuthorRole.System || message.Role == AuthorRole.User)
            {
                cleanedHistory.Add(message);
                continue;
            }
            
            // For tool messages, check if they have a proper predecessor
            if (message.Role == AuthorRole.Tool)
            {
                // Look backwards for an assistant message that might contain tool calls
                bool hasProperPredecessor = false;
                for (int j = i - 1; j >= 0; j--)
                {
                    var prevMessage = messages[j];
                    if (prevMessage.Role == AuthorRole.Assistant)
                    {
                        // Assume this is the predecessor and include both
                        hasProperPredecessor = true;
                        break;
                    }
                    else if (prevMessage.Role == AuthorRole.User || prevMessage.Role == AuthorRole.System)
                    {
                        // Hit a non-assistant message, so no proper predecessor
                        break;
                    }
                }
                
                if (hasProperPredecessor)
                {
                    cleanedHistory.Add(message);
                }
                else
                {
                    var contentLength = message.Content?.Length ?? 0;
                    var contentPreview = contentLength > 100 ? message.Content?.Substring(0, 100) : message.Content ?? "";
                    Debug.WriteLine($"[OrchestrationService] Removing orphaned tool message: {contentPreview}...");
                }
            }
            else
            {
                // Assistant and other messages
                cleanedHistory.Add(message);
            }
        }
        
        Debug.WriteLine($"[OrchestrationService] Cleaned history: {originalHistory.Count} -> {cleanedHistory.Count} messages");
        return cleanedHistory;
    }

    private async Task AddResponseToHistory(string response, ChatHistory history)
    {
        await Task.Yield();
        history.AddAssistantMessage(response);

        // Check if history is getting large (rough estimate: 20+ messages or total character count exceeds threshold)
        const int maxMessages = 20;
        const int maxCharacters = 50000; // Approximate token limit consideration
        
        var totalCharacters = history.Sum(message => message.Content?.Length ?? 0);
        
        if (history.Count > maxMessages || totalCharacters > maxCharacters)
        {
            Debug.WriteLine($"[OrchestrationService] Chat history is large ({history.Count} messages, {totalCharacters} chars). Running management...");
            await RunChatHistoryManagement(history);
        }
    }

    public async Task<string> RunContextGathering(ChatHistory history, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the ContextGathering kernel and create a new history for this lightweight subroutine
            var contextGatheringKernel = _kernels["ContextGathering"];
            var contextGatheringHistory = new ChatHistory();
            
            // Load the system prompt for context gathering
            var systemPrompt = await LoadSystemPromptAsync("ContextGathering");
            contextGatheringHistory.AddSystemMessage(systemPrompt);
            
            // Get current game state to provide context
            var gameState = await _gameStateRepository.LoadLatestStateAsync();
            
            // Extract the most recent user message from the chat history to understand what context is needed
            var recentMessages = history.TakeLast(3).ToList(); // Get last 3 messages for context
            var recentMessagesJson = JsonSerializer.Serialize(
                recentMessages.Select(msg => new { Role = msg.Role.ToString(), Content = msg.Content }).ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            
            // Extract key context information from game state
            var contextSummary = new
            {
                CurrentLocation = gameState.CurrentLocationId,
                PlayerLocation = gameState.WorldLocations.ContainsKey(gameState.CurrentLocationId) 
                    ? gameState.WorldLocations[gameState.CurrentLocationId].Name 
                    : "Unknown",
                PresentNpcs = gameState.WorldLocations.ContainsKey(gameState.CurrentLocationId) 
                    ? gameState.WorldLocations[gameState.CurrentLocationId].PresentNpcIds 
                    : new List<string>(),
                RecentEvents = gameState.RecentEvents,
                AdventureSummary = gameState.AdventureSummary
            };
            
            var contextSummaryJson = JsonSerializer.Serialize(contextSummary, new JsonSerializerOptions { WriteIndented = true });
            
            // Create lightweight directive for the context gathering subroutine
            var contextGatheringDirective = $@"CONTEXT GATHERING REQUEST:
You need to gather relevant contextual information to help the Game Master respond appropriately to the recent conversation.

RECENT CONVERSATION:
{recentMessagesJson}

CURRENT GAME STATE SUMMARY:
{contextSummaryJson}

INSTRUCTIONS:
1. Analyze the recent conversation to identify what contextual information would be helpful
2. Use the available functions to search for relevant entities, locations, lore, or narrative history
3. Focus on information that directly relates to the recent conversation
4. Keep responses concise and focused - this is a lightweight pre-processing step
5. Return a structured context summary that will be added as a system message

Use function calls to search for:
- Entities (NPCs, Pokemon, objects) mentioned or implied in the conversation
- Location details relevant to the current or discussed locations
- Lore or rules that might apply to the current situation
- Past narrative events that provide relevant context

Provide a focused context summary in the specified format.";

            contextGatheringHistory.AddUserMessage(contextGatheringDirective);
            
            // Execute the context gathering subroutine with reduced token limit for efficiency
            var chatService = contextGatheringKernel.GetRequiredService<IChatCompletionService>();
            
            var executionSettings = _llmProvider.GetExecutionSettings(
                maxTokens: 3000, // Reduced token limit for lightweight operation
                temperature: 0.3f, // Lower temperature for more focused responses
                enableFunctionCalling: true);

            var result = await chatService.GetChatMessageContentAsync(
                contextGatheringHistory,
                executionSettings,
                contextGatheringKernel,
                cancellationToken
            );
            
            var contextResult = result.ToString();
            
            Debug.WriteLine($"[OrchestrationService] Context gathering completed. Result length: {contextResult.Length}");
            
            return contextResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in RunContextGathering: {ex.Message}");
            return $"Context gathering failed: {ex.Message}. Proceeding with available context.";
        }
    }

    public async Task<string> RunContextManagement(string directive, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the ContextManagement kernel and create a new history for this subroutine
            var contextManagementKernel = _kernels["ContextManagement"];
            var contextManagementHistory = new ChatHistory();
            
            // Load the system prompt for context management
            var systemPrompt = await LoadSystemPromptAsync("ContextManagement");
            contextManagementHistory.AddSystemMessage(systemPrompt);
            
            // Get current game state to provide context
            var gameState = await _gameStateRepository.LoadLatestStateAsync();
            var gameStateJson = JsonSerializer.Serialize(gameState, new JsonSerializerOptions { WriteIndented = true });
            
            // Serialize current chat histories to provide context
            var allHistoriesJson = JsonSerializer.Serialize(
                _histories.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(msg => new { Role = msg.Role.ToString(), Content = msg.Content }).ToList()
                ),
                new JsonSerializerOptions { WriteIndented = true });
            
            // Create comprehensive directive for the context management subroutine
            var comprehensiveDirective = $@"CONTEXT MANAGEMENT REQUEST:
{directive}

CURRENT GAME STATE:
{gameStateJson}

CURRENT CHAT HISTORIES:
{allHistoriesJson}

INSTRUCTIONS:
1. Analyze the provided directive, game state, and chat histories
2. Use the available functions to search, verify, and update context as needed
3. Ensure consistency between vector database, game state, and chat histories
4. Only create new entities if they were authoritively mentioned in chat histories
5. Provide guidance on whether requested entities can exist in the current context
6. Return a structured report of actions taken and recommendations

Use function calls to:
- Search for existing entities, locations, and lore in the vector database
- Query current game state for entity existence and details
- Update or create entities in both game state and vector database as needed
- Log narrative events for future reference";

            contextManagementHistory.AddUserMessage(comprehensiveDirective);
            
            // Execute the context management subroutine with function calling enabled
            var result = await ExecuteSubroutinePromptAsync(contextManagementHistory, contextManagementKernel, cancellationToken);
            
            Debug.WriteLine($"[OrchestrationService] Context management completed. Result length: {result.Length}");
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in RunContextManagement: {ex.Message}");
            return $"Context management failed: {ex.Message}";
        }
    }

    private async Task<string> RunChatHistoryManagement(ChatHistory history)
    {
        try
        {
            // Get the ChatManagement kernel and its dedicated history
            var chatManagementKernel = _kernels["ChatManagement"];
            var chatManagementHistory = new ChatHistory();
            
            // Load the system prompt for chat management
            var systemPrompt = await LoadSystemPromptAsync("ChatManagement");
            chatManagementHistory.AddSystemMessage(systemPrompt);
            
            // Serialize the current chat history to provide context
            var historyJson = System.Text.Json.JsonSerializer.Serialize(
                history.Select(msg => new { Role = msg.Role.ToString(), Content = msg.Content }).ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            
            // Create the directive for the chat management subroutine
            var directive = $@"Please analyze the following chat history and create a comprehensive summary while identifying items that need context management.

CHAT HISTORY TO ANALYZE:
{historyJson}

INSTRUCTIONS:
1. Create a concise but comprehensive summary following the format specified in your system prompt to significantly reduce length
2. Identify any NPCs, Pokemon, locations, items, or story elements mentioned that should be verified/updated in the context management system
3. Return your response in the following structure:

ADVENTURE SUMMARY:
  Your comprehensive summary here
VERIFICATION NEEDED:
  List of items that need context verification
";

            chatManagementHistory.AddUserMessage(directive);
            
            // Execute the chat management subroutine
            var result = await ExecuteSubroutinePromptAsync(chatManagementHistory, chatManagementKernel);
            
            // Update the original history by replacing older messages with the summary
            // IMPORTANT: We need to preserve tool call sequences to avoid API errors
            
            // Keep the system messages
            var systemMessages = history.Where(msg => msg.Role == AuthorRole.System).ToList();
            
            // Find the last complete conversation block (preserve tool call sequences)
            var recentMessages = GetRecentMessagesWithCompleteToolSequences(history, 4); // Get more messages to ensure we have complete sequences
                
            history.Clear();
                
            // Re-add system messages
            foreach (var sysMsg in systemMessages)
            {
                history.Add(sysMsg);
            }
                
            // Add summary as a system message
            history.AddSystemMessage($"CHAT REDUCTION SUMMARY: {result}");
                
            // Re-add recent messages (which now include complete tool sequences)
            foreach (var recentMsg in recentMessages)
            {
                history.Add(recentMsg);
            }
                
            Debug.WriteLine($"[OrchestrationService] Chat history compressed. Summary length: {result.Length}");
            
            
            // Process context management items if any
            if (result.Contains("VERIFICATION NEEDED"))
            {
                Debug.WriteLine($"[OrchestrationService] Found VERIFICATION NEEDED in {result}");
                
                try
                {
                    var contextDirective = $@"History Reduction has just completed. 
Analyze the summary and Verification needed list to make sure all items are properly managed within the game context:
RESULT: 
{result}
";
                    await RunContextManagement(contextDirective);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OrchestrationService] Error running context management for items: {ex.Message}");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in ManageChatHistory: {ex.Message}");
            // Return empty result on error to prevent cascading failures
            return "";
        }
    }

    /// <summary>
    /// Gets recent messages while ensuring tool call sequences are complete.
    /// This prevents API errors when tool messages are separated from their tool_calls.
    /// </summary>
    private List<ChatMessageContent> GetRecentMessagesWithCompleteToolSequences(ChatHistory history, int targetCount)
    {
        if (history.Count == 0)
            return new List<ChatMessageContent>();
            
        var messages = history.ToList();
        var result = new List<ChatMessageContent>();
        
        // Start from the end and work backwards
        for (int i = messages.Count - 1; i >= 0 && result.Count < targetCount; i--)
        {
            var message = messages[i];
            
            // Always include system messages (they don't participate in tool sequences)
            if (message.Role == AuthorRole.System)
            {
                result.Insert(0, message);
                continue;
            }
            
            // For non-system messages, check for tool sequences
            if (message.Role == AuthorRole.Tool)
            {
                // If we encounter a tool message, we need to include its preceding assistant message with tool_calls
                result.Insert(0, message);
                
                // Look backwards for the assistant message with tool_calls
                for (int j = i - 1; j >= 0; j--)
                {
                    var prevMessage = messages[j];
                    result.Insert(0, prevMessage);
                    
                    // Stop when we find the assistant message that should contain tool_calls
                    if (prevMessage.Role == AuthorRole.Assistant)
                    {
                        break;
                    }
                }
            }
            else if (message.Role == AuthorRole.Assistant)
            {
                // Check if this assistant message has tool calls by examining the content
                // In Semantic Kernel, function calls are typically indicated by specific content patterns
                var hasToolCalls = message.Content?.Contains("\"tool_calls\"") == true || 
                                   message.Items?.Any() == true; // Check if there are any items which might be function calls
                
                result.Insert(0, message);
                
                if (hasToolCalls)
                {
                    // Include any subsequent tool messages
                    for (int j = i + 1; j < messages.Count; j++)
                    {
                        var nextMessage = messages[j];
                        if (nextMessage.Role == AuthorRole.Tool)
                        {
                            result.Add(nextMessage);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                // User messages and other types
                result.Insert(0, message);
            }
        }
        
        // Remove duplicates while preserving order
        var seen = new HashSet<ChatMessageContent>();
        return result.Where(msg => seen.Add(msg)).ToList();
    }

    public async Task<string> LoadSystemPromptAsync(string phaseToLoad)
    {
        try
        {
            var promptPath = phaseToLoad switch
            {
                "GameCreation" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md"),
                "CharacterCreation" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CharacterCreationPhase.md"),
                "WorldGeneration" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "WorldGenerationPhase.md"),
                "Exploration" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ExplorationPhase.md"),
                "Combat" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CombatPhase.md"),
                "LevelUp" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "LevelUpPhase.md"),
                "ChatManagement" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ChatManagementSubroutine.md"),
                "ContextGathering" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ContextGatheringSubroutine.md"),
                "ContextManagement" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ContextManagementSubroutine.md"),
                _ => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md")
            };

            var systemPrompt = await File.ReadAllTextAsync(promptPath);
            return systemPrompt;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Warning: Could not load system prompt for phase {phaseToLoad}: {ex.Message}. Using default prompt.");
            throw;
        }
    }
}