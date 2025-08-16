using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.Game.VectorStore.Models;
using PokeLLM.GameLogic;

namespace PokeLLM.Game.Plugins;

public class ExplorationPhasePlugin
{
    private readonly IGameLogicService _gameLogicService;
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IWorldManagementService _worldManagementService;
    private readonly INpcManagementService _npcManagementService;
    private readonly IEntityService _entityService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExplorationPhasePlugin(
        IGameLogicService gameLogicService,
        IGameStateRepository gameStateRepo,
        IWorldManagementService worldManagementService,
        INpcManagementService npcManagementService,
        IEntityService entityService,
        IInformationManagementService informationManagementService)
    {
        _gameLogicService = gameLogicService;
        _gameStateRepo = gameStateRepo;
        _worldManagementService = worldManagementService;
        _npcManagementService = npcManagementService;
        _entityService = entityService;
        _informationManagementService = informationManagementService;
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

    [KernelFunction("manage_player_entities")]
    [Description("Generic entity management - fallback for rulesets that don't provide specific entity management functions")]
    public async Task<string> ManagePlayerEntities(
        [Description("Action type for entity management")] string action,
        [Description("Entity ID for operations")] string entityId = "",
        [Description("Additional parameters as JSON string")] string parameters = "{}")
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManagePlayerEntities called: {action}");
        
        try
        {
            // This is a generic fallback - for Pokemon games, this should be handled by the ruleset
            // For other game types, specific implementation would be provided by their rulesets
            
            switch (action.ToLower())
            {
                case "get_player_entities":
                    // Return basic player team information
                    var gameState = await _gameStateRepo.LoadLatestStateAsync();
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = "Team entity queries are now handled through dynamic functions in the active ruleset",
                        playerLevel = gameState.Player.Level,
                        playerExperience = gameState.Player.Experience,
                        playerConditions = gameState.Player.Conditions,
                        // teamEntities and boxedEntities are now managed through ruleset data
                        action = "get_player_entities"
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new 
                    { 
                        error = "Entity management action not supported by fallback plugin. Ruleset should provide specific entity management functions.",
                        action = action,
                        availableActions = new[] { "get_player_entities" }
                    }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManagePlayerEntities: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
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

    [KernelFunction("manage_vector_store")]
    [Description("Handle all vector store operations for entities, locations, lore, rules, and narrative logs")]
    public async Task<string> ManageVectorStore(
        [Description("Operation: 'search_entities', 'upsert_entity', 'get_entity', 'search_locations', 'upsert_location', 'get_location', 'search_lore', 'upsert_lore', 'get_lore', 'search_rules', 'upsert_rule', 'get_rule', 'log_event', 'find_memories'")] string operation,
        [Description("Search queries (for search operations)")] List<string> queries = null,
        [Description("Entity/Location/Entry ID (for specific lookups)")] string id = "",
        [Description("Entity/Entry type for filtering")] string type = "",
        [Description("Name/Title of the entry")] string name = "",
        [Description("Description/Content of the entry")] string content = "",
        [Description("Additional properties as JSON")] string propertiesJson = "",
        [Description("Tags for categorization")] List<string> tags = null,
        [Description("Region (for locations)")] string region = "",
        [Description("Event type (for logging)")] string eventType = "",
        [Description("Full transcript (for events)")] string fullTranscript = "",
        [Description("Involved entities (for events)")] List<string> involvedEntities = null,
        [Description("Location ID (for events)")] string locationId = "")
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] ManageVectorStore called: {operation}");
        
        try
        {
            switch (operation.ToLower())
            {
                // Entity operations
                case "search_entities":
                    var entityResults = await _informationManagementService.SearchEntitiesAsync(queries ?? new List<string>(), type);
                    return JsonSerializer.Serialize(new { entities = entityResults, operation = operation }, _jsonOptions);
                    
                case "upsert_entity":
                    var entityResult = await _informationManagementService.UpsertEntityAsync(id, type, name, content, propertiesJson ?? "{}");
                    return JsonSerializer.Serialize(new { result = entityResult, operation = operation }, _jsonOptions);
                    
                case "get_entity":
                    var entity = await _informationManagementService.GetEntityAsync(id);
                    return JsonSerializer.Serialize(new { entity = entity, operation = operation }, _jsonOptions);
                
                // Location operations
                case "search_locations":
                case "get_location":
                    var location = await _informationManagementService.GetLocationAsync(id);
                    return JsonSerializer.Serialize(new { location = location, operation = operation }, _jsonOptions);
                    
                case "upsert_location":
                    var locationResult = await _informationManagementService.UpsertLocationAsync(id, name, content, region, tags);
                    return JsonSerializer.Serialize(new { result = locationResult, operation = operation }, _jsonOptions);
                
                // Lore operations
                case "search_lore":
                    var loreResults = await _informationManagementService.SearchLoreAsync(queries ?? new List<string>(), type);
                    return JsonSerializer.Serialize(new { lore = loreResults, operation = operation }, _jsonOptions);
                    
                case "upsert_lore":
                    var loreResult = await _informationManagementService.UpsertLoreAsync(id, type, name, content, tags);
                    return JsonSerializer.Serialize(new { result = loreResult, operation = operation }, _jsonOptions);
                    
                case "get_lore":
                    var loreEntry = await _informationManagementService.GetLoreAsync(id);
                    return JsonSerializer.Serialize(new { lore = loreEntry, operation = operation }, _jsonOptions);
                
                // Game rule operations
                case "search_rules":
                    var ruleResults = await _informationManagementService.SearchGameRulesAsync(queries ?? new List<string>(), type);
                    return JsonSerializer.Serialize(new { rules = ruleResults, operation = operation }, _jsonOptions);
                    
                case "upsert_rule":
                    var ruleResult = await _informationManagementService.UpsertGameRuleAsync(id, type, name, content, tags);
                    return JsonSerializer.Serialize(new { result = ruleResult, operation = operation }, _jsonOptions);
                    
                case "get_rule":
                    var rule = await _informationManagementService.GetGameRuleAsync(id);
                    return JsonSerializer.Serialize(new { rule = rule, operation = operation }, _jsonOptions);
                
                // Narrative log operations
                case "log_event":
                    var logResult = await _informationManagementService.LogNarrativeEventAsync(eventType, name, fullTranscript, involvedEntities ?? new List<string>(), locationId);
                    return JsonSerializer.Serialize(new { result = logResult, operation = operation }, _jsonOptions);
                    
                case "find_memories":
                    var gameState = await _gameStateRepo.LoadLatestStateAsync();
                    var memories = await _informationManagementService.FindMemoriesAsync(gameState.SessionId, content, involvedEntities);
                    return JsonSerializer.Serialize(new { memories = memories, operation = operation }, _jsonOptions);
                
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown vector store operation: {operation}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in ManageVectorStore: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, operation = operation }, _jsonOptions);
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
            
            // Log the event if requested
            if (logEvent)
            {
                await _informationManagementService.LogNarrativeEventAsync(
                    eventType,
                    eventDescription,
                    eventDescription, // Using description as full transcript for now
                    involvedEntities ?? new List<string>(),
                    currentLocationId,
                    null,
                    gameState.GameTurnNumber
                );
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

    [KernelFunction("get_player_team_status")]
    [Description("Get current player team composition and status")]
    public async Task<string> GetPlayerTeamStatus()
    {
        Debug.WriteLine($"[ExplorationPhasePlugin] GetPlayerTeamStatus called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Return generic team status since specific team management is now handled by rulesets
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Team status queries are now handled through dynamic functions in the active ruleset",
                playerLevel = gameState.Player.Level,
                playerExperience = gameState.Player.Experience,
                playerConditions = gameState.Player.Conditions,
                // teamEntities and boxedEntities are now managed through ruleset data
                action = "get_player_team_status"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExplorationPhasePlugin] Error in GetPlayerTeamStatus: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = "get_player_team_status" }, _jsonOptions);
        }
    }
}