using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.LLM;
using PokeLLM.GameState.Models;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.Configuration;
using PokeLLM.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace PokeLLM.Game.Orchestration;

public interface IPhaseServiceProvider
{
    IPhaseService GetPhaseService(GamePhase phase);
}

public interface IPhaseService
{
    GamePhase Phase { get; }
    IAsyncEnumerable<string> ProcessPhaseAsync(string inputMessage, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ProcessInputWithSpecialPromptAsync(string specialPrompt, CancellationToken cancellationToken = default);
    Task<bool> IsPhaseCompleteAsync();
    Task<GamePhase> GetNextPhaseAsync();
}

public class PhaseService : IPhaseService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRulesetManager _rulesetManager;
    private readonly IDebugConfiguration _debugConfig;
    private readonly IDebugLogger _debugLogger;
    private readonly GamePhase _phase;
    private readonly Type _pluginType;
    private readonly string _promptName;
    private readonly string _pluginRegistrationName;
    private Kernel _kernel;
    private ChatHistory _chatHistory;

    public GamePhase Phase => _phase;

    public PhaseService(
        GamePhase phase,
        Type pluginType,
        string promptName,
        string pluginRegistrationName,
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider,
        IDebugConfiguration debugConfig,
        IDebugLogger debugLogger)
    {
        _phase = phase;
        _pluginType = pluginType;
        _promptName = promptName;
        _pluginRegistrationName = pluginRegistrationName;
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
        _rulesetManager = serviceProvider.GetRequiredService<IRulesetManager>();
        _debugConfig = debugConfig;
        _debugLogger = debugLogger;
        
        InitializeKernel();
        _chatHistory = new ChatHistory();
    }

    private void InitializeKernel()
    {
        _kernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        
        // For GameSetup phase, use hardcoded plugin (no ruleset available yet)
        if (_phase == GamePhase.GameSetup)
        {
            LoadHardcodedPlugin();
        }
        else
        {
            // For other phases, try to load functions from ruleset
            LoadRulesetFunctions();
        }
    }
    
    private void LoadHardcodedPlugin()
    {
        // Use reflection to call AddFromType<T> with the dynamic type
        var addFromTypeMethod = typeof(KernelExtensions)
            .GetMethods()
            .Where(m => m.Name == "AddFromType" && m.IsGenericMethodDefinition)
            .FirstOrDefault(m => 
            {
                var parameters = m.GetParameters();
                return parameters.Length == 4 && 
                       parameters[0].ParameterType == typeof(ICollection<KernelPlugin>) &&
                       parameters[3].ParameterType == typeof(IServiceProvider);
            });
            
        if (addFromTypeMethod != null)
        {
            // Load the phase-specific plugin
            var genericMethod = addFromTypeMethod.MakeGenericMethod(_pluginType);
            var defaultJsonOptions = new System.Text.Json.JsonSerializerOptions();
            genericMethod.Invoke(null, new object[] { _kernel.Plugins, defaultJsonOptions, _pluginRegistrationName, _serviceProvider });
            
            // Also load the RulesetManagementPlugin for all phases (for LLM access to ruleset management)
            var rulesetPluginMethod = addFromTypeMethod.MakeGenericMethod(typeof(Game.Plugins.RulesetManagementPlugin));
            rulesetPluginMethod.Invoke(null, new object[] { _kernel.Plugins, defaultJsonOptions, "RulesetManagement", _serviceProvider });
            
            Debug.WriteLine($"[{_phase}PhaseService] Loaded hardcoded plugin {_pluginType.Name} and RulesetManagementPlugin");
        }
        else
        {
            throw new InvalidOperationException($"Could not find AddFromType method for plugin type {_pluginType}");
        }
    }
    
