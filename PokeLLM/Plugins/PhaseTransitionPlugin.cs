using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing game phase transitions
/// </summary>
public class PhaseTransitionPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public PhaseTransitionPlugin(IGameStateRepository repository)
    {
        _repository = repository;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("transition_to_character_creation")]
    [Description("Transition from Game Creation to Character Creation phase")]
    public async Task<string> TransitionToCharacterCreation([Description("A summary what has taken place and why the phase is changing")] string phaseChangeSummary)
    {
        Debug.WriteLine($"[PhaseTransitionPlugin] TransitionToCharacterCreation called");
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.CurrentPhase != GamePhase.GameCreation)
                return JsonSerializer.Serialize(new { error = $"Cannot transition to Character Creation from {gameState.CurrentPhase}" }, _jsonOptions);

            gameState.CurrentPhase = GamePhase.CharacterCreation;
            gameState.LastSaveTime = DateTime.UtcNow;
            gameState.PhaseChangeSummary = phaseChangeSummary;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = "Transitioned to Character Creation phase",
                newPhase = GamePhase.CharacterCreation 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to transition phase: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("transition_to_world_generation")]
    [Description("Transition from Character Creation to World Generation phase")]
    public async Task<string> TransitionToWorldGeneration([Description("A summary what has taken place and why the phase is changing")] string phaseChangeSummary)
    {
        Debug.WriteLine($"[PhaseTransitionPlugin] TransitionToWorldGeneration called");
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.CurrentPhase != GamePhase.CharacterCreation)
                return JsonSerializer.Serialize(new { error = $"Cannot transition to World Generation from {gameState.CurrentPhase}" }, _jsonOptions);

            if (!gameState.Player.CharacterCreationComplete)
                return JsonSerializer.Serialize(new { error = "Character creation must be completed first" }, _jsonOptions);

            gameState.CurrentPhase = GamePhase.WorldGeneration;
            gameState.LastSaveTime = DateTime.UtcNow;
            gameState.PhaseChangeSummary = phaseChangeSummary;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = "Transitioned to World Generation phase",
                newPhase = GamePhase.WorldGeneration 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to transition phase: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("transition_to_exploration")]
    [Description("Transition to Exploration phase from World Generation or other phases")]
    public async Task<string> TransitionToExploration([Description("A summary what has taken place and why the phase is changing")] string phaseChangeSummary)
    {
        Debug.WriteLine($"[PhaseTransitionPlugin] TransitionToExploration called");
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Allow transition from multiple phases
            var validPhases = new[] { GamePhase.WorldGeneration, GamePhase.Combat, GamePhase.LevelUp };
            if (!validPhases.Contains(gameState.CurrentPhase))
                return JsonSerializer.Serialize(new { error = $"Cannot transition to Exploration from {gameState.CurrentPhase}" }, _jsonOptions);

            gameState.CurrentPhase = GamePhase.Exploration;
            gameState.LastSaveTime = DateTime.UtcNow;
            gameState.PhaseChangeSummary = phaseChangeSummary;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = "Transitioned to Exploration phase",
                newPhase = GamePhase.Exploration 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to transition phase: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("transition_to_combat")]
    [Description("Transition to Combat phase from Exploration")]
    public async Task<string> TransitionToCombat([Description("A summary what has taken place and why the phase is changing")] string phaseChangeSummary)
    {
        Debug.WriteLine($"[PhaseTransitionPlugin] TransitionToCombat called");
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.CurrentPhase != GamePhase.Exploration)
                return JsonSerializer.Serialize(new { error = $"Cannot transition to Combat from {gameState.CurrentPhase}" }, _jsonOptions);

            gameState.CurrentPhase = GamePhase.Combat;
            gameState.LastSaveTime = DateTime.UtcNow;
            gameState.PhaseChangeSummary = phaseChangeSummary;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = "Transitioned to Combat phase",
                newPhase = GamePhase.Combat 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to transition phase: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("transition_to_level_up")]
    [Description("Transition to Level Up phase from Combat or Exploration")]
    public async Task<string> TransitionToLevelUp([Description("A summary what has taken place and why the phase is changing")] string phaseChangeSummary)
    {
        Debug.WriteLine($"[PhaseTransitionPlugin] TransitionToLevelUp called");
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var validPhases = new[] { GamePhase.Combat, GamePhase.Exploration };
            if (!validPhases.Contains(gameState.CurrentPhase))
                return JsonSerializer.Serialize(new { error = $"Cannot transition to Level Up from {gameState.CurrentPhase}" }, _jsonOptions);

            gameState.CurrentPhase = GamePhase.LevelUp;
            gameState.LastSaveTime = DateTime.UtcNow;
            gameState.PhaseChangeSummary = phaseChangeSummary;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = "Transitioned to Level Up phase",
                newPhase = GamePhase.LevelUp 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to transition phase: {ex.Message}" }, _jsonOptions);
        }
    }
}