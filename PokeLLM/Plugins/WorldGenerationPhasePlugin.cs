using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic;
using PokeLLM.Game.Plugins.Models;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.VectorStore.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for procedural world generation utilities and phase management
/// </summary>
public class WorldGenerationPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly IRulesetManager _rulesetManager;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly JsonSerializerOptions _jsonOptions;

    public WorldGenerationPhasePlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService,
        IGameLogicService gameLogicService,
        IRulesetManager rulesetManager,
        IVectorStoreService vectorStoreService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _gameLogicService = gameLogicService;
        _rulesetManager = rulesetManager;
        _vectorStoreService = vectorStoreService;
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

    #region World Entity Creation Functions

    [KernelFunction("create_quest")]
    [Description("Create a new quest with structured data in gamestate and descriptive text in Vector DB")]
    public async Task<string> CreateQuest(
        [Description("Unique quest ID")] string questId,
        [Description("Quest title")] string title,
        [Description("Quest description for Vector DB")] string description,
        [Description("Quest giver (NPC ID or name)")] string questGiver,
        [Description("Quest objectives (array of objective strings)")] string[] objectives,
        [Description("Quest rewards description")] string rewards,
        [Description("Quest difficulty level")] string difficulty = "Normal")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Quest ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Quest title cannot be null or empty" 
                }, _jsonOptions);
            }

            if (objectives == null || objectives.Length == 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Quest must have at least one objective" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Create quest object for gamestate
            var quest = new
            {
                id = questId,
                title = title,
                questGiver = questGiver ?? "Unknown",
                objectives = objectives.ToList(),
                rewards = rewards ?? "Unknown rewards",
                difficulty = difficulty,
                status = "Available",
                progress = new Dictionary<string, object>(),
                createdAt = DateTime.UtcNow.ToString("O")
            };

            // Store in WorldEntities
            gameState.WorldEntities[questId] = quest;
            await _gameStateRepo.SaveStateAsync(gameState);

            // Create Vector DB entry for quest description
            Guid? loreEntryId = null;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var loreEntry = new LoreVectorRecord
                {
                    EntryId = $"quest_{questId}",
                    Title = $"Quest: {title}",
                    Content = description,
                    EntryType = "Quest",
                    Tags = new[] { "quest", "adventure", difficulty.ToLower(), questGiver?.ToLower() ?? "unknown" }
                };

                loreEntryId = await _vectorStoreService.AddOrUpdateLoreAsync(loreEntry);
            }

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Quest '{title}' created successfully",
                questId = questId,
                quest = new
                {
                    id = questId,
                    title = title,
                    questGiver = questGiver,
                    objectives = objectives,
                    rewards = rewards,
                    difficulty = difficulty,
                    status = "Available"
                },
                loreEntryId = loreEntryId?.ToString()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error creating quest: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("create_faction")]
    [Description("Create a new faction with structured data in gamestate and ideology text in Vector DB")]
    public async Task<string> CreateFaction(
        [Description("Unique faction ID")] string factionId,
        [Description("Faction name")] string name,
        [Description("Faction ideology and goals for Vector DB")] string ideology,
        [Description("Faction leader (NPC ID or name)")] string leader,
        [Description("Faction type (Guild, Government, Religious, etc.)")] string factionType,
        [Description("Faction influence level (Local, Regional, National, Global)")] string influence = "Local",
        [Description("Starting reputation with player (-100 to 100)")] int reputation = 0)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Faction ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Faction name cannot be null or empty" 
                }, _jsonOptions);
            }

            if (reputation < -100 || reputation > 100)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Reputation must be between -100 and 100" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Create faction object for gamestate
            var faction = new
            {
                id = factionId,
                name = name,
                leader = leader ?? "Unknown",
                factionType = factionType ?? "Organization",
                influence = influence,
                reputation = reputation,
                members = new List<string>(),
                territories = new List<string>(),
                relationships = new Dictionary<string, int>(),
                createdAt = DateTime.UtcNow.ToString("O")
            };

            // Store in WorldEntities
            gameState.WorldEntities[factionId] = faction;
            await _gameStateRepo.SaveStateAsync(gameState);

            // Create Vector DB entry for faction ideology
            Guid? loreEntryId = null;
            if (!string.IsNullOrWhiteSpace(ideology))
            {
                var loreEntry = new LoreVectorRecord
                {
                    EntryId = $"faction_{factionId}",
                    Title = $"Faction: {name}",
                    Content = ideology,
                    EntryType = "Faction",
                    Tags = new[] { "faction", factionType?.ToLower() ?? "organization", influence.ToLower(), leader?.ToLower() ?? "unknown" }
                };

                loreEntryId = await _vectorStoreService.AddOrUpdateLoreAsync(loreEntry);
            }

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Faction '{name}' created successfully",
                factionId = factionId,
                faction = new
                {
                    id = factionId,
                    name = name,
                    leader = leader,
                    factionType = factionType,
                    influence = influence,
                    reputation = reputation
                },
                loreEntryId = loreEntryId?.ToString()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error creating faction: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("create_location")]
    [Description("Create a new location with structured data in gamestate and description in Vector DB")]
    public async Task<string> CreateLocation(
        [Description("Unique location ID")] string locationId,
        [Description("Location name")] string name,
        [Description("Location description for Vector DB")] string description,
        [Description("Location type (City, Dungeon, Forest, etc.)")] string locationType,
        [Description("Location size (Small, Medium, Large, Vast)")] string size = "Medium",
        [Description("Notable features (array of feature strings)")] string[] features = null,
        [Description("Environmental conditions")] string environment = "Normal")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location name cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Create location object for gamestate
            var location = new
            {
                id = locationId,
                name = name,
                locationType = locationType ?? "Unknown",
                size = size,
                features = features?.ToList() ?? new List<string>(),
                environment = environment,
                exits = new Dictionary<string, string>(), // direction -> connected location ID
                npcs = new List<string>(),
                items = new List<string>(),
                events = new List<string>(),
                visited = false,
                createdAt = DateTime.UtcNow.ToString("O")
            };

            // Store in WorldLocations
            gameState.WorldLocations[locationId] = location;
            await _gameStateRepo.SaveStateAsync(gameState);

            // Create Vector DB entry for location description
            Guid? loreEntryId = null;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var loreEntry = new LoreVectorRecord
                {
                    EntryId = $"location_{locationId}",
                    Title = $"Location: {name}",
                    Content = description,
                    EntryType = "Location",
                    Tags = new[] { "location", locationType?.ToLower() ?? "unknown", size.ToLower(), environment.ToLower() }
                };

                loreEntryId = await _vectorStoreService.AddOrUpdateLoreAsync(loreEntry);
            }

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Location '{name}' created successfully",
                locationId = locationId,
                location = new
                {
                    id = locationId,
                    name = name,
                    locationType = locationType,
                    size = size,
                    features = features ?? new string[0],
                    environment = environment
                },
                loreEntryId = loreEntryId?.ToString()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error creating location: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("link_locations")]
    [Description("Create bidirectional connections between two locations with automatic reverse linking")]
    public async Task<string> LinkLocations(
        [Description("Source location ID")] string fromLocationId,
        [Description("Target location ID")] string toLocationId,
        [Description("Direction from source to target (north, south, east, west, up, down, etc.)")] string direction,
        [Description("Optional reverse direction (auto-calculated if not provided)")] string reverseDirection = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fromLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Source location ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(toLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Target location ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(direction))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Direction cannot be null or empty" 
                }, _jsonOptions);
            }

            if (fromLocationId == toLocationId)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Cannot link location to itself" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();

            // Check if both locations exist
            if (!gameState.WorldLocations.ContainsKey(fromLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Source location '{fromLocationId}' not found" 
                }, _jsonOptions);
            }

            if (!gameState.WorldLocations.ContainsKey(toLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Target location '{toLocationId}' not found" 
                }, _jsonOptions);
            }

            // Auto-calculate reverse direction if not provided
            if (string.IsNullOrWhiteSpace(reverseDirection))
            {
                var directionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "north", "south" }, { "south", "north" },
                    { "east", "west" }, { "west", "east" },
                    { "up", "down" }, { "down", "up" },
                    { "northeast", "southwest" }, { "southwest", "northeast" },
                    { "northwest", "southeast" }, { "southeast", "northwest" },
                    { "in", "out" }, { "out", "in" }
                };

                reverseDirection = directionMap.ContainsKey(direction) 
                    ? directionMap[direction] 
                    : $"to_{fromLocationId}";
            }

            // Get location objects (they're stored as generic objects in JSON)
            var fromLocationJson = JsonSerializer.Serialize(gameState.WorldLocations[fromLocationId]);
            var toLocationJson = JsonSerializer.Serialize(gameState.WorldLocations[toLocationId]);
            
            var fromLocation = JsonSerializer.Deserialize<Dictionary<string, object>>(fromLocationJson);
            var toLocation = JsonSerializer.Deserialize<Dictionary<string, object>>(toLocationJson);

            // Ensure exits dictionary exists
            if (!fromLocation.ContainsKey("exits"))
                fromLocation["exits"] = new Dictionary<string, object>();
            if (!toLocation.ContainsKey("exits"))
                toLocation["exits"] = new Dictionary<string, object>();

            var fromExits = fromLocation["exits"] as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, string>());
            var toExits = toLocation["exits"] as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, string>());

            var fromExitsDict = fromExits.ValueKind == JsonValueKind.Object 
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(fromExits) ?? new Dictionary<string, string>()
                : new Dictionary<string, string>();
            
            var toExitsDict = toExits.ValueKind == JsonValueKind.Object 
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(toExits) ?? new Dictionary<string, string>()
                : new Dictionary<string, string>();

            // Add the links
            fromExitsDict[direction.ToLower()] = toLocationId;
            toExitsDict[reverseDirection.ToLower()] = fromLocationId;

            // Update the location objects
            fromLocation["exits"] = fromExitsDict;
            toLocation["exits"] = toExitsDict;

            // Save back to gamestate
            gameState.WorldLocations[fromLocationId] = fromLocation;
            gameState.WorldLocations[toLocationId] = toLocation;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Locations linked successfully",
                link = new
                {
                    from = fromLocationId,
                    to = toLocationId,
                    direction = direction.ToLower(),
                    reverseDirection = reverseDirection.ToLower()
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error linking locations: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("place_npc_in_location")]
    [Description("Associate an NPC with a specific location, updating both NPC and location data")]
    public async Task<string> PlaceNpcInLocation(
        [Description("NPC ID")] string npcId,
        [Description("Location ID where NPC should be placed")] string locationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(locationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Location ID cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();

            // Check if NPC exists
            if (!gameState.WorldEntities.ContainsKey(npcId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"NPC '{npcId}' not found" 
                }, _jsonOptions);
            }

            // Check if location exists
            if (!gameState.WorldLocations.ContainsKey(locationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Location '{locationId}' not found" 
                }, _jsonOptions);
            }

            // Update NPC location
            var npcJson = JsonSerializer.Serialize(gameState.WorldEntities[npcId]);
            var npc = JsonSerializer.Deserialize<Dictionary<string, object>>(npcJson);
            
            // Remove NPC from previous location if it exists
            var previousLocation = npc.ContainsKey("currentLocation") ? npc["currentLocation"]?.ToString() : null;
            if (!string.IsNullOrEmpty(previousLocation) && gameState.WorldLocations.ContainsKey(previousLocation))
            {
                var prevLocationJson = JsonSerializer.Serialize(gameState.WorldLocations[previousLocation]);
                var prevLocation = JsonSerializer.Deserialize<Dictionary<string, object>>(prevLocationJson);
                
                if (prevLocation.ContainsKey("npcs"))
                {
                    var npcsElement = prevLocation["npcs"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<string>());
                    var npcsList = npcsElement.ValueKind == JsonValueKind.Array 
                        ? JsonSerializer.Deserialize<List<string>>(npcsElement) ?? new List<string>()
                        : new List<string>();
                    
                    npcsList.Remove(npcId);
                    prevLocation["npcs"] = npcsList;
                    gameState.WorldLocations[previousLocation] = prevLocation;
                }
            }

            // Set NPC's current location
            npc["currentLocation"] = locationId;
            gameState.WorldEntities[npcId] = npc;

            // Add NPC to new location
            var locationJson = JsonSerializer.Serialize(gameState.WorldLocations[locationId]);
            var location = JsonSerializer.Deserialize<Dictionary<string, object>>(locationJson);
            
            if (!location.ContainsKey("npcs"))
                location["npcs"] = new List<string>();
            
            var locationNpcsElement = location["npcs"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<string>());
            var locationNpcs = locationNpcsElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<string>>(locationNpcsElement) ?? new List<string>()
                : new List<string>();
            
            if (!locationNpcs.Contains(npcId))
            {
                locationNpcs.Add(npcId);
            }
            
            location["npcs"] = locationNpcs;
            gameState.WorldLocations[locationId] = location;

            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"NPC '{npcId}' placed in location '{locationId}'",
                placement = new
                {
                    npcId = npcId,
                    locationId = locationId,
                    previousLocation = previousLocation
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error placing NPC in location: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("create_npc")]
    [Description("Create a new NPC with structured data in gamestate and backstory in Vector DB (uses ruleset for class validation)")]
    public async Task<string> CreateNpc(
        [Description("Unique NPC ID")] string npcId,
        [Description("NPC name")] string name,
        [Description("NPC backstory for Vector DB")] string backstory,
        [Description("NPC class from active ruleset")] string className,
        [Description("NPC level")] int level = 1,
        [Description("NPC role/profession")] string role = "Civilian",
        [Description("NPC attitude towards player (Friendly, Neutral, Hostile)")] string attitude = "Neutral",
        [Description("NPC stats (stat name -> value)")] Dictionary<string, int> stats = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC name cannot be null or empty" 
                }, _jsonOptions);
            }

            if (level < 1)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "NPC level must be at least 1" 
                }, _jsonOptions);
            }

            // Validate class exists in ruleset if provided
            if (!string.IsNullOrWhiteSpace(className))
            {
                var activeRuleset = _rulesetManager.GetActiveRuleset();
                if (activeRuleset == null)
                {
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = "No active ruleset found. Please load a ruleset first." 
                    }, _jsonOptions);
                }

                bool classExists = false;
                if (activeRuleset.RootElement.TryGetProperty("trainerClasses", out var trainerClasses))
                {
                    foreach (var trainerClass in trainerClasses.EnumerateArray())
                    {
                        if (trainerClass.TryGetProperty("id", out var classId) && 
                            classId.GetString()?.Equals(className, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            classExists = true;
                            break;
                        }
                    }
                }

                if (!classExists)
                {
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"NPC class '{className}' not found in active ruleset" 
                    }, _jsonOptions);
                }
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Create NPC object for gamestate
            var npc = new
            {
                id = npcId,
                name = name,
                className = className ?? "Civilian",
                level = level,
                role = role,
                attitude = attitude,
                stats = stats ?? new Dictionary<string, int>(),
                currentLocation = (string)null, // Will be set when placed
                inventory = new List<string>(),
                dialogue = new Dictionary<string, object>(),
                questsOffered = new List<string>(),
                factionAffiliation = (string)null,
                isAlive = true,
                createdAt = DateTime.UtcNow.ToString("O")
            };

            // Store in WorldEntities
            gameState.WorldEntities[npcId] = npc;
            await _gameStateRepo.SaveStateAsync(gameState);

            // Create Vector DB entry for NPC backstory
            Guid? loreEntryId = null;
            if (!string.IsNullOrWhiteSpace(backstory))
            {
                var loreEntry = new LoreVectorRecord
                {
                    EntryId = $"npc_{npcId}",
                    Title = $"NPC: {name}",
                    Content = backstory,
                    EntryType = "NPC",
                    Tags = new[] { "npc", "character", role.ToLower(), className?.ToLower() ?? "civilian", attitude.ToLower() }
                };

                loreEntryId = await _vectorStoreService.AddOrUpdateLoreAsync(loreEntry);
            }

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"NPC '{name}' created successfully",
                npcId = npcId,
                npc = new
                {
                    id = npcId,
                    name = name,
                    className = className,
                    level = level,
                    role = role,
                    attitude = attitude,
                    stats = stats ?? new Dictionary<string, int>()
                },
                loreEntryId = loreEntryId?.ToString(),
                note = "Use place_npc_in_location to position the NPC in the world"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error creating NPC: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    #endregion

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