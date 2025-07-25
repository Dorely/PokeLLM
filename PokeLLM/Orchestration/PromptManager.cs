using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.GameState.Models;
using System.Diagnostics;

namespace PokeLLM.Game.Orchestration;

public class PromptManager : IPromptManager
{
    private readonly IGameStateRepository _gameStateRepository;

    public PromptManager(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<string> LoadSystemPromptAsync(GamePhase phase)
    {
        try
        {
            var promptPath = phase switch
            {
                GamePhase.GameCreation => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md"),
                GamePhase.CharacterCreation => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CharacterCreationPhase.md"),
                GamePhase.WorldGeneration => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "WorldGenerationPhase.md"),
                GamePhase.Exploration => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ExplorationPhase.md"),
                GamePhase.Combat => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CombatPhase.md"),
                GamePhase.LevelUp => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "LevelUpPhase.md"),
                _ => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md")
            };
            var systemPrompt = await File.ReadAllTextAsync(promptPath);

            // Add phase transition context if available
            var gameState = await _gameStateRepository.LoadLatestStateAsync();
            if (gameState != null)
            {
                // Add previous phase conversation summary for context
                if (!string.IsNullOrEmpty(gameState.PreviousPhaseConversationSummary))
                {
                    systemPrompt += $"\n\n## Previous Phase Context\n{gameState.PreviousPhaseConversationSummary}";
                }

                // Add phase change summary (reason for transition)
                if (!string.IsNullOrEmpty(gameState.PhaseChangeSummary))
                {
                    systemPrompt += $"\n\n## Phase Change Summary\n{gameState.PhaseChangeSummary}";
                }
            }

            return systemPrompt;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load system prompt for phase {phase}: {ex.Message}");
            return "There has been an error loading the game prompt. Do not continue";
        }
    }

    public async Task<string> LoadContextGatheringPromptAsync()
    {
        try
        {
            var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ContextGatheringSubroutine.md");
            return await File.ReadAllTextAsync(promptPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load context gathering prompt: {ex.Message}");
            return @"You are a context gathering subroutine. Your job is to gather all necessary context for the main game chat.
                     Use the available functions to search for entities and information relevant to the player input.
                     Return a structured JSON response with your findings.";
        }
    }

    public async Task<string> LoadChatManagementPromptAsync()
    {
        try
        {
            var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ChatManagementPrompt.md");
            return await File.ReadAllTextAsync(promptPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load chat management prompt: {ex.Message}");
            return "You are a helpful assistant that summarizes conversations concisely while preserving important details.";
        }
    }

    public string CreateContextGatheringPrompt(string contextGatheringPrompt, string playerInput, string adventureSummary, List<string> recentHistory)
    {
        var recentHistoryText = string.Join("\n", recentHistory);

        return $@"{contextGatheringPrompt}

## Current Task
Gather context for the following player input:

**Player Input**: {playerInput}

**Adventure Summary**: {adventureSummary}

**Recent History**: 
{recentHistoryText}

Use the available functions to gather all necessary context. When you have completed your research, respond with a JSON object that matches the GameContext structure with the following properties:
- relevantEntities: Dictionary of entity IDs to entity objects
- missingEntities: Array of entity names that were referenced but not found
- gameStateUpdates: Array of strings describing any changes made
- vectorStoreData: Array of VectorStoreResult objects with relevant information
- contextSummary: String summary of gathered context
- recommendedActions: Array of suggested actions for the main game chat

Begin your context gathering now:";
    }
}