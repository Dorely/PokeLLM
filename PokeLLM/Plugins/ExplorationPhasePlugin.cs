using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.VectorStore.Models;

namespace PokeLLM.Game.Plugins;

public class ExplorationPhasePlugin
{
    private readonly IGameLogicService _gameLogicService;
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly IEntityService _entityService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExplorationPhasePlugin(
        IGameLogicService gameLogicService,
        IGameStateRepository gameStateRepo,
        IWorldManagementService worldManagementService,
        INpcManagementService npcManagementService,
        IEntityService entityService,
        IVectorStoreService vectorStoreService)
    {
        _gameLogicService = gameLogicService;
        _gameStateRepo = gameStateRepo;
        _worldManagementService = worldManagementService;
        _npcManagementService = npcManagementService;
        _entityService = entityService;
        _vectorStoreService = vectorStoreService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("manage_dice_and_checks")]
    [Description("Handle all dice rolling and skill checks with comprehensive options")]
    public async Task<string> ManageDiceAndChecks(
        [Description("Type of check: 'd20', 'advantage', 'disadvantage', 'skill_check', 'dice_roll', 'random_choice'")] string checkType,
        [Description("For skill checks: stat name (Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma)")] string statName = "",
        [Description("For skill checks: difficulty class (5=Very Easy, 8=Easy, 11=Medium, 14=Hard, 17=Very Hard, 20=Nearly Impossible)")] int difficultyClass = 10,
        [Description("For dice rolls: number of sides on each die")] int sides = 20,
        [Description("Number of dice to roll")] int count = 1,
        [Description("Additional modifier to add to rolls")] int modifier = 0,
        [Description("For random choices: list of options to choose from")] List<string> options = null)
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManageDiceAndChecks called: {checkType}");
        
