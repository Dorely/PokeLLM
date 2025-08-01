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
            // Load current game state to determine phase and track changes
            gameState = await _gameStateRepository.LoadLatestStateAsync();
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
                    history.AddSystemMessage($"GATHERED CONTEXT: {contextResult}");
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
        try
        {
            var fullResponse = responseBuilder.ToString();
            
            // Reload game state to check if phase was changed by plugin functions
            var updatedGameState = await _gameStateRepository.LoadLatestStateAsync();
            var finalPhase = updatedGameState.CurrentPhase;
            
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
            // Note: We can't yield return here since streaming is already complete
        }
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

    private async IAsyncEnumerable<string> ExecutePromptStreamingAsync(ChatHistory history, Kernel kernel, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);
        
        var responseBuilder = new StringBuilder();
        var result = chatService.GetStreamingChatMessageContentsAsync(
            history,
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
            await RunChatManagementHistory(history);
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
            var result = await ExecutePromptAsync(contextManagementHistory, contextManagementKernel, cancellationToken);
            
            Debug.WriteLine($"[OrchestrationService] Context management completed. Result length: {result.Length}");
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in RunContextManagement: {ex.Message}");
            return $"Context management failed: {ex.Message}";
        }
    }

    private async Task<ChatManagementResult> RunChatManagementHistory(ChatHistory history)
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
1. Create a concise but comprehensive summary following the format specified in your system prompt
2. Identify any NPCs, Pokemon, locations, items, or story elements mentioned that should be verified/updated in the context management system
3. Return your response as a JSON object with the following structure:
{{
  ""adventureSummary"": ""Your comprehensive summary here"",
  ""contextManagementItems"": [""List of items that need context verification""]
}}";

            chatManagementHistory.AddUserMessage(directive);
            
            // Execute the chat management subroutine
            var result = await ExecutePromptAsync(chatManagementHistory, chatManagementKernel);
            
            // Parse the result into a ChatManagementResult
            ChatManagementResult managementResult;
            try
            {
                var resultJson = JsonSerializer.Deserialize<ChatManagementResult>(result);
                managementResult = resultJson ?? new ChatManagementResult();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[OrchestrationService] Failed to parse chat management result as JSON: {ex.Message}");
                // Fallback: create a basic result with the raw response as summary
                managementResult = new ChatManagementResult
                {
                    AdventureSummary = result,
                    ContextManagementItems = new List<string>()
                };
            }
            
            // Update the original history by replacing older messages with the summary
            if (!string.IsNullOrEmpty(managementResult.AdventureSummary))
            {
                // Keep the system message and recent messages, replace middle with summary
                var systemMessages = history.Where(msg => msg.Role == AuthorRole.System).ToList();
                var recentMessages = history.TakeLast(5).ToList(); // Keep last 5 messages
                
                history.Clear();
                
                // Re-add system messages
                foreach (var sysMsg in systemMessages)
                {
                    history.Add(sysMsg);
                }
                
                // Add summary as a system message
                history.AddSystemMessage($"ADVENTURE SUMMARY: {managementResult.AdventureSummary}");
                
                // Re-add recent messages
                foreach (var recentMsg in recentMessages)
                {
                    history.Add(recentMsg);
                }
                
                Debug.WriteLine($"[OrchestrationService] Chat history compressed. Summary length: {managementResult.AdventureSummary.Length}");
            }
            
            // Process context management items if any
            if (managementResult.ContextManagementItems?.Any() == true)
            {
                Debug.WriteLine($"[OrchestrationService] Found {managementResult.ContextManagementItems.Count} items for context management");
                
                try
                {
                    var contextDirective = $@"History Management has just completed. 
Please verify the following list of items to be properly managed within the game context:
ADVENTURE SUMMARY: {managementResult.AdventureSummary}
ITEMS TO MANAGE: {string.Join(", ", managementResult.ContextManagementItems)}
";
                    await RunContextManagement(contextDirective);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OrchestrationService] Error running context management for items: {ex.Message}");
                }
            }
            
            return managementResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrchestrationService] Error in ManageChatHistory: {ex.Message}");
            // Return empty result on error to prevent cascading failures
            return new ChatManagementResult();
        }
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

public class ChatManagementResult
{
    [JsonPropertyName("adventureSummary")]
    [Description("A comprehensive summary of the adventure that preserves narrative flow and important story elements")]
    public string AdventureSummary { get; set; } = string.Empty;

    [JsonPropertyName("contextManagementItems")]
    [Description("List of NPCs, Pokemon, locations, items, or story elements that should be verified/updated in the context management system")]
    public List<string> ContextManagementItems { get; set; } = new();
}