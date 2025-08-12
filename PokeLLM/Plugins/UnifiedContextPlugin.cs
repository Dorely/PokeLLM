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
            var gameState = await _gameStateRepo.LoadLatestStateAsync();

            // Get current location details
            var currentLocation = gameState.WorldLocations.GetValueOrDefault(gameState.CurrentLocationId);
            var vectorLocation = await _informationManagementService.GetLocationAsync(gameState.CurrentLocationId);

            // Get present NPCs with details
            var presentNpcs = new List<string>();
            if (currentLocation != null)
            {
                foreach (var npcId in currentLocation.PresentNpcIds)
                {
                    var npcDetails = await _npcManagementService.GetNpcDetails(npcId);
                    if (npcDetails != null)
                    {
                        presentNpcs.Add($"{npcDetails.Name} ({npcDetails.CharacterDetails.Class})");
                    }
                }
            }

            // Get present Pokemon
            var presentPokemon = new List<string>();
            if (currentLocation != null)
            {
                foreach (var pokemonId in currentLocation.PresentPokemonIds)
                {
                    var pokemon = gameState.WorldPokemon.GetValueOrDefault(pokemonId);
                    if (pokemon != null)
                    {
                        presentPokemon.Add($"{pokemon.Species} (Level {pokemon.Level})");
                    }
                }
            }

            // Build simple string response to avoid complex object serialization issues with Gemini
            var locationName = currentLocation?.Name ?? "Unknown Location";
            var description = vectorLocation?.Description ?? currentLocation?.Name ?? "";
            var exitsText = currentLocation?.Exits?.Keys != null ? string.Join(", ", currentLocation.Exits.Keys) : "";
            var npcText = string.Join(", ", presentNpcs);
            var pokemonText = string.Join(", ", presentPokemon);
            var eventsText = string.Join("; ", gameState.RecentEvents.TakeLast(3).Select(e => e.EventDescription));

            return $"Location: {locationName}\nDescription: {description}\nExits: {exitsText}\nNPCs: {npcText}\nPokemon: {pokemonText}\nTime: {gameState.TimeOfDay}\nWeather: {gameState.Weather}\nRegion: {gameState.Region}\nRecent Events: {eventsText}";
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
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var narrativeContext = new List<string>();
            
            // Parse comma-separated elements
            var elementList = string.IsNullOrWhiteSpace(sceneElements) 
                ? new List<string>() 
                : sceneElements.Split(',').Select(e => e.Trim()).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            
            // Search for relevant narrative memories
            foreach (var element in elementList)
            {
                var memories = await _informationManagementService.FindMemoriesAsync(
                    gameState.SessionId, element, null, 0.7);
                narrativeContext.AddRange(memories.Select(m => $"Memory: {m.EventSummary} (Turn {m.GameTurnNumber})"));
            }
            
            // Search for world lore
            var loreResults = await _informationManagementService.SearchLoreAsync(elementList);
            narrativeContext.AddRange(loreResults.Select(l => $"Lore: {l.Title} - {l.Content}"));
            
            return string.Join("\n", narrativeContext);
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
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Update the CurrentContext as a simple string
            gameState.CurrentContext = contextDescription;
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return $"Context updated successfully. Length: {contextDescription.Length}";
        }
        catch (Exception ex)
        {
            return $"An error occurred while updating context: {ex.Message}";
        }
    }

}