    private void LoadRulesetFunctions()
    {
        try
        {
            // Load functions from the active ruleset
            var rulesetFunctions = _rulesetManager.GetPhaseFunctionsAsync(_phase).GetAwaiter().GetResult();
            
            if (rulesetFunctions.Any())
            {
                // Add ruleset functions to the kernel
                var rulesetPlugin = KernelPluginFactory.CreateFromFunctions(_pluginRegistrationName, null, rulesetFunctions);
                _kernel.Plugins.Add(rulesetPlugin);
                
                Debug.WriteLine($"[{_phase}PhaseService] Loaded {rulesetFunctions.Count()} functions from ruleset");
                
                // Also add the RulesetManagementPlugin for LLM access to ruleset management
                var addFromTypeMethod = typeof(KernelExtensions)
                    .GetMethods()
                    .Where(m => m.Name == "AddFromType" && m.IsGenericMethodDefinition)
                    .FirstOrDefault(m => 
                    {
                        var parameters = m.GetParameters();
                        return parameters.Length == 4 && 
                               parameters[0].ParameterType == typeof(ICollection<KernelPlugin>) &&
                               parameters[3].ParameterType == typeof(IServiceProvider);
                    });
                    
                if (addFromTypeMethod != null)
                {
                    var rulesetPluginMethod = addFromTypeMethod.MakeGenericMethod(typeof(Game.Plugins.RulesetManagementPlugin));
                    var defaultJsonOptions = new System.Text.Json.JsonSerializerOptions();
                    rulesetPluginMethod.Invoke(null, new object[] { _kernel.Plugins, defaultJsonOptions, "RulesetManagement", _serviceProvider });
                    
                    Debug.WriteLine($"[{_phase}PhaseService] Also loaded RulesetManagementPlugin alongside ruleset functions");
                }
            }
            else
            {
                Debug.WriteLine($"[{_phase}PhaseService] No ruleset functions found, falling back to hardcoded plugin");
                LoadHardcodedPlugin();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{_phase}PhaseService] Error loading ruleset functions: {ex.Message}, falling back to hardcoded plugin");
            LoadHardcodedPlugin();
        }
    }

    public async IAsyncEnumerable<string> ProcessPhaseAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    _debugLogger.LogUserInput(inputMessage);
    _debugLogger.LogDebug($"[{_phase}PhaseService] Processing user input: {inputMessage}");
    
    var responseBuilder = new StringBuilder();
    
    // Load current game state and increment turn number
    var gameState = await _gameStateRepository.LoadLatestStateAsync();
    gameState.GameTurnNumber++;
    await _gameStateRepository.SaveStateAsync(gameState);
    
    _debugLogger.LogGameState(JsonSerializer.Serialize(gameState, new JsonSerializerOptions { WriteIndented = true }));
    _debugLogger.LogDebug($"[{_phase}PhaseService] Game turn incremented to: {gameState.GameTurnNumber}");

    await foreach (var chunk in StreamResponseAsync(gameState, inputMessage, responseBuilder, cancellationToken))
    {
        // Removed redundant LogSystemOutput - the complete response is logged in LogLLMResponse
        yield return chunk;
    }
    
