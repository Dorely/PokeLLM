using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.Game.Plugins.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Combined game and character setup phase - handles region selection and mechanical character creation
/// </summary>
public class GameSetupPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IWorldManagementService _worldManagementService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameSetupPhasePlugin(
        IGameStateRepository gameStateRepo,
        IWorldManagementService worldManagementService,
        IInformationManagementService informationManagementService,
        ICharacterManagementService characterManagementService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _worldManagementService = worldManagementService;
        _informationManagementService = informationManagementService;
        _characterManagementService = characterManagementService;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    // === REGION SETUP FUNCTIONS ===

    [KernelFunction("search_existing_region_knowledge")]
    [Description("Search for existing region information in the vector database")]
    public async Task<string> SearchExistingRegionKnowledge(
        [Description("Search Query for stored region information")] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Search query cannot be empty"
                }, _jsonOptions);
            }

            var loreResults = await _informationManagementService.SearchLoreAsync(
                new List<string> { query, $"{query} region", $"{query} area" },
                "Region"
            );

            return JsonSerializer.Serialize(new {
                success = true,
                query = query,
                resultsCount = loreResults.Count(),
                results = loreResults.Select(r => new
                {
                    entryId = r.EntryId,
                    entryType = r.EntryType,
                    title = r.Title,
                    content = r.Content,
                    tags = r.Tags
                }).ToList()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("set_region")]
    [Description("Sets the Region chosen by the player")]
    public async Task<string> SetRegion(
        [Description("Region name to set")] string regionName,
        [Description("Full Details to be stored in the vector database")] LoreVectorRecordDto regionRecord)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(regionName))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Region name cannot be empty"
                }, _jsonOptions);
            }

            if (regionRecord == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Region record cannot be null"
                }, _jsonOptions);
            }

            // Set the region in the game state
            var setRegionResult = await _worldManagementService.SetRegionAsync(regionName);
            
            // Store the region details in the vector database
            var upsertResult = await _informationManagementService.UpsertLoreAsync(
                regionRecord.EntryId, 
                regionRecord.EntryType, 
                regionRecord.Title, 
                regionRecord.Content, 
                regionRecord.Tags?.ToList(),
                null
            );

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
            
            return JsonSerializer.Serialize(new { 
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
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    // === TRAINER CLASS FUNCTIONS ===

    [KernelFunction("search_trainer_classes")]
    [Description("Search for available trainer class data")]
    public async Task<string> SearchTrainerClasses([Description("Class name or type to search for")] string query)
    {
        try
        {
            var results = await _informationManagementService.SearchGameRulesAsync(new List<string> { query, "class", "trainer class" }, "TrainerClass");
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                query = query,
                results = results.Select(r => new
                {
                    classId = r.EntryId,
                    name = r.Title,
                    description = r.Content,
                    tags = r.Tags
                }).ToList()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("create_trainer_class")]
    [Description("Create a new trainer class and store it")]
    public async Task<string> CreateTrainerClass([Description("Complete trainer class data")] TrainerClass classData)
    {
        try
        {
            // Store in vector database using existing TrainerClass structure
            var vectorResult = await _informationManagementService.UpsertGameRuleAsync(
                classData.Id,
                "TrainerClass", 
                classData.Name,
                JsonSerializer.Serialize(classData, _jsonOptions),
                classData.Tags?.ToList()
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                classId = classData.Id,
                name = classData.Name,
                result = vectorResult
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("set_player_trainer_class")]
    [Description("Set the player's trainer class with full class data integration")]
    public async Task<string> SetPlayerTrainerClass([Description("Trainer class ID")] string classId)
    {
        try
        {
            // Get class data from vector database
            var classData = await _informationManagementService.SearchGameRulesAsync(new List<string> { classId }, "TrainerClass");
            var classInfo = classData.FirstOrDefault();
            
            if (classInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Trainer class {classId} not found" }, _jsonOptions);
            }
            
            // Parse class data using existing TrainerClass structure
            var trainerClass = JsonSerializer.Deserialize<TrainerClass>(classInfo.Content);
            
            // Update player with full class integration
            await _characterManagementService.SetPlayerClass(classId);
            
            // Store full TrainerClass data in player
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            gameState.Player.TrainerClassData = trainerClass;
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                classId = classId,
                className = trainerClass.Name,
                statModifiers = trainerClass.StatModifiers,
                startingAbilities = trainerClass.StartingAbilities,
                startingMoney = trainerClass.StartingMoney,
                startingItems = trainerClass.StartingItems
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    // === CHARACTER CREATION FUNCTIONS ===

    [KernelFunction("set_player_name")]
    [Description("Set the player's trainer name")]
    public async Task<string> SetPlayerName([Description("The chosen trainer name")] string name)
    {
        try
        {
            await _characterManagementService.SetPlayerName(name);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Player name set to: {name}",
                playerName = name
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error setting player name: {ex.Message}"
            }, _jsonOptions);
        }
    }

    [KernelFunction("set_player_stats")]
    [Description("Set the player's ability scores")]
    public async Task<string> SetPlayerStats(
        [Description("Array of 6 ability scores: [Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma]")] int[] stats)
    {
        try
        {
            if (stats.Length != 6)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Must provide exactly 6 ability scores"
                }, _jsonOptions);
            }
            
            foreach (var stat in stats)
            {
                if (stat < 3 || stat > 20)
                {
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"Ability scores must be between 3 and 20. Invalid value: {stat}"
                    }, _jsonOptions);
                }
            }
            
            await _characterManagementService.SetPlayerStats(stats);
            
            var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            var statDict = statNames.Zip(stats, (name, value) => new { name, value }).ToDictionary(x => x.name, x => x.value);
            
            return JsonSerializer.Serialize(new { 
                success = true,
                message = "Player stats set successfully",
                stats = statDict
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error setting player stats: {ex.Message}"
            }, _jsonOptions);
        }
    }

    [KernelFunction("generate_random_stats")]
    [Description("Generate random ability scores using 4d6 drop lowest method")]
    public async Task<string> GenerateRandomStats()
    {
        try
        {
            var stats = await _characterManagementService.GenerateRandomStats();
            
            var result = new
            {
                success = true,
                stats,
                total = stats.Sum(),
                average = stats.Average(),
                description = "Generated using 4d6 drop lowest method"
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("generate_standard_stats")]
    [Description("Generate standard ability score array (15, 14, 13, 12, 10, 8) for balanced characters")]
    public async Task<string> GenerateStandardStats()
    {
        try
        {
            var stats = await _characterManagementService.GenerateStandardStats();
            
            var result = new
            {
                success = true,
                stats,
                total = stats.Sum(),
                description = "Standard array: 15, 14, 13, 12, 10, 8 (assign to desired abilities)"
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }

    [KernelFunction("finalize_game_setup")]
    [Description("Complete game setup - does NOT transition phases (handled by service)")]
    public async Task<string> FinalizeGameSetup([Description("Summary of setup choices made")] string setupSummary)
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var player = gameState.Player;
            
            // Validate setup is complete
            if (string.IsNullOrEmpty(gameState.Region) || 
                string.IsNullOrEmpty(player.Name) || 
                string.IsNullOrEmpty(player.CharacterDetails.Class))
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = "Setup incomplete - region, name, and class required"
                }, _jsonOptions);
            }
            
            // Update adventure summary (but DO NOT change phase)
            gameState.AdventureSummary = $"A new Pokemon adventure begins with {player.Name}, a {player.CharacterDetails.Class} trainer in the {gameState.Region} region. {setupSummary}";
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Game setup completed successfully",
                playerName = player.Name,
                trainerClass = player.CharacterDetails.Class,
                region = gameState.Region,
                setupComplete = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = ex.Message 
            }, _jsonOptions);
        }
    }
}