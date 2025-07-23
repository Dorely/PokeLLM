using PokeLLM.GameState.Models;

namespace PokeLLM.GameState.Interfaces;

public interface IGameStateRepository
{
    Task<GameStateModel> CreateNewGameStateAsync(string playerName);
    Task SaveStateAsync(GameStateModel gameState);
    Task<GameStateModel> LoadLatestStateAsync();
    Task<bool> HasGameStateAsync();
}