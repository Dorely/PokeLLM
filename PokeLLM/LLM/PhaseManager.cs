using Microsoft.SemanticKernel;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.LLM;

public interface IPhaseManager
{
    Task<string> GetSystemPromptForPhaseAsync(GamePhase phase);
    void RegisterPluginsForPhase(Kernel kernel, GamePhase phase, IVectorStoreService vectorStoreService, IGameStateRepository gameStateRepository);
    Task<GamePhase> TransitionToPhaseAsync(GamePhase newPhase, IGameStateRepository gameStateRepository);
}

public class PhaseManager : IPhaseManager
{
    public async Task<string> GetSystemPromptForPhaseAsync(GamePhase phase)
    {
        try
        {
            var promptFileName = GetPromptFileNameForPhase(phase);
            var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", promptFileName);
            return await File.ReadAllTextAsync(promptPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load system prompt for phase {phase}. Error: {ex.Message}");
            return GetFallbackPromptForPhase(phase);
        }
    }

    public void RegisterPluginsForPhase(Kernel kernel, GamePhase phase, IVectorStoreService vectorStoreService, IGameStateRepository gameStateRepository)
    {
        // Clear existing plugins
        kernel.Plugins.Clear();

        // Always include core engine functionality
        kernel.Plugins.AddFromObject(new GameEnginePlugin(gameStateRepository));

        // Add phase-specific plugins
        switch (phase)
        {
            case GamePhase.GameCreation:
                kernel.Plugins.AddFromObject(new PhaseTransitionPlugin(gameStateRepository));
                break;

            case GamePhase.CharacterCreation:
                kernel.Plugins.AddFromObject(new CharacterCreationPlugin(gameStateRepository));
                kernel.Plugins.AddFromObject(new PhaseTransitionPlugin(gameStateRepository));
                break;

            case GamePhase.WorldGeneration:
                kernel.Plugins.AddFromObject(new VectorStorePlugin(vectorStoreService));
                kernel.Plugins.AddFromObject(new PhaseTransitionPlugin(gameStateRepository));
                // Add world generation specific plugins here when implemented
                break;

            case GamePhase.Exploration:
                kernel.Plugins.AddFromObject(new VectorStorePlugin(vectorStoreService));
                kernel.Plugins.AddFromObject(new DicePlugin(gameStateRepository));
                kernel.Plugins.AddFromObject(new PhaseTransitionPlugin(gameStateRepository));
                // Add exploration specific plugins here
                break;

            case GamePhase.Combat:
                kernel.Plugins.AddFromObject(new DicePlugin(gameStateRepository));
                kernel.Plugins.AddFromObject(new PhaseTransitionPlugin(gameStateRepository));
                // STUB: Add combat-specific plugins when implemented
                break;

            case GamePhase.LevelUp:
                kernel.Plugins.AddFromObject(new CharacterCreationPlugin(gameStateRepository));
                kernel.Plugins.AddFromObject(new PhaseTransitionPlugin(gameStateRepository));
                // Add level up specific plugins here
                break;
        }
    }

    public async Task<GamePhase> TransitionToPhaseAsync(GamePhase newPhase, IGameStateRepository gameStateRepository)
    {
        var gameState = await gameStateRepository.LoadLatestStateAsync();
        if (gameState == null)
        {
            throw new InvalidOperationException("Cannot transition phases without a valid game state");
        }

        var oldPhase = gameState.CurrentPhase;
        gameState.CurrentPhase = newPhase;
        gameState.LastSaveTime = DateTime.UtcNow;
        
        await gameStateRepository.SaveStateAsync(gameState);
        
        Console.WriteLine($"Phase transition: {oldPhase} -> {newPhase}");
        return newPhase;
    }

    private string GetPromptFileNameForPhase(GamePhase phase) => phase switch
    {
        GamePhase.GameCreation => "GameCreationPhase.md",
        GamePhase.CharacterCreation => "CharacterCreationPhase.md",
        GamePhase.WorldGeneration => "WorldGenerationPhase.md",
        GamePhase.Exploration => "ExplorationPhase.md",
        GamePhase.Combat => "CombatPhase.md",
        GamePhase.LevelUp => "LevelUpPhase.md",
        _ => "SystemPrompt.md" // Fallback to original prompt
    };

    private string GetFallbackPromptForPhase(GamePhase phase) => phase switch
    {
        GamePhase.GameCreation => "You are PokeLLM in Game Creation phase. Ask for player name and create new game.",
        GamePhase.CharacterCreation => "You are PokeLLM in Character Creation phase. Guide stat allocation and character setup.",
        GamePhase.WorldGeneration => "You are PokeLLM in World Generation phase. Populate the world with locations, NPCs, and storylines.",
        GamePhase.Exploration => "You are PokeLLM in Exploration phase. Facilitate immersive storytelling and world exploration.",
        GamePhase.Combat => "You are PokeLLM in Combat phase. Manage tactical Pokémon battles.",
        GamePhase.LevelUp => "You are PokeLLM in Level Up phase. Handle character and Pokémon advancement.",
        _ => "You are PokeLLM, a Pokémon adventure game. There has been an error loading the game phase."
    };
}