        try
        {
            switch (checkType.ToLower())
            {
                case "d20":
                    var d20Result = await _gameLogicService.RollD20Async();
                    return d20Result.Message;
                    
                case "advantage":
                    var advResult = await _gameLogicService.RollD20WithAdvantageAsync();
                    return advResult.Message;
                    
                case "disadvantage":
                    var disResult = await _gameLogicService.RollD20WithDisadvantageAsync();
                    return disResult.Message;
                    
                case "skill_check":
                    var skillResult = await _gameLogicService.MakeSkillCheckAsync(statName, difficultyClass, false, false, modifier);
                    if (!string.IsNullOrEmpty(skillResult.Error))
                    {
                        return JsonSerializer.Serialize(new { error = skillResult.Error }, _jsonOptions);
                    }
                    return $"{skillResult.Outcome} {skillResult.StatName} check: {skillResult.TotalRoll} vs DC {skillResult.DifficultyClass} ({skillResult.Margin:+#;-#;0})";
                    
                case "dice_roll":
                    var diceResult = await _gameLogicService.RollDiceAsync(sides, count);
                    if (!diceResult.Success)
                    {
                        return JsonSerializer.Serialize(new { error = diceResult.Error }, _jsonOptions);
                    }
                    return JsonSerializer.Serialize(diceResult, _jsonOptions);
                    
                case "random_choice":
                    if (options == null || options.Count == 0)
                    {
                        return JsonSerializer.Serialize(new { error = "No options provided for random choice" }, _jsonOptions);
                    }
                    var choiceResult = await _gameLogicService.MakeRandomDecisionFromOptionsAsync(options);
                    if (!choiceResult.Success)
                    {
                        return JsonSerializer.Serialize(new { error = choiceResult.Error }, _jsonOptions);
                    }
                    return JsonSerializer.Serialize(choiceResult, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown check type: {checkType}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManageDiceAndChecks: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("manage_world_movement")]
    [Description("Handle player movement, location transitions, and world navigation")]
    public async Task<string> ManageWorldMovement(
        [Description("Action: 'move_to_location', 'get_current_location'")] string action,
        [Description("Destination location ID (for movement actions)")] string locationId = "")
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManageWorldMovement called: {action}");
        
        try
        {
            switch (action.ToLower())
            {
                case "move_to_location":
                    await _worldManagementService.MovePlayerToLocationAsync(locationId);
                    var newLocation = await _worldManagementService.GetLocationDetailsAsync(locationId);
                    var locationName = newLocation?.ContainsKey("name") == true ? newLocation["name"]?.ToString() : locationId;
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true, 
                        message = $"Moved to {locationName}",
                        location = newLocation,
                        action = action
                    }, _jsonOptions);
                    
                case "get_current_location":
                    var currentLocation = await _worldManagementService.GetPlayerCurrentLocationAsync();
                    return JsonSerializer.Serialize(new 
                    { 
                        currentLocation = currentLocation,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown movement action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManageWorldMovement: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("manage_npc_interactions")]
    [Description("Handle NPC interactions, creation, and relationship management")]
    public async Task<string> ManageNpcInteractions(
        [Description("Action: 'get_npcs_at_location', 'create_npc', 'move_npc', 'update_relationship', 'get_npc_details'")] string action,
        [Description("NPC ID for specific operations")] string npcId = "",
        [Description("Location ID for NPC operations")] string locationId = "",
        [Description("NPC name (for creation)")] string name = "",
        [Description("NPC class (for creation)")] string characterClass = "Citizen",
        [Description("Relationship change amount")] int relationshipDelta = 0)
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManageNpcInteractions called: {action}");
        
        try
        {
            switch (action.ToLower())
            {
                case "get_npcs_at_location":
                    var npcsAtLocation = await _npcManagementService.GetNpcsAtLocation(locationId);
                    return JsonSerializer.Serialize(new 
                    { 
                        npcs = npcsAtLocation,
                        locationId = locationId,
                        count = npcsAtLocation.Count,
                        action = action
                    }, _jsonOptions);
                    
                case "create_npc":
                    var newNpc = await _npcManagementService.CreateNpc(name, characterClass, locationId);
                    var createdNpcId = newNpc?.ContainsKey("id") == true ? newNpc["id"]?.ToString() : name;
                    await _worldManagementService.AddNpcToLocationAsync(locationId, createdNpcId);
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Created NPC {name} at {locationId}",
                        npc = newNpc,
                        action = action
                    }, _jsonOptions);
                    
                case "move_npc":
                    await _npcManagementService.MoveNpcToLocationAsync(npcId, locationId);
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Moved NPC {npcId} to {locationId}",
                        npcId = npcId,
                        locationId = locationId,
                        action = action
                    }, _jsonOptions);
                    
                case "update_relationship":
                    await _npcManagementService.UpdateNpcRelationshipWithPlayerAsync(npcId, relationshipDelta);
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Updated relationship with {npcId} by {relationshipDelta:+#;-#;0}",
                        npcId = npcId,
                        relationshipDelta = relationshipDelta,
                        action = action
                    }, _jsonOptions);
                    
