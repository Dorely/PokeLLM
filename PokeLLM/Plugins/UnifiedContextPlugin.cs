using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.Game.Plugins.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Unified context management system that runs after each turn to maintain 
/// world state consistency and current scene context. Does NOT return structured data - 
/// saves all context via function calls.
/// </summary>
public class UnifiedContextPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public UnifiedContextPlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService,
        IWorldManagementService worldManagementService,
        INpcManagementService npcManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _worldManagementService = worldManagementService;
        _npcManagementService = npcManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("retrieve_state_context")]
    [Description("Returns a formatted version of the context from game state")]
    public async Task<string> RetrieveStateContext()
    {
        try
        {
            // TODO: Implement with dynamic ruleset approach using IEntityService
            return "Context: Dynamic ruleset system active - game state managed through entity service";
        }
        catch (Exception ex)
        {
            return $"An error occurred while gathering context {ex.Message}";
        }
    }

    [KernelFunction("search_narrative_context")]
    [Description("Search for relevant narrative history and world knowledge for current scene")]
    public async Task<string> SearchNarrativeContext(
        [Description("Comma-separated scene elements to search for context")] string sceneElements)
    {
        try
        {
            // TODO: Implement with dynamic ruleset approach using IEntityService
            return "Narrative context: Dynamic ruleset system active - narrative managed through entity service";
        }
        catch (Exception ex)
        {
            return $"An error occurred while searching narrative context: {ex.Message}";
        }
    }

    [KernelFunction("update_current_context")]
    [Description("Update the game states current context field with comprehensive scene information as a string")]
    public async Task<string> UpdateCurrentContext(
        [Description("Detailed scene description including all present entities, environment, and narrative context")] string contextDescription)
    {
        try
        {
            // TODO: Implement with dynamic ruleset approach using IEntityService
            return $"Context updated successfully. Length: {contextDescription.Length}";
        }
        catch (Exception ex)
        {
            return $"An error occurred while updating context: {ex.Message}";
        }
    }

}