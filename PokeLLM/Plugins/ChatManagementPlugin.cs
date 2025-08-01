using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing adventure summaries, recent events, and conversation/event history storage
/// </summary>
public class ChatManagementPlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IInformationManagementService _informationManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ChatManagementPlugin(
        IGameStateRepository gameStateRepo,
        IInformationManagementService informationManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _informationManagementService = informationManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("get_adventure_summary")]
    [Description("Get the current adventure summary from game state")]
    public async Task<string> GetAdventureSummary()
    {
        Debug.WriteLine($"[ChatManagementPlugin] GetAdventureSummary called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            var result = new
            {
                adventureSummary = gameState.AdventureSummary,
                sessionId = gameState.SessionId,
                gameTurnNumber = gameState.GameTurnNumber,
                currentPhase = gameState.CurrentPhase.ToString(),
                lastUpdated = gameState.LastSaveTime
            };
            
            Debug.WriteLine($"[ChatManagementPlugin] Retrieved adventure summary: {gameState.AdventureSummary.Length} characters");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error getting adventure summary: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("get_recent_events")]
    [Description("Get the recent events list from game state")]
    public async Task<string> GetRecentEvents()
    {
        Debug.WriteLine($"[ChatManagementPlugin] GetRecentEvents called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            var result = new
            {
                recentEvents = gameState.RecentEvents,
                eventCount = gameState.RecentEvents.Count,
                sessionId = gameState.SessionId,
                gameTurnNumber = gameState.GameTurnNumber,
                currentPhase = gameState.CurrentPhase.ToString()
            };
            
            Debug.WriteLine($"[ChatManagementPlugin] Retrieved {gameState.RecentEvents.Count} recent events");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error getting recent events: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("modify_recent_events")]
    [Description("Add, remove, or modify events in the recent events list")]
    public async Task<string> ModifyRecentEvents(
        [Description("Action to take: 'add', 'remove', or 'clear'")] string action,
        [Description("Event description to add, or index to remove (for remove action)")] string eventData = "")
    {
        Debug.WriteLine($"[ChatManagementPlugin] ModifyRecentEvents called with action: {action}, data: {eventData}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var originalCount = gameState.RecentEvents.Count;
            
            switch (action.ToLower())
            {
                case "add":
                    if (!string.IsNullOrWhiteSpace(eventData))
                    {
                        var eventLog = new EventLog
                        {
                            TurnNumber = gameState.GameTurnNumber,
                            EventDescription = eventData
                        };
                        gameState.RecentEvents.Add(eventLog);
                        Debug.WriteLine($"[ChatManagementPlugin] Added event: {eventData}");
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = "Event data cannot be empty for add action" }, _jsonOptions);
                    }
                    break;
                    
                case "remove":
                    if (int.TryParse(eventData, out int index) && index >= 0 && index < gameState.RecentEvents.Count)
                    {
                        var removedEvent = gameState.RecentEvents[index];
                        gameState.RecentEvents.RemoveAt(index);
                        Debug.WriteLine($"[ChatManagementPlugin] Removed event at index {index}: {removedEvent.EventDescription}");
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = $"Invalid index for remove action: {eventData}" }, _jsonOptions);
                    }
                    break;
                    
                case "clear":
                    gameState.RecentEvents.Clear();
                    Debug.WriteLine($"[ChatManagementPlugin] Cleared all recent events");
                    break;
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Invalid action: {action}. Use 'add', 'remove', or 'clear'" }, _jsonOptions);
            }
            
            // Update last save time and save
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepo.SaveStateAsync(gameState);
            
            var result = new
            {
                success = true,
                action = action,
                originalCount = originalCount,
                newCount = gameState.RecentEvents.Count,
                recentEvents = gameState.RecentEvents
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error modifying recent events: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("update_adventure_summary")]
    [Description("Update the adventure summary in game state")]
    public async Task<string> UpdateAdventureSummary(
        [Description("New adventure summary text")] string summary)
    {
        Debug.WriteLine($"[ChatManagementPlugin] UpdateAdventureSummary called with summary length: {summary?.Length ?? 0}");
        
        try
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return JsonSerializer.Serialize(new { error = "Summary cannot be empty" }, _jsonOptions);
            }
            
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var oldSummary = gameState.AdventureSummary;
            
            gameState.AdventureSummary = summary;
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _gameStateRepo.SaveStateAsync(gameState);
            
            var result = new
            {
                success = true,
                oldSummaryLength = oldSummary.Length,
                newSummaryLength = summary.Length,
                sessionId = gameState.SessionId,
                gameTurnNumber = gameState.GameTurnNumber,
                updatedAt = gameState.LastSaveTime
            };
            
            Debug.WriteLine($"[ChatManagementPlugin] Successfully updated adventure summary from {oldSummary.Length} to {summary.Length} characters");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error updating adventure summary: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("store_conversation_history")]
    [Description("Format and store dialogue/conversation history to the vector store")]
    public async Task<string> StoreConversationHistory(
        [Description("Summary of the conversation")] string conversationSummary,
        [Description("Full dialogue transcript")] string fullTranscript,
        [Description("List of entities involved in the conversation")] List<string> involvedEntities,
        [Description("Location where the conversation took place")] string locationId = "")
    {
        Debug.WriteLine($"[ChatManagementPlugin] StoreConversationHistory called: summary length {conversationSummary?.Length ?? 0}, transcript length {fullTranscript?.Length ?? 0}");
        
        try
        {
            if (string.IsNullOrWhiteSpace(conversationSummary))
            {
                return JsonSerializer.Serialize(new { error = "Conversation summary cannot be empty" }, _jsonOptions);
            }
            
            // Get current game state for context
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var currentLocationId = string.IsNullOrWhiteSpace(locationId) ? gameState.CurrentLocationId : locationId;
            
            // Store as narrative event in vector store
            var result = await _informationManagementService.LogNarrativeEventAsync(
                eventType: "conversation",
                eventSummary: conversationSummary,
                fullTranscript: fullTranscript ?? "",
                involvedEntities: involvedEntities ?? new List<string>(),
                locationId: currentLocationId
            );
            
            var response = new
            {
                success = true,
                result = result,
                sessionId = gameState.SessionId,
                gameTurnNumber = gameState.GameTurnNumber,
                locationId = currentLocationId,
                involvedEntities = involvedEntities ?? new List<string>(),
                storedAt = DateTime.UtcNow
            };
            
            Debug.WriteLine($"[ChatManagementPlugin] Successfully stored conversation history: {result}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error storing conversation history: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("store_event_history")]
    [Description("Store a detailed summary of important events into the vector store")]
    public async Task<string> StoreEventHistory(
        [Description("Type of event (e.g., 'battle', 'discovery', 'story_event', 'character_development')")] string eventType,
        [Description("Summary of the important event")] string eventSummary,
        [Description("Detailed description or transcript of the event")] string eventDetails,
        [Description("List of entities involved in the event")] List<string> involvedEntities,
        [Description("Location where the event occurred")] string locationId = "",
        [Description("Turn number the event occurred")] int? turnNumber = null)
    {
        Debug.WriteLine($"[ChatManagementPlugin] StoreEventHistory called: type {eventType}, summary length {eventSummary?.Length ?? 0}");
        
        try
        {
            if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(eventSummary))
            {
                return JsonSerializer.Serialize(new { error = "Event type and summary cannot be empty" }, _jsonOptions);
            }
            
            // Get current game state for context
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var currentLocationId = string.IsNullOrWhiteSpace(locationId) ? gameState.CurrentLocationId : locationId;
            
            // Store as narrative event in vector store
            var result = await _informationManagementService.LogNarrativeEventAsync(
                eventType: eventType,
                eventSummary: eventSummary,
                fullTranscript: eventDetails ?? "",
                involvedEntities: involvedEntities ?? new List<string>(),
                locationId: currentLocationId,
                turnNumber: turnNumber
            );
            
            // Also add to recent events in game state for immediate context
            var eventLog = new EventLog
            {
                TurnNumber = turnNumber ?? gameState.GameTurnNumber,
                EventDescription = $"{eventType}: {eventSummary}"
            };
            gameState.RecentEvents.Add(eventLog);
            gameState.LastSaveTime = DateTime.UtcNow;
            await _gameStateRepo.SaveStateAsync(gameState);
            
            var response = new
            {
                success = true,
                result = result,
                eventType = eventType,
                sessionId = gameState.SessionId,
                gameTurnNumber = gameState.GameTurnNumber,
                locationId = currentLocationId,
                involvedEntities = involvedEntities ?? new List<string>(),
                storedAt = DateTime.UtcNow,
                addedToRecentEvents = true
            };
            
            Debug.WriteLine($"[ChatManagementPlugin] Successfully stored event history: {result}");
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error storing event history: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("search_memories")]
    [Description("Search for past narrative events and memories from the vector store")]
    public async Task<string> SearchMemories(
        [Description("Query to search for in past events")] string query,
        [Description("List of entities to filter by (optional)")] List<string> involvedEntities = null,
        [Description("Minimum relevance score (0.0 to 1.0)")] double minRelevanceScore = 0.75)
    {
        Debug.WriteLine($"[ChatManagementPlugin] SearchMemories called with query: {query}");
        
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new { error = "Search query cannot be empty" }, _jsonOptions);
            }
            
            // Get current session ID
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Search for memories
            var memories = await _informationManagementService.FindMemoriesAsync(
                sessionId: gameState.SessionId,
                query: query,
                involvedEntities: involvedEntities,
                minRelevanceScore: minRelevanceScore
            );
            
            var result = new
            {
                query = query,
                memoryCount = memories.Count(),
                memories = memories.Select(m => new
                {
                    gameTurnNumber = m.GameTurnNumber,
                    eventType = m.EventType,
                    eventSummary = m.EventSummary,
                    involvedEntities = m.InvolvedEntities,
                    locationId = m.LocationId
                }).ToList(),
                searchedAt = DateTime.UtcNow
            };
            
            Debug.WriteLine($"[ChatManagementPlugin] Found {memories.Count()} memories for query: {query}");
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatManagementPlugin] Error searching memories: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}
