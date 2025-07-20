using PokeLLM.GameState.Models;

namespace PokeLLM.GameState.Interfaces;

public interface IGameStateRepository
{
    Task<GameStateModel> CreateNewGameStateAsync(string characterName = "Trainer");
    Task SaveStateAsync(GameStateModel gameState);
    Task<GameStateModel?> LoadLatestStateAsync();
    Task<GameStateModel?> LoadStateByIdAsync(string stateId);
    Task<List<GameStateModel>> GetAllStatesAsync(int limit = 50);
    Task DeleteStateAsync(string stateId);
    Task UpdateCharacterAsync(Action<Character> updateAction);
    Task UpdateAdventureAsync(Action<Adventure> updateAction);
    Task AddPokemonToTeamAsync(Pokemon pokemon);
    Task AddEventToHistoryAsync(GameEvent gameEvent);
    Task<bool> HasGameStateAsync();
}