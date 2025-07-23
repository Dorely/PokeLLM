using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.GameState;

public class GameStateRepository : IGameStateRepository
{
    private readonly string _gameStateDirectory;
    private readonly string _currentStateFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameStateRepository(string dataDirectory = "GameData")
    {
        _gameStateDirectory = dataDirectory;
        _currentStateFile = Path.Combine(_gameStateDirectory, "game_current_state.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        InitializeDirectories();
    }

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_gameStateDirectory);
    }

    public async Task<GameStateModel> CreateNewGameStateAsync(string playerName)
    {
        var state = new GameStateModel();
        state.Player.Character.Name = playerName;
        state.Player.Character.IsTrainer = true;
        state.Player.AvailableStatPoints = 1;
        state.AdventureSummary = $"New Game - The Player's name is {playerName}. Character Creation is not complete. The Adventure has not yet begun";

        await SaveStateAsync(state);

        return state;
    }

    public async Task SaveStateAsync(GameStateModel gameState)
    {
        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

        var json = JsonSerializer.Serialize(gameState, _jsonOptions);
        await File.WriteAllTextAsync(_currentStateFile, json);
    }

    public async Task<GameStateModel> LoadLatestStateAsync()
    {
        var json = await File.ReadAllTextAsync(_currentStateFile);
        var gameState = JsonSerializer.Deserialize<GameStateModel>(json, _jsonOptions);
        return gameState;
    }

    public async Task<bool> HasGameStateAsync()
    {
        await Task.Yield();
        return File.Exists(_currentStateFile);
    }
}