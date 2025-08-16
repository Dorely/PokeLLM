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

    [KernelFunction("search_character_classes")]
    [Description("Search for available character class data")]
    public async Task<string> SearchCharacterClasses([Description("Class name or type to search for")] string query)
    {
        try
        {
            var results = await _informationManagementService.SearchGameRulesAsync(new List<string> { query, "class", "character class" }, "CharacterClass");
            
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

    [KernelFunction("create_character_class")]
    [Description("Create a new character class with specific abilities and bonuses")]
    public async Task<string> CreateCharacterClass([Description("Complete character class data as JSON")] string classData)
    {
        try
        {
            // Parse the class data as generic JSON
            var classJson = JsonDocument.Parse(classData);
            
            // Store the class definition in RulesetGameData for future use
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            if (!gameState.RulesetGameData.ContainsKey("customClasses"))
            {
                gameState.RulesetGameData["customClasses"] = JsonSerializer.SerializeToElement(new List<object>());
            }
            
            var existingClassesElement = gameState.RulesetGameData["customClasses"];
            var existingClasses = JsonSerializer.Deserialize<List<object>>(existingClassesElement.GetRawText()) ?? new List<object>();
            existingClasses.Add(classJson.RootElement);
            gameState.RulesetGameData["customClasses"] = JsonSerializer.SerializeToElement(existingClasses);
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Character class created successfully",
                classData = classJson.RootElement
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

    [KernelFunction("set_player_character_class")]
    [Description("Set the player's character class with full class data integration")]
    public async Task<string> SetPlayerCharacterClass([Description("Character class ID")] string classId)
    {
        try
        {
            // Get class data from vector database
            var classData = await _informationManagementService.SearchGameRulesAsync(new List<string> { classId }, "CharacterClass");
            var classInfo = classData.FirstOrDefault();
            
            if (classInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Character class {classId} not found" }, _jsonOptions);
            }
            
            // Store class data in RulesetGameData
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            gameState.RulesetGameData["characterClass"] = JsonSerializer.SerializeToElement(classId);
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                classId = classId,
                classDescription = "Class data stored in ruleset game data"
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

    [KernelFunction("set_character_class")]
    [Description("Set the player's character class/profession")]
    public async Task<string> SetCharacterClass(
        [Description("The character class ID to set")] string classId)
    {
        try
        {
            // Store character class in RulesetGameData
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Initialize RulesetGameData if needed
            if (!gameState.RulesetGameData.ContainsKey("characterClass"))
            {
                gameState.RulesetGameData["characterClass"] = JsonSerializer.SerializeToElement(string.Empty);
            }
            
            // Store the class ID in ruleset game data
            gameState.RulesetGameData["characterClass"] = JsonSerializer.SerializeToElement(classId);
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Character class set to {classId}",
                classId = classId
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
    [Description("Set the player's character name")]
    public async Task<string> SetPlayerName([Description("The chosen character name")] string name)
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
    [Description("Set the player's ability scores based on the current ruleset")]
    public async Task<string> SetPlayerStats(
        [Description("Dictionary of stat names to values based on current ruleset")] Dictionary<string, int> stats)
    {
        try
        {
            if (stats == null || stats.Count == 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Stats dictionary cannot be null or empty" 
                }, _jsonOptions);
            }
            
            // Store stats in RulesetGameData - let the ruleset define what stats exist
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            gameState.RulesetGameData["stats"] = JsonSerializer.SerializeToElement(stats);
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Player stats set successfully",
                stats = stats
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

    [KernelFunction("generate_random_stats")]
    [Description("Generate random ability scores using dice rolling method (configurable by ruleset)")]
    public async Task<string> GenerateRandomStats([Description("Number of stats to generate (based on ruleset)")] int statCount = 6)
    {
        try
        {
            // Generate stats using 4d6 drop lowest method (can be customized by ruleset)
            var stats = new List<int>();
            var random = new Random();
            
            for (int i = 0; i < statCount; i++)
            {
                var rolls = new List<int>();
                for (int j = 0; j < 4; j++)
                {
                    rolls.Add(random.Next(1, 7));
                }
                rolls.Sort();
                rolls.RemoveAt(0); // Remove lowest
                stats.Add(rolls.Sum());
            }
            
            var result = new
            {
                success = true,
                stats = stats.ToArray(),
                total = stats.Sum(),
                description = $"Random stats generated using 4d6 drop lowest method ({statCount} stats)"
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
    [Description("Generate standard ability score array - values configurable by ruleset")]
    public async Task<string> GenerateStandardStats([Description("Standard stat values to use")] int[] standardValues = null)
    {
        try
        {
            // Use default standard array if none provided, but allow ruleset to override
            var stats = standardValues ?? new int[] { 15, 14, 13, 12, 10, 8 };
            
            var result = new
            {
                success = true,
                stats,
                total = stats.Sum(),
                description = $"Standard array: {string.Join(", ", stats)} (assign to desired abilities as defined by ruleset)"
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
    [Description("Complete game setup - and signal for transition to World Generation phase")]
    public async Task<string> FinalizeGameSetup([Description("Summary of setup choices made")] string setupSummary)
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var player = gameState.Player;
            
            // Validate setup is complete - check basic requirements
            var hasCharacterClass = gameState.RulesetGameData.ContainsKey("characterClass") && 
                                   gameState.RulesetGameData["characterClass"].ValueKind != JsonValueKind.Null;
            
            if (string.IsNullOrEmpty(gameState.Region) || 
                string.IsNullOrEmpty(player.Name) || 
                !hasCharacterClass)
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = "Setup incomplete - region, name, and character class required"
                }, _jsonOptions);
            }
            
            // Get character class from ruleset data
            var characterClass = "unknown";
            if (hasCharacterClass)
            {
                characterClass = gameState.RulesetGameData["characterClass"].GetString() ?? "unknown";
            }
            
            // Update adventure summary and transition to WorldGeneration phase
            gameState.AdventureSummary = $"A new adventure begins with {player.Name}, a {characterClass} in the {gameState.Region} region. {setupSummary}";
            gameState.CurrentPhase = GamePhase.WorldGeneration;
            gameState.PhaseChangeSummary = $"Game setup completed successfully. {setupSummary}";
            await _gameStateRepo.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Game setup completed successfully",
                playerName = player.Name,
                characterClass = characterClass,
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