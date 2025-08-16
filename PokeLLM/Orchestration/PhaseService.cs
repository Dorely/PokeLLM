using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Game.LLM;
using PokeLLM.GameState.Models;
using PokeLLM.GameRules.Interfaces;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;
using System.Reflection;

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
        IServiceProvider serviceProvider)
    {
        _phase = phase;
        _pluginType = pluginType;
        _promptName = promptName;
        _pluginRegistrationName = pluginRegistrationName;
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
        _rulesetManager = serviceProvider.GetRequiredService<IRulesetManager>();
        
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
        var responseBuilder = new StringBuilder();
        
        // Load current game state and increment turn number
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        gameState.GameTurnNumber++;
        await _gameStateRepository.SaveStateAsync(gameState);

        await foreach (var chunk in StreamResponseAsync(gameState, inputMessage, responseBuilder, cancellationToken))
        {
            yield return chunk;
        }
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
            // For GameSetup phase, use traditional file-based prompts
            if (_phase == GamePhase.GameSetup)
            {
                return await LoadFileBasedPrompt(promptName);
            }
            
            // For other phases, try to load prompt from ruleset first
            var rulesetPrompt = _rulesetManager.GetPhasePromptTemplate(_phase);
            if (!string.IsNullOrEmpty(rulesetPrompt))
            {
                Debug.WriteLine($"[{_phase}PhaseService] Using ruleset prompt template");
                return rulesetPrompt;
            }
            
            // Fallback to file-based prompt
            Debug.WriteLine($"[{_phase}PhaseService] No ruleset prompt found, falling back to file-based prompt");
            return await LoadFileBasedPrompt(promptName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{_phase}PhaseService] Error loading prompt: {ex.Message}");
            throw;
        }
    }
    
    private async Task<string> LoadFileBasedPrompt(string promptName)
    {
        var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", $"{promptName}.md");
        var systemPrompt = await File.ReadAllTextAsync(promptPath);
        return systemPrompt;
    }
}

public class PhaseServiceProvider : IPhaseServiceProvider
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<GamePhase, IPhaseService> _phaseServices;

    public PhaseServiceProvider(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        _serviceProvider = serviceProvider;
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
            _serviceProvider);

        _phaseServices[GamePhase.WorldGeneration] = new PhaseService(
            GamePhase.WorldGeneration,
            typeof(Game.Plugins.WorldGenerationPhasePlugin),
            "WorldGenerationPhase",
            "WorldGeneration",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider);

        _phaseServices[GamePhase.Exploration] = new PhaseService(
            GamePhase.Exploration,
            typeof(Game.Plugins.ExplorationPhasePlugin),
            "ExplorationPhase",
            "Exploration",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider);

        _phaseServices[GamePhase.Combat] = new PhaseService(
            GamePhase.Combat,
            typeof(Game.Plugins.CombatPhasePlugin),
            "CombatPhase",
            "Combat",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider);

        _phaseServices[GamePhase.LevelUp] = new PhaseService(
            GamePhase.LevelUp,
            typeof(Game.Plugins.LevelUpPhasePlugin),
            "LevelUpPhase",
            "LevelUp",
            _llmProvider,
            _gameStateRepository,
            _serviceProvider);
    }

    public IPhaseService GetPhaseService(GamePhase phase)
    {
        return _phaseServices.TryGetValue(phase, out var service) 
            ? service 
            : _phaseServices[GamePhase.Exploration]; // Default fallback
    }
}