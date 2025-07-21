using PokeLLM.GameState.Models;

namespace PokeLLM.GameState.Interfaces;

public interface IGameStateRepository
{
    Task<GameStateModel> CreateNewGameStateAsync(string trainerName = "Trainer");
    Task SaveStateAsync(GameStateModel gameState);
    Task<GameStateModel?> LoadLatestStateAsync();
    Task<GameStateModel?> LoadStateByIdAsync(string stateId);
    Task<List<GameStateModel>> GetAllStatesAsync(int limit = 50);
    Task DeleteStateAsync(string stateId);
    Task UpdateTrainerAsync(Action<TrainerState> updateAction);
    Task UpdateWorldStateAsync(Action<GameWorldState> updateAction);
    Task AddPokemonToTeamAsync(Pokemon pokemon);
    Task<bool> HasGameStateAsync();
}