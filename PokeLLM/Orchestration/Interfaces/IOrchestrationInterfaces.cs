using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Orchestration.Interfaces;


/// <summary>
/// Manages conversation history for different game phases
/// </summary>
public interface IConversationHistoryManager
{
    Task<ChatHistory> GetHistoryAsync(GamePhase phase);
    Task AddUserMessageAsync(GamePhase phase, string message);
    Task AddAssistantMessageAsync(GamePhase phase, string message);
    Task AddSystemMessageAsync(GamePhase phase, string message);
    Task SummarizeHistoryIfNeededAsync(GamePhase phase);
    Task HandlePhaseTransitionAsync(GamePhase oldPhase, GamePhase newPhase);
    ChatHistory CreateHistory();
}

/// <summary>
/// Manages plugins for different game phases
/// </summary>
public interface IPluginManager
{
    Task LoadPluginsForPhaseAsync(Microsoft.SemanticKernel.Kernel kernel, GamePhase phase);
    Task LoadContextGatheringPluginsAsync(Microsoft.SemanticKernel.Kernel kernel);
    Task ClearAllPluginsAsync(Microsoft.SemanticKernel.Kernel kernel);
}

/// <summary>
/// Manages prompt loading and processing
/// </summary>
public interface IPromptManager
{
    Task<string> LoadSystemPromptAsync(GamePhase phase);
    Task<string> LoadContextGatheringPromptAsync();
    Task<string> LoadChatManagementPromptAsync();
    string CreateContextGatheringPrompt(string contextGatheringPrompt, string playerInput, string adventureSummary, List<string> recentHistory);
}