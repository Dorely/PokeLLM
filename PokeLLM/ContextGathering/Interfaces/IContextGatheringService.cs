using PokeLLM.GameState.Models;

namespace PokeLLM.Game.ContextGathering.Interfaces;

/// <summary>
/// Service responsible for gathering context before the main game chat processes player input.
/// </summary>
public interface IContextGatheringService
{
    /// <summary>
    /// Gathers all necessary context for the main game chat to properly respond to player input.
    /// </summary>
    /// <param name="playerInput">The player's input that needs context</param>
    /// <param name="adventureSummary">High-level summary of the adventure so far</param>
    /// <param name="recentHistory">Recent conversation history from the active phase</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A structured GameContext object containing all gathered context</returns>
    Task<GameContext> GatherContextAsync(
        string playerInput, 
        string adventureSummary, 
        List<string> recentHistory, 
        CancellationToken cancellationToken = default);
}