    var fullResponse = responseBuilder.ToString();
    _debugLogger.LogLLMResponse(fullResponse);
    _debugLogger.LogDebug($"[{_phase}PhaseService] Complete response generated. Length: {fullResponse.Length}");
}

    public async IAsyncEnumerable<string> ProcessInputWithSpecialPromptAsync(string specialPrompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseBuilder = new StringBuilder();
        
        // Create a fresh ChatHistory with the special prompt as system message
        var specialHistory = new ChatHistory();
        specialHistory.AddSystemMessage(specialPrompt);
        
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 4000,
            temperature: 0.7f,
            enableFunctionCalling: false);

        // Stream the response
        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(specialHistory, executionSettings, _kernel, cancellationToken))
        {
            var content = chunk.Content ?? string.Empty;
            responseBuilder.Append(content);
            yield return content;
        }
    }

    private async IAsyncEnumerable<string> StreamResponseAsync(GameStateModel gameState, string inputMessage, StringBuilder responseBuilder, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create fresh ChatHistory with updated system prompt
        var freshHistory = new ChatHistory();
        var systemPrompt = await LoadSystemPromptAsync(_promptName);

        // Inject CurrentContext into prompt using {{context}} variable
        var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ?
            gameState.CurrentContext : "No context available.";
        systemPrompt = systemPrompt.Replace("{{context}}", currentContext);

        freshHistory.AddSystemMessage(systemPrompt);
        
        // Transfer existing conversation history (skip old system message if exists)
        var messagesToTransfer = _chatHistory.Where(msg => msg.Role != AuthorRole.System);
        foreach (var message in messagesToTransfer)
        {
            freshHistory.Add(message);
        }
        
        // Add new user message
        freshHistory.AddUserMessage(inputMessage);
        
        // Update the stored history
        _chatHistory = freshHistory;

        // Pre-validate and clean the history if needed
        var historyToUse = await ValidateAndCleanHistory(_chatHistory, cancellationToken);

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        // Stream the response
        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(historyToUse, executionSettings, _kernel, cancellationToken))
        {
            var content = chunk.Content ?? string.Empty;
            responseBuilder.Append(content);
            yield return content;
        }

        // Add assistant response to history
        var fullResponse = responseBuilder.ToString();
        _chatHistory.AddAssistantMessage(fullResponse);

        Debug.WriteLine($"[{_phase}PhaseService] Processed input. Response length: {fullResponse.Length}");
    }

    public async Task<bool> IsPhaseCompleteAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        // Phase completion is handled by plugin functions that change CurrentPhase
        return false;
    }

    public async Task<GamePhase> GetNextPhaseAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        return gameState.CurrentPhase; // Phase changes are made by plugin functions
    }

    private async Task<ChatHistory> ValidateAndCleanHistory(ChatHistory history, CancellationToken cancellationToken)
    {
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var executionSettings = _llmProvider.GetExecutionSettings(
                maxTokens: 10000,
                temperature: 0.7f,
                enableFunctionCalling: true);

            // Test the history with a minimal non-streaming call to detect tool sequence issues
            var testResult = await chatService.GetChatMessageContentAsync(
                history,
                executionSettings,
                _kernel,
                cancellationToken
            );
            
            // If we get here, the history is valid
            return history;
        }
        catch (Exception ex) when (ex.Message.Contains("tool") && ex.Message.Contains("must be a response to a preceeding message"))
        {
            Debug.WriteLine($"[{_phase}PhaseService] Tool call sequence error detected during validation: {ex.Message}");
            Debug.WriteLine($"[{_phase}PhaseService] Cleaning chat history...");
            
            var cleanedHistory = CleanChatHistoryToolSequences(history);
            
            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                var executionSettings = _llmProvider.GetExecutionSettings(
                    maxTokens: 10000,
                    temperature: 0.7f,
                    enableFunctionCalling: true);

                // Test the cleaned history
                var testCleanedResult = await chatService.GetChatMessageContentAsync(
                    cleanedHistory,
                    executionSettings,
                    _kernel,
                    cancellationToken
                );
                
                Debug.WriteLine($"[{_phase}PhaseService] Cleaned history validation succeeded");
                return cleanedHistory;
            }
            catch (Exception cleanEx)
            {
                Debug.WriteLine($"[{_phase}PhaseService] Cleaned history validation failed: {cleanEx.Message}");
                // Return the cleaned history anyway, as it's the best we can do
                return cleanedHistory;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{_phase}PhaseService] Unexpected error during history validation: {ex.Message}");
            // Return original history for other types of errors
            return history;
        }
    }

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
                    Debug.WriteLine($"[{_phase}PhaseService] Removing orphaned tool message: {contentPreview}...");
                }
            }
            else
            {
                // Assistant and other messages
                cleanedHistory.Add(message);
            }
        }
        
        Debug.WriteLine($"[{_phase}PhaseService] Cleaned history: {originalHistory.Count} -> {cleanedHistory.Count} messages");
        return cleanedHistory;
    }

    private async Task<string> LoadSystemPromptAsync(string promptName)
    {
        try
        {
            _debugLogger.LogDebug($"[{_phase}PhaseService] Loading system prompt: {promptName}");
            
            // Always start with the file-based prompt as the template
            var templatePrompt = await LoadFileBasedPrompt(promptName);
            
            // Inject ruleset prompt templates if available for any phase
            var rulesetSystemPrompt = _rulesetManager.GetPhasePromptTemplate(_phase);
            var rulesetPhaseObjective = _rulesetManager.GetPhaseObjectiveTemplate(_phase);
            var settingRequirements = _rulesetManager.GetSettingRequirements();
            var storytellingDirective = _rulesetManager.GetStorytellingDirective();
            
            _debugLogger.LogDebug($"[{_phase}PhaseService] Ruleset injections - SystemPrompt: {!string.IsNullOrEmpty(rulesetSystemPrompt)}, Objective: {!string.IsNullOrEmpty(rulesetPhaseObjective)}, Requirements: {!string.IsNullOrEmpty(settingRequirements)}, Directive: {!string.IsNullOrEmpty(storytellingDirective)}");
            
            // Inject ruleset system prompt or fall back to empty string
            templatePrompt = templatePrompt.Replace("{{rulesetSystemPrompt}}", 
                !string.IsNullOrEmpty(rulesetSystemPrompt) ? rulesetSystemPrompt : "No specific ruleset guidelines available.");
                
            // Inject ruleset phase objective or fall back to empty string
            templatePrompt = templatePrompt.Replace("{{rulesetPhaseObjective}}", 
                !string.IsNullOrEmpty(rulesetPhaseObjective) ? rulesetPhaseObjective : "Follow the default phase objective below.");
                
            // Inject setting requirements
            templatePrompt = templatePrompt.Replace("{{settingRequirements}}", 
                !string.IsNullOrEmpty(settingRequirements) ? settingRequirements : "No specific setting requirements defined.");
                
            // Inject storytelling directive
            templatePrompt = templatePrompt.Replace("{{storytellingDirective}}", 
                !string.IsNullOrEmpty(storytellingDirective) ? storytellingDirective : "No specific storytelling directive defined.");
                
            _debugLogger.LogDebug($"[{_phase}PhaseService] System prompt loaded and processed. Final length: {templatePrompt.Length}");
            _debugLogger.LogPrompt($"Final {promptName} System Prompt", templatePrompt);
            
            return templatePrompt;
        }
        catch (Exception ex)
        {
            _debugLogger.LogError($"[{_phase}PhaseService] Error loading prompt: {ex.Message}", ex);
            throw;
        }
    }
    
    private async Task<string> LoadFileBasedPrompt(string promptName)
{
    string promptPath;
    
    // Use debug prompts if debug mode is enabled
    if (_debugConfig.IsDebugPromptsEnabled)
    {
        promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "Debug", $"{promptName}.md");
        _debugLogger.LogDebug($"Loading DEBUG prompt from: {promptPath}");
    }
    else
    {
        promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", $"{promptName}.md");
        _debugLogger.LogDebug($"Loading standard prompt from: {promptPath}");
    }
    
    var systemPrompt = await File.ReadAllTextAsync(promptPath);
    
    // Log the prompt content in debug mode
    _debugLogger.LogPrompt($"{promptName} ({(_debugConfig.IsDebugPromptsEnabled ? "DEBUG" : "STANDARD")})", systemPrompt);
    
    return systemPrompt;
}
}

