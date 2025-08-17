using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.GameState;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState.Models;
using System.Text.RegularExpressions;
using PokeLLM.Logging;

namespace PokeLLM.Game.Orchestration;

public class ContextManagementResult
{
    public string Response { get; set; } = string.Empty;
    public ChatHistory CompressedHistory { get; set; }
    public bool HistoryWasCompressed { get; set; }
}

public interface IUnifiedContextService
{
    Task<ContextManagementResult> RunContextManagementAsync(ChatHistory history, string directive, CancellationToken cancellationToken = default);
}

public class UnifiedContextService : IUnifiedContextService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IRulesetManager _rulesetManager;
    private readonly IDebugLogger _debugLogger;
    private Kernel _contextKernel;

    public UnifiedContextService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider,
        IRulesetManager rulesetManager,
        IDebugLogger debugLogger)
    {
        _llmProvider = llmProvider;
        _serviceProvider = serviceProvider;
        _gameStateRepository = gameStateRepository;
        _rulesetManager = rulesetManager;
        _debugLogger = debugLogger;
        
        _debugLogger.LogDebug("[UnifiedContextService] UnifiedContextService initialized");
        InitializeKernel();
    }

    private void InitializeKernel()
    {
        _debugLogger.LogDebug("[UnifiedContextService] Initializing context kernel...");
        _contextKernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        _contextKernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
        _debugLogger.LogDebug("[UnifiedContextService] Context kernel initialized with UnifiedContextPlugin");
    }

    public async Task<ContextManagementResult> RunContextManagementAsync(ChatHistory history, string directive, CancellationToken cancellationToken = default)
    {
        _debugLogger.LogDebug($"[UnifiedContextService] Starting context management with directive: {directive}");
        
        try
        {
            var contextHistory = new ChatHistory();
            var systemPrompt = await LoadSystemPromptAsync("UnifiedContextSubroutine");

            var gameState = await _gameStateRepository.LoadLatestStateAsync();
            _debugLogger.LogDebug($"[UnifiedContextService] Loaded game state - Phase: {gameState.CurrentPhase}, Turn: {gameState.GameTurnNumber}");

            // Process history to ensure role compatibility with Gemini - handle nulls
            var processedHistory = (history ?? new ChatHistory())
                .Where(msg => msg != null && 
                             msg.Role != AuthorRole.System && 
                             !string.IsNullOrWhiteSpace(msg.Content))
                .ToList();

            var historyString = string.Join("\n\n", (history ?? new ChatHistory())
                .Where(msg => msg != null && 
                             msg.Role != AuthorRole.System && 
                             !string.IsNullOrWhiteSpace(msg.Content))
                .Select(msg => msg.Content ?? string.Empty));

            _debugLogger.LogDebug($"[UnifiedContextService] Processed history: {processedHistory.Count} messages, {historyString.Length} characters");

            // Check if history needs compression (based on the formatted string that will be sent)
            const int maxMessages = 20;
            const int maxCharacters = 50000;
            var needsCompression = processedHistory.Count > maxMessages || historyString.Length > maxCharacters;
            
            _debugLogger.LogDebug($"[UnifiedContextService] History compression needed: {needsCompression} (Messages: {processedHistory.Count}/{maxMessages}, Characters: {historyString.Length}/{maxCharacters})");

            var currentContext = !string.IsNullOrEmpty(gameState?.CurrentContext) ?
                gameState.CurrentContext : "World generation beginning - creating initial world content.";

            // Add compression directive if needed
            var finalDirective = directive ?? string.Empty;
            if (needsCompression)
            {
                finalDirective += $"\n\nIMPORTANT: The chat history is too large ({processedHistory.Count} messages, {historyString.Length} characters). " +
                                "Please provide a compressed version of the conversation history in the following format:\n" +
                                "COMPRESSED_HISTORY\n" +
                                "[System] Brief description of initial system context\n" +
                                "[User] Summary of key player actions and decisions\n" +
                                "[Assistant] Summary of key story developments and responses\n" +
                                "... (continue pattern for key exchanges)\n" +
                                "</COMPRESSED_HISTORY>";
                                
                _debugLogger.LogDebug("[UnifiedContextService] Added compression directive to final directive");
            }

            // Get ruleset-specific context elements for the current phase
            var rulesetContextElements = GetRulesetContextElements(gameState.CurrentPhase);
            var rulesetContextText = string.IsNullOrEmpty(rulesetContextElements) ? 
                "" : $"\n\nRuleset Context Elements: {rulesetContextElements}";
            
            _debugLogger.LogDebug($"[UnifiedContextService] Ruleset context elements: {rulesetContextElements}");
            
            //Replace placeholders with actual context
            systemPrompt = systemPrompt.Replace("{{history}}", historyString);
            systemPrompt = systemPrompt.Replace("{{context}}", currentContext);
            systemPrompt = systemPrompt.Replace("{{ruleset_context}}", rulesetContextText);

            contextHistory.AddSystemMessage(systemPrompt);
            contextHistory.AddUserMessage(finalDirective);

            _debugLogger.LogPrompt("UnifiedContext System Prompt", systemPrompt);
            _debugLogger.LogDebug($"[UnifiedContextService] Final directive: {finalDirective}");

            var chatService = _contextKernel.GetRequiredService<IChatCompletionService>();
            var executionSettings = _llmProvider.GetExecutionSettings(
                maxTokens: 3000,
                temperature: 0.7f,
                enableFunctionCalling: true);

            _debugLogger.LogDebug("[UnifiedContextService] Sending context management request to LLM...");
            var result = await chatService.GetChatMessageContentAsync(
                contextHistory, executionSettings, _contextKernel, cancellationToken);

            // Handle function calls manually since we're using EnableKernelFunctions
            var response = result?.ToString() ?? string.Empty;
            _debugLogger.LogLLMResponse(response);

            // Ensure we always have a response, even if functions were called
            if (string.IsNullOrWhiteSpace(response))
            {
                response = "Context management completed successfully.";
                _debugLogger.LogDebug("[UnifiedContextService] Empty response, using default message");
            }

            var compressedHistory = ExtractCompressedHistory(response);
            _debugLogger.LogDebug($"[UnifiedContextService] Extracted compressed history: {compressedHistory.Count} messages");

            var contextResult = new ContextManagementResult
            {
                Response = response,
                CompressedHistory = compressedHistory,
                HistoryWasCompressed = needsCompression
            };
            
            _debugLogger.LogDebug($"[UnifiedContextService] Context management completed successfully. History was compressed: {needsCompression}");
            return contextResult;
        }
        catch (Exception ex)
        {
            _debugLogger.LogError($"[UnifiedContextService] Error during context management: {ex.Message}", ex);
            throw;
        }
        
    }

    private ChatHistory ExtractCompressedHistory(string response)
    {
        _debugLogger.LogDebug("[UnifiedContextService] Extracting compressed history from response");
        
        // Handle null or empty response
        if (string.IsNullOrWhiteSpace(response))
        {
            _debugLogger.LogDebug("[UnifiedContextService] Response is null or empty, returning empty history");
            return new ChatHistory();
        }

        // Look for the compressed history section
        var match = Regex.Match(response, @"<COMPRESSED_HISTORY>(.*?)</COMPRESSED_HISTORY>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            _debugLogger.LogDebug("[UnifiedContextService] No compressed history section found in response");
            return new ChatHistory();
        }

        var compressedContent = match.Groups[1].Value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(compressedContent))
        {
            _debugLogger.LogDebug("[UnifiedContextService] Compressed history section is empty");
            return new ChatHistory();
        }

        var lines = compressedContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _debugLogger.LogDebug($"[UnifiedContextService] Processing {lines.Length} lines from compressed history");
        
        var compressedHistory = new ChatHistory();
        var messagesAdded = 0;
        
        foreach (var line in lines)
        {
            var trimmedLine = line?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            // Parse role-based format: [Role] Content
            var roleMatch = Regex.Match(trimmedLine, @"^\[(\w+)\]\s*(.*)$");
            if (roleMatch.Success)
            {
                var role = roleMatch.Groups[1].Value?.ToLowerInvariant() ?? string.Empty;
                var content = roleMatch.Groups[2].Value ?? string.Empty;

                if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(content))
                    continue;

                switch (role)
                {
                    case "system":
                        compressedHistory.AddSystemMessage(content);
                        messagesAdded++;
                        break;
                    case "user":
                        compressedHistory.AddUserMessage(content);
                        messagesAdded++;
                        break;
                    case "assistant":
                        compressedHistory.AddAssistantMessage(content);
                        messagesAdded++;
                        break;
                    default:
                        _debugLogger.LogDebug($"[UnifiedContextService] Skipping unknown role: {role}");
                        break;
                }
            }
        }

        _debugLogger.LogDebug($"[UnifiedContextService] Successfully added {messagesAdded} messages to compressed history");
        return compressedHistory.Count > 0 ? compressedHistory : new ChatHistory();
    }

    private static string ConvertRoleForDisplay(AuthorRole role)
    {
        return role.Label switch
        {
            "user" => "User",
            "assistant" => "Assistant", 
            "system" => "System",
            "tool" => "Tool",
            _ => role.ToString()
        };
    }

    private async Task<string> LoadSystemPromptAsync(string promptName)
    {
        _debugLogger.LogDebug($"[UnifiedContextService] Loading system prompt: {promptName}");
        
        var promptPath = GetPromptPath(promptName);
        if (File.Exists(promptPath))
        {
            var prompt = await File.ReadAllTextAsync(promptPath);
            _debugLogger.LogDebug($"[UnifiedContextService] Successfully loaded prompt from: {promptPath} (length: {prompt.Length})");
            return prompt;
        }
        
        var errorMsg = $"System prompt for {promptName} not found at {promptPath}";
        _debugLogger.LogError($"[UnifiedContextService] {errorMsg}");
        return errorMsg;
    }

    private string GetPromptPath(string promptName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDirectory, "Prompts", $"{promptName}.md");
    }

    private string GetRulesetContextElements(GamePhase currentPhase)
    {
        try
        {
            _debugLogger.LogDebug($"[UnifiedContextService] Getting ruleset context elements for phase: {currentPhase}");
            var contextElements = _rulesetManager.GetPhaseContextElements(currentPhase);
            var result = string.Join(", ", contextElements);
            _debugLogger.LogDebug($"[UnifiedContextService] Retrieved context elements: {result}");
            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error getting ruleset context elements: {ex.Message}";
            _debugLogger.LogError($"[UnifiedContextService] {errorMsg}", ex);
            System.Diagnostics.Debug.WriteLine($"[UnifiedContextService] {errorMsg}");
            return string.Empty;
        }
    }
}