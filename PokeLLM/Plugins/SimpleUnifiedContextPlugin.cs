using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Simplified UnifiedContextPlugin to test Gemini compatibility with basic data types only
/// </summary>
public class SimpleUnifiedContextPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public SimpleUnifiedContextPlugin(
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

    [KernelFunction("gather_basic_context")]
    [Description("Gather basic context about the current scene")]
    public async Task<string> GatherBasicContext()
    {
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        
        // Return only basic strings
        var locationName = gameState.WorldLocations.GetValueOrDefault(gameState.CurrentLocationId)?.Name ?? "Unknown Location";
        var timeOfDay = gameState.TimeOfDay.ToString();
        var weather = gameState.Weather.ToString();
        var region = gameState.Region;
        
        return $"Location: {locationName}, Time: {timeOfDay}, Weather: {weather}, Region: {region}";
    }

    [KernelFunction("search_simple_context")]
    [Description("Search for simple narrative context")]
    public async Task<string> SearchSimpleContext(
        [Description("Single search term")] string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return "No search term provided";
        }
        
        // Just return a simple message instead of calling the complex service
        return $"Searched for context related to '{searchTerm}'";
    }

    [KernelFunction("update_simple_context")]
    [Description("Update context with simple string")]
    public async Task<string> UpdateSimpleContext(
        [Description("Context description")] string contextDescription)
    {
        var gameState = await _gameStateRepo.LoadLatestStateAsync();
        
        gameState.CurrentContext = contextDescription ?? "";
        await _gameStateRepo.SaveStateAsync(gameState);
        
        return $"Context updated. Length: {contextDescription?.Length ?? 0}";
    }
}