using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.State;
using System.Runtime.CompilerServices;

namespace PokeLLM.Agents;

public interface IGameSession
{
    string SessionId { get; }
    GameContext CurrentContext { get; }
    bool IsActive { get; }
    
    Task StartNewGameAsync(string playerName, string characterBackstory, string preferredSetting, CancellationToken cancellationToken = default);
    IAsyncEnumerable<GameTurnResult> ProcessPlayerInputAsync(string userInput, CancellationToken cancellationToken = default);
    Task SaveGameAsync(CancellationToken cancellationToken = default);
    Task LoadGameAsync(string saveData, CancellationToken cancellationToken = default);
    Task EndSessionAsync();
}

public class GameSession : IGameSession
{
    private readonly IGameAgentManager _agentManager;
    private readonly IEventLog _eventLog;
    private readonly ILogger<GameSession> _logger;
    private readonly ChatHistory _conversationHistory;
    
    private GameContext? _currentContext;
    private AdventureModule? _adventureModule;
    private GameStateSnapshot? _currentState;

    public string SessionId { get; }
    public GameContext CurrentContext => _currentContext ?? throw new InvalidOperationException("Game session not started");
    public bool IsActive { get; private set; }

    public GameSession(
        IGameAgentManager agentManager,
        IEventLog eventLog,
        ILogger<GameSession> logger)
    {
        SessionId = Guid.NewGuid().ToString();
        _agentManager = agentManager;
        _eventLog = eventLog;
        _logger = logger;
        _conversationHistory = new ChatHistory();
        IsActive = false;
    }

    public async Task StartNewGameAsync(
        string playerName, 
        string characterBackstory, 
        string preferredSetting, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting new game session {SessionId} for player {PlayerName}", SessionId, playerName);

        // Step 1: Run SetupAgent to generate Adventure Module
        var setupAgent = _agentManager.GetAgent("setup-agent") as SetupAgent
            ?? throw new InvalidOperationException("Setup agent not found");

        _adventureModule = await setupAgent.GenerateAdventureModuleAsync(
            playerName, characterBackstory, preferredSetting, cancellationToken);

        // Step 2: Initialize game state from Adventure Module
        _currentState = CreateInitialGameState(_adventureModule);

        // Step 3: Create initial game context
        var recentEvents = await _eventLog.GetRecentEventsAsync(10, cancellationToken);
        _currentContext = GameContext.Create(_adventureModule, _currentState, recentEvents);

        // Step 4: Initialize conversation with setup summary
        await InitializeConversationAsync(cancellationToken);

        IsActive = true;
        
        await _eventLog.AppendEventAsync(
            GameEvent.Create("game_session_started", $"New game session started for {playerName}",
                new Dictionary<string, object> { ["session_id"] = SessionId, ["player_name"] = playerName }),
            cancellationToken);

        _logger.LogInformation("Game session {SessionId} successfully started", SessionId);
    }

    public async IAsyncEnumerable<GameTurnResult> ProcessPlayerInputAsync(
        string userInput, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsActive || _currentContext == null)
        {
            throw new InvalidOperationException("Game session is not active");
        }

        _logger.LogInformation("Processing player input in session {SessionId}: {Input}", SessionId, userInput);

        // Add player input to conversation history
        _conversationHistory.AddUserMessage(userInput);

        // Get GM Supervisor to coordinate the response
        var supervisorAgent = _agentManager.GetAgent("gm-supervisor") as GMSupervisorAgent;
        if (supervisorAgent == null)
        {
            yield return new GameTurnResult(
                SessionId: SessionId,
                PlayerInput: userInput,
                Response: "GM Supervisor agent not available.",
                UpdatedContext: _currentContext,
                TurnNumber: _conversationHistory.Count / 2,
                Timestamp: DateTime.UtcNow
            );
            yield break;
        }

