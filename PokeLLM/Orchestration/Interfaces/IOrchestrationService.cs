using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Orchestration.Interfaces;

/// <summary>
/// Core orchestration service that manages common LLM operations and can be used by multiple providers
/// </summary>
public interface IOrchestrationService
{
    /// <summary>
    /// Executes a prompt with the current game phase context, history management, and plugin loading
    /// </summary>
    Task<string> ExecutePromptAsync(string prompt, GamePhase? phase = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a prompt with streaming response
    /// </summary>
    IAsyncEnumerable<string> ExecutePromptStreamingAsync(string prompt, GamePhase? phase = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes context gathering with specialized plugins and prompt
    /// </summary>
    Task<GameContext> ExecuteContextGatheringAsync(string playerInput, string adventureSummary, List<string> recentHistory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current game phase
    /// </summary>
    Task<GamePhase> GetCurrentPhaseAsync();

    /// <summary>
    /// Forces a refresh of the current phase from game state
    /// </summary>
    Task RefreshPhaseAsync();
}