public class PhaseServiceProvider : IPhaseServiceProvider
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDebugConfiguration _debugConfig;
    private readonly IDebugLogger _debugLogger;
    private readonly Dictionary<GamePhase, IPhaseService> _phaseServices;

    public PhaseServiceProvider(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider,
        IDebugConfiguration debugConfig,
        IDebugLogger debugLogger)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
        _debugConfig = debugConfig;
        _debugLogger = debugLogger;
        _phaseServices = new Dictionary<GamePhase, IPhaseService>();
        
        InitializePhaseServices();
    }

    private void InitializePhaseServices()
    {
        _phaseServices[GamePhase.GameSetup] = new PhaseService(
            GamePhase.GameSetup,
            typeof(Game.Plugins.GameSetupPhasePlugin),
            "GameSetupPhase",
            "GameSetup",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider,
            _debugConfig,
            _debugLogger);

        _phaseServices[GamePhase.WorldGeneration] = new PhaseService(
            GamePhase.WorldGeneration,
            typeof(Game.Plugins.WorldGenerationPhasePlugin),
            "WorldGenerationPhase",
            "WorldGeneration",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider,
            _debugConfig,
            _debugLogger);

        _phaseServices[GamePhase.Exploration] = new PhaseService(
            GamePhase.Exploration,
            typeof(Game.Plugins.ExplorationPhasePlugin),
            "ExplorationPhase",
            "Exploration",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider,
            _debugConfig,
            _debugLogger);

        _phaseServices[GamePhase.Combat] = new PhaseService(
            GamePhase.Combat,
            typeof(Game.Plugins.CombatPhasePlugin),
            "CombatPhase",
            "Combat",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider,
            _debugConfig,
            _debugLogger);

        _phaseServices[GamePhase.LevelUp] = new PhaseService(
            GamePhase.LevelUp,
            typeof(Game.Plugins.LevelUpPhasePlugin),
            "LevelUpPhase",
            "LevelUp",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider,
            _debugConfig,
            _debugLogger);
    }

    public IPhaseService GetPhaseService(GamePhase phase)
    {
        return _phaseServices.TryGetValue(phase, out var service) 
            ? service 
            : _phaseServices[GamePhase.Exploration]; // Default fallback
    }
}