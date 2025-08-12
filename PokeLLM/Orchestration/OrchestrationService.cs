using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PokeLLM.Game.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace PokeLLM.Game.Orchestration;
public interface IOrchestrationService
{
    public IAsyncEnumerable<string> OrchestrateAsync(string inputMessage, CancellationToken cancellationToken = default);
}

public class OrchestrationService : IOrchestrationService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnifiedContextService _unifiedContextService;
    private Dictionary<string, Kernel> _kernels;
    private Dictionary<string, ChatHistory> _histories;
    
    private GamePhase _currentPhase;

    public OrchestrationService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider,
        IUnifiedContextService unifiedContextService)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
        _unifiedContextService = unifiedContextService;
        _kernels = new Dictionary<string, Kernel>();
        _histories = new Dictionary<string, ChatHistory>();
        SetupPromptsAndPlugins();
    }

    private void SetupPromptsAndPlugins()
    {
        // Initialize kernels and chat histories for gameplay phases only
        SetupGamePhaseKernel<ExplorationPhasePlugin>("Exploration");
        SetupGamePhaseKernel<CombatPhasePlugin>("Combat");
        SetupGamePhaseKernel<LevelUpPhasePlugin>("LevelUp");
    }

    private void SetupGamePhaseKernel<T>(string phaseName) where T : class
    {
        try
        {
            var kernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
            kernel.Plugins.AddFromType<T>(phaseName, _serviceProvider);
            _kernels[phaseName] = kernel;
            _histories[phaseName] = new ChatHistory();

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
        GamePhase initialPhase = GamePhase.Exploration; // Initialize with default
        Kernel kernel = null;
        ChatHistory history = null;
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
            var oldHistory = _histories[phaseKernelName];
            
            // Create fresh ChatHistory with updated system prompt
            history = new ChatHistory();
            var systemPrompt = await LoadSystemPromptAsync(phaseKernelName);

            // INJECT: CurrentContext into prompt using {{context}} variable
            var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ?
                gameState.CurrentContext : "No context available.";
            systemPrompt = systemPrompt.Replace("{{context}}", currentContext);

            history.AddSystemMessage(systemPrompt);
            
            // Transfer existing conversation history (skip old system message if exists)
            var messagesToTransfer = oldHistory.Where(msg => msg.Role != AuthorRole.System);
            foreach (var message in messagesToTransfer)
            {
                history.Add(message);
            }
            
            Debug.WriteLine($"[OrchestrationService] Processing input for phase: {_currentPhase}");
            
            Debug.WriteLine($"[OrchestrationService] Processing gameplay phase: {_currentPhase}");
            
            // Add the user's input message to the history
            history.AddUserMessage(inputMessage);
            
            // Update the stored history
            _histories[phaseKernelName] = history;
            
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
                
                Debug.WriteLine($"[OrchestrationService] Phase transition detected, context management will be handled by recursive call");
                
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
            
            // NEW: Post-processing with Unified Context Management
            if (!hasPhaseTransition) // Only run if not handling phase transition
            {
                var contextDirective = $@"Post-turn context update for {_currentPhase} phase.

Turn: {updatedGameState.GameTurnNumber}
Input: {inputMessage}
Response: {fullResponse}

Execute all context management functions to update CurrentContext field and maintain consistency.";

                var contextResult = await _unifiedContextService.RunContextManagementAsync(history, contextDirective, cancellationToken);
                
                // If history was compressed, update the stored history with the compressed version
                if (contextResult.HistoryWasCompressed && contextResult.CompressedHistory.Count > 0)
                {
                    var phaseKernelName = GetPhaseKernelName(_currentPhase);
                    
                    // Create fresh ChatHistory with updated system prompt for compressed history
                    var compressedHistory = new ChatHistory();
                    var systemPrompt = await LoadSystemPromptAsync(phaseKernelName);
                    
                    // INJECT: CurrentContext into prompt using {{context}} variable
                    var currentContext = !string.IsNullOrEmpty(updatedGameState.CurrentContext) ?
                        updatedGameState.CurrentContext : "No context available.";
                    systemPrompt = systemPrompt.Replace("{{context}}", currentContext);
                    
                    compressedHistory.AddSystemMessage(systemPrompt);
                    
                    // Add compressed conversation history (skip system message from compressed version)
                    var messagesToAdd = contextResult.CompressedHistory.Where(msg => msg.Role != AuthorRole.System);
                    foreach (var message in messagesToAdd)
                    {
                        compressedHistory.Add(message);
                    }
                    
                    // Store the compressed history
                    _histories[phaseKernelName] = compressedHistory;
                    
                    Debug.WriteLine($"[OrchestrationService] Chat history compressed: {history.Count} -> {compressedHistory.Count} messages");
                }
            }
            
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
            GamePhase.Exploration => $"{baseMessage} Please continue the adventure in exploration mode. The player can now explore the world, interact with NPCs, and discover new locations.",
            
            GamePhase.Combat => $"{baseMessage} Please continue managing the combat encounter that has just begun. Describe the battle situation and guide the player through their combat options.",
            
            GamePhase.LevelUp => $"{baseMessage} Please guide the player through the level up process, allowing them to improve their abilities and grow stronger.",
            
            _ => $"{baseMessage} Please continue the adventure in the new {toPhase} phase."
        };
    }

    private string GetPhaseKernelName(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Exploration => "Exploration",
            GamePhase.Combat => "Combat",
            GamePhase.LevelUp => "LevelUp",
            _ => "Exploration" // Default fallback
        };
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
        
        // Note: Chat history management now handled by UnifiedContextService
    }



    public async Task<string> LoadSystemPromptAsync(string phaseToLoad)
    {
        try
        {
            var promptPath = phaseToLoad switch
            {
                "Exploration" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ExplorationPhase.md"),
                "Combat" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CombatPhase.md"),
                "LevelUp" => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "LevelUpPhase.md"),
                _ => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ExplorationPhase.md")
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