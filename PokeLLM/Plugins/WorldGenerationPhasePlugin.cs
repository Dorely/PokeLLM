using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic;
using PokeLLM.Game.Plugins.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for procedural world generation utilities and phase management
/// </summary>
public class WorldGenerationPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public WorldGenerationPhasePlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Procedural Generation Functions

    [KernelFunction("generate_procedural_content")]
    [Description("Use dice rolls and random generation for procedural world building")]
    public async Task<string> GenerateProceduralContent(
        [Description("Generation type: 'random_number', 'random_choice', 'dice_roll', 'random_stats'")] string generationType,
        [Description("Number of sides for dice (if applicable)")] int sides = 20,
        [Description("Number of dice to roll")] int count = 1,
        [Description("List of choices for random selection")] ChoicesDto choices = null,
        [Description("Minimum value (for ranges)")] int minValue = 1,
        [Description("Maximum value (for ranges)")] int maxValue = 100)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] GenerateProceduralContent called: {generationType}");
        
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(generationType))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Generation type is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            var validTypes = new[] { "random_number", "random_choice", "dice_roll", "random_stats" };
            if (!validTypes.Contains(generationType.ToLower()))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Invalid generation type '{generationType}'. Valid types are: {string.Join(", ", validTypes)}" 
                }, _jsonOptions);
            }

            switch (generationType.ToLower())
            {
                case "dice_roll":
                    if (sides <= 0)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Number of sides must be greater than 0" 
                        }, _jsonOptions);
                    }

                    if (count <= 0)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Number of dice to roll must be greater than 0" 
                        }, _jsonOptions);
                    }

                    var diceResult = await _gameLogicService.RollDiceAsync(sides, count);
                    return JsonSerializer.Serialize(diceResult, _jsonOptions);
                    
                case "random_choice":
                    if (choices == null || choices.Choices == null || choices.Choices.Count == 0 || choices.Choices.All(string.IsNullOrWhiteSpace))
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Choices list is required for random selection and must contain at least one non-empty choice" 
                        }, _jsonOptions);
                    }
                    
                    var validChoices = choices.Choices.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                    var choiceResult = await _gameLogicService.MakeRandomDecisionFromOptionsAsync(validChoices);
                    return JsonSerializer.Serialize(choiceResult, _jsonOptions);
                    
                case "random_number":
                    if (minValue >= maxValue)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = "Maximum value must be greater than minimum value" 
                        }, _jsonOptions);
                    }

                    var randomRoll = await _gameLogicService.RollDiceAsync(maxValue - minValue + 1, 1);
                    var randomValue = randomRoll.Total + minValue - 1;
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        value = randomValue,
                        min = minValue,
                        max = maxValue,
                        generationType = generationType
                    }, _jsonOptions);
                    
                case "random_stats":
                    // Generate random RPG stats (3d6 for each)
                    var stats = new Dictionary<string, int>();
                    var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
                    
                    foreach (var statName in statNames)
                    {
                        var statRoll = await _gameLogicService.RollDiceAsync(6, 3);
                        stats[statName] = statRoll.Total;
                    }
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        stats = stats,
                        generationType = generationType
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"Unknown generation type: {generationType}" 
                    }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in GenerateProceduralContent: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    #endregion

    #region Phase Management

    [KernelFunction("finalize_world_generation")]
    [Description("Complete world generation and transition to exploration phase")]
    public async Task<string> FinalizeWorldGeneration(
        [Description("Opening scenario context that will start the adventure")] string openingScenario)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] FinalizeWorldGeneration called");
        
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(openingScenario))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Opening scenario is required and cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Verify we have a region selected
            if (string.IsNullOrEmpty(gameState.Region))
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false,
                    error = "Cannot finalize world generation - no region has been set",
                    requiresRegionSelection = true
                }, _jsonOptions);
            }
            
            // Transition to exploration phase
            gameState.CurrentPhase = GamePhase.Exploration;
            gameState.PhaseChangeSummary = $"World generation completed successfully. {openingScenario}";
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"World Generation Completed: {openingScenario}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            // Store the opening scenario for immediate use
            await _informationManagementService.UpsertLoreAsync(
                $"opening_scenario_{gameState.SessionId}",
                "opening_scenario",
                $"Opening Scenario for {gameState.Region}",
                openingScenario,
                new List<string> { "opening_scenario", "character_creation", gameState.Region.ToLower() }
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "World generation completed successfully",
                region = gameState.Region,
                nextPhase = "Exploration",
                openingScenario = openingScenario,
                sessionId = gameState.SessionId,
                phaseTransitionCompleted = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorldGenerationPhasePlugin] Error in FinalizeWorldGeneration: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    #endregion
}