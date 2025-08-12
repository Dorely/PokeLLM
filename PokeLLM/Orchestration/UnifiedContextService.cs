using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.GameState;
using System.Text.RegularExpressions;

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
    private Kernel _contextKernel;

    public UnifiedContextService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository,
        IServiceProvider serviceProvider)
    {
        _llmProvider = llmProvider;
        _serviceProvider = serviceProvider;
        _gameStateRepository = gameStateRepository;
        InitializeKernel();
    }

    private void InitializeKernel()
    {
        _contextKernel = _llmProvider.CreateKernelAsync().GetAwaiter().GetResult();
        _contextKernel.Plugins.AddFromType<UnifiedContextPlugin>("UnifiedContext", _serviceProvider);
    }

    public async Task<ContextManagementResult> RunContextManagementAsync(ChatHistory history, string directive, CancellationToken cancellationToken = default)
    {
        var contextHistory = new ChatHistory();
        var systemPrompt = await LoadSystemPromptAsync("UnifiedContextSubroutine");

        var gameState = await _gameStateRepository.LoadLatestStateAsync();

        // Process history to ensure role compatibility with Gemini
        var processedHistory = history
            .Where(msg => msg.Role != AuthorRole.System && !string.IsNullOrWhiteSpace(msg.Content))
            .ToList();

        var historyString = string.Join("\n\n", history
            .Where(msg => msg.Role != AuthorRole.System && !string.IsNullOrWhiteSpace(msg.Content))
            .Select(msg => msg.Content));

        // Check if history needs compression (based on the formatted string that will be sent)
        const int maxMessages = 20;
        const int maxCharacters = 50000;
        var needsCompression = processedHistory.Count > maxMessages || historyString.Length > maxCharacters;

        var currentContext = !string.IsNullOrEmpty(gameState.CurrentContext) ?
            gameState.CurrentContext : "World generation beginning - creating initial world content.";

        // Add compression directive if needed
        var finalDirective = directive;
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
        }

        //Replace {{context}} placeholder with actual context
        systemPrompt = systemPrompt.Replace("{{history}}", historyString);
        systemPrompt = systemPrompt.Replace("{{context}}", currentContext);

        contextHistory.AddSystemMessage(systemPrompt);
        contextHistory.AddUserMessage(finalDirective);


        var chatService = _contextKernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 3000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        var result = await chatService.GetChatMessageContentAsync(
            contextHistory, executionSettings, _contextKernel, cancellationToken);

        // Handle function calls manually since we're using EnableKernelFunctions
        var response = result.ToString();
        
        // Ensure we always have a response, even if functions were called
        if (string.IsNullOrWhiteSpace(response))
        {
            response = "Context management completed successfully.";
        }
        
        var compressedHistory = ExtractCompressedHistory(response);

        return new ContextManagementResult
        {
            Response = response,
            CompressedHistory = compressedHistory,
            HistoryWasCompressed = needsCompression
        };
    }

    private ChatHistory ExtractCompressedHistory(string response)
    {
        // Look for the compressed history section
        var match = Regex.Match(response, @"<COMPRESSED_HISTORY>(.*?)</COMPRESSED_HISTORY>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return new ChatHistory();
        }

        var compressedContent = match.Groups[1].Value.Trim();
        var lines = compressedContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var compressedHistory = new ChatHistory();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            // Parse role-based format: [Role] Content
            var roleMatch = Regex.Match(trimmedLine, @"^\[(\w+)\]\s*(.*)$");
            if (roleMatch.Success)
            {
                var role = roleMatch.Groups[1].Value.ToLowerInvariant();
                var content = roleMatch.Groups[2].Value;

                switch (role)
                {
                    case "system":
                        compressedHistory.AddSystemMessage(content);
                        break;
                    case "user":
                        compressedHistory.AddUserMessage(content);
                        break;
                    case "assistant":
                        compressedHistory.AddAssistantMessage(content);
                        break;
                    default:
                        // Skip unknown roles
                        break;
                }
            }
        }

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
        var promptPath = GetPromptPath(promptName);
        if (File.Exists(promptPath))
        {
            return await File.ReadAllTextAsync(promptPath);
        }
        return $"System prompt for {promptName} not found.";
    }

    private string GetPromptPath(string promptName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDirectory, "Prompts", $"{promptName}.md");
    }
}