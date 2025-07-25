using PokeLLM.GameState.Models;

namespace PokeLLM.Game.GameLoop.Interfaces;

/// <summary>
/// Service responsible for orchestrating the main game loop with context gathering.
/// </summary>
public interface IGameLoopService
{
    /// <summary>
    /// Processes player input through the new architecture:
    /// 1. Gathers context using the Context Gathering Subroutine
    /// 2. Processes the context (placeholder for future use)
    /// 3. Passes context and input to the main game chat
    /// </summary>
    /// <param name="playerInput">The player's input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Streaming response from the main game chat</returns>
    IAsyncEnumerable<string> ProcessPlayerInputAsync(string playerInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-streaming version of ProcessPlayerInputAsync
    /// </summary>
    /// <param name="playerInput">The player's input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete response from the main game chat</returns>
    Task<string> ProcessPlayerInputCompleteAsync(string playerInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the initial welcome message when starting the game
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Welcome message</returns>
    IAsyncEnumerable<string> GetWelcomeMessageAsync(CancellationToken cancellationToken = default);
}