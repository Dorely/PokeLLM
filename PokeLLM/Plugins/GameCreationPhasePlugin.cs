using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling game creation, initialization, and regional selection
/// </summary>
public class GameCreationPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IWorldManagementService _worldManagementService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameCreationPhasePlugin(
        IGameStateRepository gameStateRepo,
        IWorldManagementService worldManagementService,
        IInformationManagementService informationManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _worldManagementService = worldManagementService;
        _informationManagementService = informationManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("set_region")]
    [Description("Sets the Region chosen by the player")]
    public async Task<string> ManageRegionalSelection(
        [Description("Region name to set")] string regionName,
        [Description("Full Details to be stored in the vector database")] LoreVectorRecord regionRecord)
    {
        Debug.WriteLine($"[GameCreationPhasePlugin] ManageRegionalSelection called with region: {regionName}");
        
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(regionName))
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false,
                    error = "Region name cannot be empty"
                }, _jsonOptions);
            }

            if (regionRecord == null)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false,
                    error = "Region record cannot be null"
                }, _jsonOptions);
            }

            // Set the region in the game state using WorldManagementService
            var setRegionResult = await _worldManagementService.SetRegionAsync(regionName);
            
            // Store the region details in the vector database using InformationManagementService
            var upsertResult = await _informationManagementService.UpsertLoreAsync(
                regionRecord.EntryId, 
                regionRecord.EntryType, 
                regionRecord.Title, 
                regionRecord.Content, 
                regionRecord.Tags?.ToList(),
                regionRecord.Id == Guid.Empty ? null : regionRecord.Id
            );

            // Get current game state to include in response
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Log the region selection event
            await _informationManagementService.LogNarrativeEventAsync(
                "region_selected",
                $"Player selected the {regionName} region for their adventure",
                $"Region: {regionName}\nDescription: {regionRecord.Content}",
                new List<string> { "player" },
                "",
                null,
                gameState.GameTurnNumber
            );

            Debug.WriteLine($"[GameCreationPhasePlugin] Region {regionName} set successfully");
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Region {regionName} selected successfully",
                regionName = regionName,
                gameStateResult = setRegionResult,
                vectorStoreResult = upsertResult,
                sessionId = gameState.SessionId
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameCreationPhasePlugin] Error in ManageRegionalSelection: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("search_existing_region_knowledge")]
    [Description("Search for existing region information in the vector database")]
    public async Task<string> QueryRegionData(
        [Description("Search Query for stored information")] string query)
    {
        Debug.WriteLine($"[GameCreationPhasePlugin] QueryRegionData called with query: {query}");
        
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false,
                    error = "Search query cannot be empty"
                }, _jsonOptions);
            }

            // Search for region information in the lore records
            var loreResults = await _informationManagementService.SearchLoreAsync(
                new List<string> { query }, 
                "Region" // Filter by region type if this is how regions are stored
            );

            // Also search for any region-related entries without type filter
            var generalLoreResults = await _informationManagementService.SearchLoreAsync(
                new List<string> { query, $"{query} region", $"{query} area" }
            );

            // Combine and deduplicate results
            var allResults = loreResults.Concat(generalLoreResults)
                .GroupBy(r => r.EntryId)
                .Select(g => g.First())
                .ToList();

            Debug.WriteLine($"[GameCreationPhasePlugin] Found {allResults.Count} region-related records");

            var response = new
            {
                success = true,
                query = query,
                resultsCount = allResults.Count,
                results = allResults.Select(r => new
                {
                    entryId = r.EntryId,
                    entryType = r.EntryType,
                    title = r.Title,
                    content = r.Content,
                    tags = r.Tags
                }).ToList()
            };

            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameCreationPhasePlugin] Error in QueryRegionData: {ex.Message}");
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("finalize_game_creation")]
    [Description("Complete the game creation phase and transition to world generation")]
    public async Task<string> FinalizeGameCreation(
        [Description("Summary of the game creation process")] string creationSummary)
    {
        Debug.WriteLine($"[GameCreationPhasePlugin] FinalizeGameCreation called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Verify that a region has been selected
            if (string.IsNullOrEmpty(gameState.Region))
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false,
                    error = "Cannot finalize game creation - no region has been selected",
                    requiresRegionSelection = true
                }, _jsonOptions);
            }
            
            // Set the phase to WorldGeneration
            gameState.CurrentPhase = GamePhase.WorldGeneration;
            
            // Set the phase change summary
            var fullSummary = $"Game creation completed. Region selected: {gameState.Region}. {creationSummary}";
            gameState.PhaseChangeSummary = fullSummary;
            
            // Update adventure summary
            gameState.AdventureSummary = $"A new Pokemon adventure begins in the {gameState.Region} region. {creationSummary}";
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"Game Creation Completed: {fullSummary}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            // Log the completion
            await _informationManagementService.LogNarrativeEventAsync(
                "game_creation_completed",
                "Game creation phase completed successfully",
                fullSummary,
                new List<string>(),
                "",
                null,
                gameState.GameTurnNumber
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Game creation completed successfully",
                selectedRegion = gameState.Region,
                nextPhase = "WorldGeneration",
                summary = fullSummary,
                sessionId = gameState.SessionId,
                phaseTransitionCompleted = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameCreationPhasePlugin] Error in FinalizeGameCreation: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}