                case "get_npc_details":
                    var npcDetails = await _npcManagementService.GetNpcDetails(npcId);
                    return JsonSerializer.Serialize(new 
                    { 
                        npc = npcDetails,
                        npcId = npcId,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown NPC action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManageNpcInteractions: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }


    [KernelFunction("manage_environment_and_time")]
    [Description("Handle environmental changes, time progression, and world state")]
    public async Task<string> ManageEnvironmentAndTime(
        [Description("Action: 'advance_time', 'set_time', 'change_weather', 'set_weather', 'get_environment'")] string action,
        [Description("Time of day to set (Dawn, Morning, Day, Afternoon, Dusk, Night)")] string timeOfDay = "",
        [Description("Weather to set (Clear, Cloudy, Rain, Storm, Thunderstorm, Snow, Fog, Sandstorm, Sunny, Overcast)")] string weather = "")
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManageEnvironmentAndTime called: {action}");
        
        try
        {
            switch (action.ToLower())
            {
                case "advance_time":
                    await _worldManagementService.AdvanceTimeOfDayAsync();
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = "Time advanced to next period",
                        action = action
                    }, _jsonOptions);
                    
                case "set_time":
                    if (Enum.TryParse<TimeOfDay>(timeOfDay, true, out var parsedTime))
                    {
                        await _worldManagementService.SetTimeOfDayAsync(timeOfDay);
                        return JsonSerializer.Serialize(new 
                        { 
                            success = true,
                            message = $"Time set to {timeOfDay}",
                            timeOfDay = timeOfDay,
                            action = action
                        }, _jsonOptions);
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = $"Invalid time of day: {timeOfDay}" }, _jsonOptions);
                    }
                    
                case "change_weather":
                case "set_weather":
                    if (Enum.TryParse<Weather>(weather, true, out var newWeather))
                    {
                        await _worldManagementService.SetWeatherAsync(weather);
                        return JsonSerializer.Serialize(new 
                        { 
                            success = true,
                            message = $"Weather set to {weather}",
                            weather = weather,
                            action = action
                        }, _jsonOptions);
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = $"Invalid weather: {weather}" }, _jsonOptions);
                    }
                    
                case "get_environment":
                    var gameState = await _gameStateRepo.LoadLatestStateAsync();
                    return JsonSerializer.Serialize(new 
                    { 
                        timeOfDay = gameState.TimeOfDay.ToString(),
                        weather = gameState.Weather.ToString(),
                        region = gameState.Region,
                        currentLocation = gameState.CurrentLocationId,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown environment action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManageEnvironmentAndTime: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }


    [KernelFunction("manage_phase_transitions")]
    [Description("Handle transitions to other game phases based on exploration events")]
    public async Task<string> ManagePhaseTransitions(
        [Description("Target phase: 'Combat', 'LevelUp'")] string targetPhase,
        [Description("Reason for the phase transition")] string transitionReason,
        [Description("Additional context for the new phase")] string additionalContext = "")
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManagePhaseTransitions called: {targetPhase}");
        
        try
        {
            if (Enum.TryParse<GamePhase>(targetPhase, true, out var newPhase))
            {
                var gameState = await _gameStateRepo.LoadLatestStateAsync();
                
                // Set the new phase
                gameState.CurrentPhase = newPhase;
                
                // Set the phase change summary with context
                var fullContext = string.IsNullOrEmpty(additionalContext) 
                    ? transitionReason 
                    : $"{transitionReason}. {additionalContext}";
                gameState.PhaseChangeSummary = fullContext;
                
                // Add to recent events
                gameState.RecentEvents.Add(new EventLog 
                { 
                    TurnNumber = gameState.GameTurnNumber, 
                    EventDescription = $"Phase Transition: {fullContext}" 
                });
                
                // Update save time
                gameState.LastSaveTime = DateTime.UtcNow;
                
                // Save the state
                await _gameStateRepo.SaveStateAsync(gameState);
                
                return JsonSerializer.Serialize(new 
                { 
                    success = true,
                    message = $"Transitioned to {targetPhase} phase",
                    targetPhase = targetPhase,
                    reason = transitionReason,
                    context = additionalContext,
                    phaseTransitionCompleted = true
                }, _jsonOptions);
            }
            else
            {
                return JsonSerializer.Serialize(new { error = $"Invalid target phase: {targetPhase}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManagePhaseTransitions: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("manage_exploration_events")]
    [Description("Handle exploration-specific events, mysteries, and discoveries")]
    public async Task<string> ManageExplorationEvents(
        [Description("Event type: 'mystery_clue', 'discovery', 'random_event', 'investigation', 'story_hook'")] string eventType,
        [Description("Event description or content")] string eventDescription,
        [Description("Location where event occurs")] string locationId = "",
        [Description("Entities involved in the event")] List<string> involvedEntities = null,
        [Description("Whether this event should be logged for future reference")] bool logEvent = true)
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManageExplorationEvents called: {eventType}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var currentLocationId = string.IsNullOrEmpty(locationId) ? gameState.CurrentLocationId : locationId;
            
            // Log the event if requested - this is now handled by VectorPlugin
            if (logEvent)
            {
                // Note: Event logging is now handled through VectorPlugin's manage_vector_store function
                // The LLM should use VectorPlugin for logging narrative events
            }
            
            // Add to recent events for immediate context
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"{eventType}: {eventDescription}" 
            });
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            var response = new 
            { 
                success = true,
                eventType = eventType,
                description = eventDescription,
                locationId = currentLocationId,
                involvedEntities = involvedEntities ?? new List<string>(),
                logged = logEvent,
                turnNumber = gameState.GameTurnNumber
            };
            
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManageExplorationEvents: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #region Core Required Functions from Todo List

    [KernelFunction("get_location_details")]
    [Description("Retrieve all structured data about a specific location for LLM")]
    public async Task<string> GetLocationDetails(
        [Description("Location ID to retrieve details for")] string locationId)
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

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Check if location exists
            if (!gameState.WorldLocations.ContainsKey(locationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Location '{locationId}' not found" 
                }, _jsonOptions);
            }

            var location = gameState.WorldLocations[locationId];
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                locationId = locationId,
                location = location
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error retrieving location details: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("search_lore")]
    [Description("Perform semantic search on Vector DB for relevant lore/descriptions")]
    public async Task<string> SearchLore(
        [Description("Search query for semantic search")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5,
        [Description("Minimum relevance score (0.0 to 1.0)")] double minRelevanceScore = 0.7)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Search query cannot be null or empty" 
                }, _jsonOptions);
            }

            if (maxResults <= 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Max results must be greater than 0" 
                }, _jsonOptions);
            }

            if (minRelevanceScore < 0.0 || minRelevanceScore > 1.0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Minimum relevance score must be between 0.0 and 1.0" 
                }, _jsonOptions);
            }

            var searchResults = await _vectorStoreService.SearchLoreAsync(query, minRelevanceScore, maxResults);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                query = query,
                maxResults = maxResults,
                minRelevanceScore = minRelevanceScore,
                results = searchResults,
                resultCount = searchResults.Count()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error searching lore: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("update_player_location")]
    [Description("Handle player movement between locations with validation")]
    public async Task<string> UpdatePlayerLocation(
        [Description("Direction to move (north, south, east, west, up, down, etc.)")] string direction,
        [Description("Optional specific target location ID (overrides direction-based movement)")] string targetLocationId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(direction) && string.IsNullOrWhiteSpace(targetLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Either direction or target location ID must be provided" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var currentLocationId = gameState.CurrentLocationId;
            
            if (string.IsNullOrEmpty(currentLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Player current location is not set" 
                }, _jsonOptions);
            }

            // Check if current location exists
            if (!gameState.WorldLocations.ContainsKey(currentLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Current location '{currentLocationId}' not found" 
                }, _jsonOptions);
            }

            string destinationId;
            
            // Determine destination
            if (!string.IsNullOrWhiteSpace(targetLocationId))
            {
                destinationId = targetLocationId;
            }
            else
            {
                // Get exits from current location
                var currentLocationJson = JsonSerializer.Serialize(gameState.WorldLocations[currentLocationId]);
                var currentLocation = JsonSerializer.Deserialize<Dictionary<string, object>>(currentLocationJson);
                
                if (!currentLocation.ContainsKey("exits"))
                {
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"Current location '{currentLocationId}' has no exits defined" 
                    }, _jsonOptions);
                }

                var exitsElement = currentLocation["exits"] as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, string>());
                var exits = exitsElement.ValueKind == JsonValueKind.Object 
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(exitsElement) ?? new Dictionary<string, string>()
                    : new Dictionary<string, string>();

                if (!exits.ContainsKey(direction.ToLower()))
                {
                    return JsonSerializer.Serialize(new { 
                        success = false,
                        error = $"No exit '{direction}' from current location. Available exits: {string.Join(", ", exits.Keys)}" 
                    }, _jsonOptions);
                }

                destinationId = exits[direction.ToLower()];
            }

            // Check if destination exists
            if (!gameState.WorldLocations.ContainsKey(destinationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Destination location '{destinationId}' not found" 
                }, _jsonOptions);
            }

            // Update player location
            gameState.CurrentLocationId = destinationId;
            
            // Mark location as visited
            var destinationJson = JsonSerializer.Serialize(gameState.WorldLocations[destinationId]);
            var destination = JsonSerializer.Deserialize<Dictionary<string, object>>(destinationJson);
            destination["visited"] = true;
            gameState.WorldLocations[destinationId] = destination;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Moved from '{currentLocationId}' to '{destinationId}'",
                previousLocation = currentLocationId,
                currentLocation = destinationId,
                direction = direction,
                destination = destination
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error updating player location: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("advance_time")]
    [Description("Move the in-game clock forward")]
    public async Task<string> AdvanceTime(
        [Description("Duration to advance time by (e.g., '1 hour', '30 minutes', 'to next morning', '2 days')")] string duration)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(duration))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Duration cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var originalTurn = gameState.GameTurnNumber;
            var originalTime = gameState.TimeOfDay;
            
            // Parse duration and advance time
            var (success, newTurn, newTimeOfDay, message) = ParseAndAdvanceTime(gameState.GameTurnNumber, gameState.TimeOfDay, duration);
            
            if (!success)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = message 
                }, _jsonOptions);
            }

            // Update game state
            gameState.GameTurnNumber = newTurn;
            gameState.TimeOfDay = newTimeOfDay;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Time advanced {duration}",
                duration = duration,
                previousTime = new { turn = originalTurn, timeOfDay = originalTime.ToString() },
                currentTime = new { turn = newTurn, timeOfDay = newTimeOfDay.ToString() },
                details = message
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error advancing time: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("update_player_money")]
    [Description("Modify player currency amounts")]
    public async Task<string> UpdatePlayerMoney(
        [Description("Amount to add (positive) or subtract (negative)")] int amount,
        [Description("Currency type (if multiple currencies supported)")] string currencyType = "gold",
        [Description("Reason for the transaction")] string reason = "")
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Ensure player exists
            if (gameState.Player == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Player not found" 
                }, _jsonOptions);
            }

            // Get current money from RulesetGameData (where extended player data is stored)
            var currentMoney = 0;
            if (gameState.RulesetGameData.ContainsKey("playerMoney"))
            {
                var moneyElement = gameState.RulesetGameData["playerMoney"];
                currentMoney = moneyElement.ValueKind == JsonValueKind.Number ? moneyElement.GetInt32() : 0;
            }
            
            var newAmount = currentMoney + amount;
            
            // Prevent negative money
            if (newAmount < 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Insufficient funds. Current: {currentMoney}, Required: {Math.Abs(amount)}, Shortfall: {Math.Abs(newAmount)}" 
                }, _jsonOptions);
            }

            // Update player money in RulesetGameData
            gameState.RulesetGameData["playerMoney"] = JsonSerializer.SerializeToElement(newAmount);
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = amount >= 0 ? $"Added {amount} {currencyType}" : $"Spent {Math.Abs(amount)} {currencyType}",
                transaction = new
                {
                    amount = amount,
                    currencyType = currencyType,
                    reason = reason,
                    previousAmount = currentMoney,
                    newAmount = newAmount
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error updating player money: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("update_bond_score")]
    [Description("Modify relationships between trainer and Pokemon")]
    public async Task<string> UpdateBondScore(
        [Description("Entity ID (e.g., Pokemon ID, companion ID)")] string entityId,
        [Description("Bond score change (positive or negative)")] int bondChange,
        [Description("Reason for bond change")] string reason = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Entity ID cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Check if entity exists
            if (!gameState.WorldEntities.ContainsKey(entityId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Entity '{entityId}' not found" 
                }, _jsonOptions);
            }

            // Get entity and update bond score
            var entityJson = JsonSerializer.Serialize(gameState.WorldEntities[entityId]);
            var entity = JsonSerializer.Deserialize<Dictionary<string, object>>(entityJson);
            
            var currentBondScore = 0;
            if (entity.ContainsKey("bondScore"))
            {
                var bondElement = entity["bondScore"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                currentBondScore = bondElement.ValueKind == JsonValueKind.Number ? bondElement.GetInt32() : 0;
            }

            var newBondScore = Math.Max(0, Math.Min(100, currentBondScore + bondChange)); // Clamp between 0-100
            entity["bondScore"] = newBondScore;
            
            // Update bond history
            if (!entity.ContainsKey("bondHistory"))
                entity["bondHistory"] = new List<object>();
            
            var historyElement = entity["bondHistory"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>());
            var bondHistory = historyElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<object>>(historyElement) ?? new List<object>()
                : new List<object>();
            
            bondHistory.Add(new {
                change = bondChange,
                reason = reason,
                previousScore = currentBondScore,
                newScore = newBondScore,
                timestamp = DateTime.UtcNow.ToString("O")
            });
            
            entity["bondHistory"] = bondHistory;
            gameState.WorldEntities[entityId] = entity;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            // Determine bond threshold status
            var bondStatus = newBondScore switch
            {
                >= 90 => "Exceptional Bond",
                >= 75 => "Strong Bond", 
                >= 50 => "Good Bond",
                >= 25 => "Developing Bond",
                _ => "Weak Bond"
            };

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Bond with {entityId} {(bondChange >= 0 ? "increased" : "decreased")} by {Math.Abs(bondChange)}",
                entityId = entityId,
                bondUpdate = new
                {
                    change = bondChange,
                    reason = reason,
                    previousScore = currentBondScore,
                    newScore = newBondScore,
                    bondStatus = bondStatus
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error updating bond score: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("get_faction_reputation")]
    [Description("Retrieve current standing with factions")]
    public async Task<string> GetFactionReputation(
        [Description("Faction ID to check reputation with")] string factionId)
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

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Check if faction exists
            if (!gameState.WorldEntities.ContainsKey(factionId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Faction '{factionId}' not found" 
                }, _jsonOptions);
            }

            var factionJson = JsonSerializer.Serialize(gameState.WorldEntities[factionId]);
            var faction = JsonSerializer.Deserialize<Dictionary<string, object>>(factionJson);
            
            var reputation = 0;
            if (faction.ContainsKey("reputation"))
            {
                var repElement = faction["reputation"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                reputation = repElement.ValueKind == JsonValueKind.Number ? repElement.GetInt32() : 0;
            }

            // Determine reputation status
            var reputationStatus = reputation switch
            {
                >= 75 => "Revered",
                >= 50 => "Honored", 
                >= 25 => "Friendly",
                >= 0 => "Neutral",
                >= -25 => "Unfriendly",
                >= -50 => "Hostile",
                _ => "Hated"
            };

            var factionName = faction.ContainsKey("name") ? faction["name"]?.ToString() : factionId;

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                factionId = factionId,
                factionName = factionName,
                reputation = new
                {
                    value = reputation,
                    status = reputationStatus,
                    description = $"{reputationStatus} ({reputation}/100)"
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error getting faction reputation: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("update_faction_reputation")]
    [Description("Modify faction relationship values")]
    public async Task<string> UpdateFactionReputation(
        [Description("Faction ID to update reputation with")] string factionId,
        [Description("Reputation change (positive or negative)")] int reputationChange,
        [Description("Reason for reputation change")] string reason = "")
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

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Check if faction exists
            if (!gameState.WorldEntities.ContainsKey(factionId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Faction '{factionId}' not found" 
                }, _jsonOptions);
            }

            var factionJson = JsonSerializer.Serialize(gameState.WorldEntities[factionId]);
            var faction = JsonSerializer.Deserialize<Dictionary<string, object>>(factionJson);
            
            var currentReputation = 0;
            if (faction.ContainsKey("reputation"))
            {
                var repElement = faction["reputation"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                currentReputation = repElement.ValueKind == JsonValueKind.Number ? repElement.GetInt32() : 0;
            }

            var newReputation = Math.Max(-100, Math.Min(100, currentReputation + reputationChange)); // Clamp between -100 and 100
            faction["reputation"] = newReputation;
            
            // Update reputation history
            if (!faction.ContainsKey("reputationHistory"))
                faction["reputationHistory"] = new List<object>();
            
            var historyElement = faction["reputationHistory"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>());
            var reputationHistory = historyElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<object>>(historyElement) ?? new List<object>()
                : new List<object>();
            
            reputationHistory.Add(new {
                change = reputationChange,
                reason = reason,
                previousReputation = currentReputation,
                newReputation = newReputation,
                timestamp = DateTime.UtcNow.ToString("O")
            });
            
            faction["reputationHistory"] = reputationHistory;
            gameState.WorldEntities[factionId] = faction;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            // Determine new reputation status
            var oldStatus = GetReputationStatus(currentReputation);
            var newStatus = GetReputationStatus(newReputation);
            var statusChanged = oldStatus != newStatus;

            var factionName = faction.ContainsKey("name") ? faction["name"]?.ToString() : factionId;

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Reputation with {factionName} {(reputationChange >= 0 ? "increased" : "decreased")} by {Math.Abs(reputationChange)}",
                factionId = factionId,
                factionName = factionName,
                reputationUpdate = new
                {
                    change = reputationChange,
                    reason = reason,
                    previousReputation = currentReputation,
                    newReputation = newReputation,
                    previousStatus = oldStatus,
                    newStatus = newStatus,
                    statusChanged = statusChanged
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error updating faction reputation: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("update_npc_objective")]
    [Description("Modify NPC quest states and goals")]
    public async Task<string> UpdateNpcObjective(
        [Description("NPC ID")] string npcId,
        [Description("Objective ID or quest ID")] string objectiveId,
        [Description("New objective status (active, completed, failed, available)")] string newStatus,
        [Description("Additional context or progress notes")] string progressNotes = "")
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

            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Objective ID cannot be null or empty" 
                }, _jsonOptions);
            }

            var validStatuses = new[] { "active", "completed", "failed", "available" };
            if (!validStatuses.Contains(newStatus.ToLower()))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Invalid status '{newStatus}'. Valid statuses: {string.Join(", ", validStatuses)}" 
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

            var npcJson = JsonSerializer.Serialize(gameState.WorldEntities[npcId]);
            var npc = JsonSerializer.Deserialize<Dictionary<string, object>>(npcJson);
            
            // Initialize objectives if not present
            if (!npc.ContainsKey("objectives"))
                npc["objectives"] = new Dictionary<string, object>();
            
            var objectivesElement = npc["objectives"] as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, object>());
            var objectives = objectivesElement.ValueKind == JsonValueKind.Object 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(objectivesElement) ?? new Dictionary<string, object>()
                : new Dictionary<string, object>();
            
            var previousStatus = "unknown";
            if (objectives.ContainsKey(objectiveId))
            {
                var objElement = objectives[objectiveId] as JsonElement? ?? JsonSerializer.SerializeToElement(new {});
                if (objElement.ValueKind == JsonValueKind.Object)
                {
                    var existingObj = JsonSerializer.Deserialize<Dictionary<string, object>>(objElement);
                    if (existingObj != null && existingObj.ContainsKey("status"))
                    {
                        previousStatus = existingObj["status"]?.ToString() ?? "unknown";
                    }
                }
            }

            // Update objective
            objectives[objectiveId] = new
            {
                status = newStatus.ToLower(),
                progressNotes = progressNotes,
                lastUpdated = DateTime.UtcNow.ToString("O"),
                previousStatus = previousStatus
            };
            
            npc["objectives"] = objectives;
            gameState.WorldEntities[npcId] = npc;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            var npcName = npc.ContainsKey("name") ? npc["name"]?.ToString() : npcId;

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Updated objective '{objectiveId}' for {npcName} to '{newStatus}'",
                npcId = npcId,
                npcName = npcName,
                objectiveUpdate = new
                {
                    objectiveId = objectiveId,
                    previousStatus = previousStatus,
                    newStatus = newStatus.ToLower(),
                    progressNotes = progressNotes
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error updating NPC objective: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    #endregion

    #region Helper Methods

    private (bool success, int newTurn, TimeOfDay newTimeOfDay, string message) ParseAndAdvanceTime(int currentTurn, TimeOfDay currentTime, string duration)
    {
        try
        {
            var lowerDuration = duration.ToLower().Trim();
            
            // Handle special cases
            if (lowerDuration.Contains("next morning") || lowerDuration.Contains("to morning"))
            {
                return (true, currentTurn + 1, TimeOfDay.Morning, "Advanced to next morning");
            }
            if (lowerDuration.Contains("next day") || lowerDuration.Contains("to next day"))
            {
                return (true, currentTurn + 1, TimeOfDay.Morning, "Advanced to next day");
            }
            if (lowerDuration.Contains("next night") || lowerDuration.Contains("to night"))
            {
                return (true, currentTurn, TimeOfDay.Night, "Advanced to night");
            }
            
            // Parse numeric durations
            var timeAdvancement = 0;
            
            if (lowerDuration.Contains("hour"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lowerDuration, @"(\d+)\s*hour");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var hours))
                {
                    timeAdvancement = hours;
                }
            }
            else if (lowerDuration.Contains("minute"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lowerDuration, @"(\d+)\s*minute");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var minutes))
                {
                    // Convert minutes to time periods (approximate)
                    timeAdvancement = Math.Max(1, minutes / 60);
                }
            }
            else if (lowerDuration.Contains("day"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lowerDuration, @"(\d+)\s*day");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
                {
                    return (true, currentTurn + days, currentTime, $"Advanced {days} day(s)");
                }
            }
            
            if (timeAdvancement > 0)
            {
                var newTurn = currentTurn;
                var newTime = currentTime;
                
                for (int i = 0; i < timeAdvancement; i++)
                {
                    newTime = AdvanceTimeOfDay(newTime);
                    if (newTime == TimeOfDay.Morning && i > 0) // New day started
                    {
                        newTurn++;
                    }
                }
                
                return (true, newTurn, newTime, $"Advanced time by {timeAdvancement} period(s)");
            }
            
            return (false, currentTurn, currentTime, $"Could not parse duration: {duration}");
        }
        catch (Exception ex)
        {
            return (false, currentTurn, currentTime, $"Error parsing duration: {ex.Message}");
        }
    }
    
    private TimeOfDay AdvanceTimeOfDay(TimeOfDay current)
    {
        return current switch
        {
            TimeOfDay.Morning => TimeOfDay.Day,
            TimeOfDay.Day => TimeOfDay.Afternoon,
            TimeOfDay.Afternoon => TimeOfDay.Dusk,
            TimeOfDay.Dusk => TimeOfDay.Night,
            TimeOfDay.Night => TimeOfDay.Morning,
            _ => TimeOfDay.Morning
        };
    }
    
    private string GetReputationStatus(int reputation)
    {
        return reputation switch
        {
            >= 75 => "Revered",
            >= 50 => "Honored", 
            >= 25 => "Friendly",
            >= 0 => "Neutral",
            >= -25 => "Unfriendly",
            >= -50 => "Hostile",
            _ => "Hated"
        };
    }

    #endregion

}