        // Stream responses from the supervisor
        await foreach (var response in ProcessSupervisorResponseAsync(supervisorAgent, userInput, cancellationToken))
        {
            yield return response;
        }
    }

    public async Task SaveGameAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive || _currentContext == null)
        {
            throw new InvalidOperationException("Cannot save inactive game session");
        }

        _logger.LogInformation("Saving game session {SessionId}", SessionId);

        // In a full implementation, this would serialize to persistent storage
        var saveData = new GameSaveData(
            SessionId: SessionId,
            AdventureModule: _adventureModule!,
            CurrentState: _currentState!,
            ConversationHistory: _conversationHistory.ToList(),
            SavedAt: DateTime.UtcNow
        );

        await _eventLog.AppendEventAsync(
            GameEvent.Create("game_saved", "Game session saved",
                new Dictionary<string, object> { ["session_id"] = SessionId }),
            cancellationToken);

        _logger.LogInformation("Game session {SessionId} saved successfully", SessionId);
    }

    public async Task LoadGameAsync(string saveData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading game session {SessionId}", SessionId);

        // In a full implementation, this would deserialize from persistent storage
        // For now, just log the operation
        await _eventLog.AppendEventAsync(
            GameEvent.Create("game_loaded", "Game session loaded",
                new Dictionary<string, object> { ["session_id"] = SessionId }),
            cancellationToken);

        IsActive = true;
        _logger.LogInformation("Game session {SessionId} loaded successfully", SessionId);
    }

    public async Task EndSessionAsync()
    {
        _logger.LogInformation("Ending game session {SessionId}", SessionId);

        IsActive = false;
        _conversationHistory.Clear();

        await _eventLog.AppendEventAsync(
            GameEvent.Create("game_session_ended", "Game session ended",
                new Dictionary<string, object> { ["session_id"] = SessionId }),
            CancellationToken.None);

        _logger.LogInformation("Game session {SessionId} ended", SessionId);
    }

    private async IAsyncEnumerable<GameTurnResult> ProcessSupervisorResponseAsync(
        GMSupervisorAgent supervisorAgent,
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var response in supervisorAgent.SuperviseAndRouteAsync(userInput, _currentContext!, cancellationToken))
        {
            // Add response to conversation history
            _conversationHistory.Add(response);

            // Update game context with latest events
            var recentEvents = await _eventLog.GetRecentEventsAsync(10, cancellationToken);
            _currentContext = GameContext.Create(_adventureModule!, _currentState!, recentEvents);

            yield return new GameTurnResult(
                SessionId: SessionId,
                PlayerInput: userInput,
                Response: response.Content ?? "",
                UpdatedContext: _currentContext,
                TurnNumber: _conversationHistory.Count / 2, // Rough turn counter
                Timestamp: DateTime.UtcNow
            );
        }

        await _eventLog.AppendEventAsync(
            GameEvent.Create("player_turn_processed", "Player turn completed",
                new Dictionary<string, object> { ["session_id"] = SessionId, ["input"] = userInput }),
            cancellationToken);
    }

    private GameStateSnapshot CreateInitialGameState(AdventureModule adventureModule)
    {
        var playerState = new State.PlayerState(
            Name: adventureModule.PlayerCharacter.Name,
            Level: adventureModule.PlayerCharacter.Stats["Level"],
            Health: adventureModule.PlayerCharacter.Stats["Health"],
            MaxHealth: adventureModule.PlayerCharacter.Stats["Health"],
            Experience: 0,
            Stats: adventureModule.PlayerCharacter.Stats,
            Inventory: new List<string> { "Potion", "Pokeball" },
            CurrentLocation: adventureModule.Regions.First().Locations.First()
        );

        var worldState = new WorldState(
            CurrentRegion: adventureModule.Regions.First().Name,
            CurrentLocation: playerState.CurrentLocation,
            LocationData: new Dictionary<string, object>(),
            ActiveEvents: new List<string>(),
            NpcStates: adventureModule.NpcSeeds.ToDictionary(
                npc => npc.Id,
                npc => new NpcState(npc.Id, npc.Name, npc.Location, new Dictionary<string, object>())
            )
        );

        return GameStateSnapshot.Create(playerState, worldState);
    }

    private async Task InitializeConversationAsync(CancellationToken cancellationToken)
    {
        // Add initial system context
        var systemMessage = $"""
            Adventure Module: {_adventureModule!.Title}
            Setting: {_adventureModule.Setting}
            Player: {_adventureModule.PlayerCharacter.Name}
            Starting Location: {_currentState!.Player.CurrentLocation}
            
            Active Quests:
            {string.Join("\n", _adventureModule.Quests.Where(q => q.Status == QuestStatus.Active).Select(q => $"- {q.Title}: {q.Description}"))}
            """;

        _conversationHistory.AddSystemMessage(systemMessage);

        // Get initial narrative description
        var narratorAgent = _agentManager.GetAgent("narrator-agent") as NarratorAgent
            ?? throw new InvalidOperationException("Narrator agent not found");

        var initialDescription = await narratorAgent.DescribeLocationAsync(
            _currentState.Player.CurrentLocation, 
            _currentContext!, 
            cancellationToken);

        var welcomeMessage = $"""
            Welcome to {_adventureModule.Title}!
            
            {initialDescription}
            
            Your adventure begins now. What would you like to do?
            """;

        _conversationHistory.AddAssistantMessage(welcomeMessage);
    }
}

public record GameTurnResult(
    string SessionId,
    string PlayerInput,
    string Response,
    GameContext UpdatedContext,
    int TurnNumber,
    DateTime Timestamp);

public record GameSaveData(
    string SessionId,
    AdventureModule AdventureModule,
    GameStateSnapshot CurrentState,
    List<ChatMessageContent> ConversationHistory,
    DateTime SavedAt);