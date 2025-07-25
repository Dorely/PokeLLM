using Microsoft.SemanticKernel;
using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState.Models;
using System.Diagnostics;

namespace PokeLLM.Game.Orchestration;

public class PluginManager : IPluginManager
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IVectorStoreService _vectorStoreService;

    public PluginManager(IGameStateRepository gameStateRepository, IVectorStoreService vectorStoreService)
    {
        _gameStateRepository = gameStateRepository;
        _vectorStoreService = vectorStoreService;
    }

    public async Task LoadPluginsForPhaseAsync(Kernel kernel, GamePhase phase)
    {
        try
        {
            switch (phase)
            {
                case GamePhase.GameCreation:
                    await LoadGameCreationPluginsAsync(kernel);
                    break;
                    
                case GamePhase.CharacterCreation:
                    await LoadCharacterCreationPluginsAsync(kernel);
                    break;
                    
                case GamePhase.WorldGeneration:
                    await LoadWorldGenerationPluginsAsync(kernel);
                    break;
                    
                case GamePhase.Exploration:
                    await LoadExplorationPluginsAsync(kernel);
                    break;
                    
                case GamePhase.Combat:
                    await LoadCombatPluginsAsync(kernel);
                    break;
                    
                case GamePhase.LevelUp:
                    await LoadLevelUpPluginsAsync(kernel);
                    break;
                    
                default:
                    Debug.WriteLine($"Warning: Unknown phase {phase}, loading default plugins");
                    await LoadGameCreationPluginsAsync(kernel);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading plugins for phase {phase}: {ex.Message}");
        }
    }

    public async Task LoadContextGatheringPluginsAsync(Kernel kernel)
    {
        try
        {
            // Load all available plugins for comprehensive context gathering
            var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
            kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
            
            var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
            kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
            
            var dicePlugin = new DicePlugin(_gameStateRepository);
            kernel.ImportPluginFromObject(dicePlugin, "Dice");
            
            Debug.WriteLine("Context gathering plugins loaded: VectorStore, GameEngine, Dice");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading context gathering plugins: {ex.Message}");
        }
    }

    public async Task ClearAllPluginsAsync(Kernel kernel)
    {
        try
        {
            kernel.Plugins.Clear();
            Debug.WriteLine("All plugins cleared from kernel");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to clear plugins: {ex.Message}");
        }
    }

    private async Task LoadGameCreationPluginsAsync(Kernel kernel)
    {
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        Debug.WriteLine("Loaded Game Creation plugins: PhaseTransition, GameEngine");
    }

    private async Task LoadCharacterCreationPluginsAsync(Kernel kernel)
    {
        var characterCreationPlugin = new CharacterCreationPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(characterCreationPlugin, "CharacterCreation");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        Debug.WriteLine("Loaded Character Creation plugins: CharacterCreation, PhaseTransition, Dice");
    }

    private async Task LoadWorldGenerationPluginsAsync(Kernel kernel)
    {
        var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
        kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        Debug.WriteLine("Loaded World Generation plugins: VectorStore, PhaseTransition, GameEngine, Dice");
    }

    private async Task LoadExplorationPluginsAsync(Kernel kernel)
    {
        var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
        kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
        
        var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        Debug.WriteLine("Loaded Exploration plugins: VectorStore, GameEngine, Dice, PhaseTransition");
    }

    private async Task LoadCombatPluginsAsync(Kernel kernel)
    {
        var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
        kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
        
        Debug.WriteLine("Loaded Combat plugins: GameEngine, Dice, PhaseTransition, VectorStore");
    }

    private async Task LoadLevelUpPluginsAsync(Kernel kernel)
    {
        var characterCreationPlugin = new CharacterCreationPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(characterCreationPlugin, "CharacterCreation");
        
        var gameEnginePlugin = new GameStatePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        Debug.WriteLine("Loaded Level Up plugins: CharacterCreation, GameEngine, Dice, PhaseTransition");
    }
}