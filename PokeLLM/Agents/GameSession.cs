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
    private State.PlayerState? _currentPlayerState;

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
        _currentPlayerState = CreateInitialGameState(_adventureModule);

        // Step 3: Create initial game context
        var recentEvents = await _eventLog.GetRecentEventsAsync(10, cancellationToken);
        _currentContext = GameContext.Create(SessionId, _adventureModule, _currentPlayerState!, recentEvents);

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
            PlayerState: _currentPlayerState!,
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
        // Create or get memory-enabled thread for this session
        var thread = await supervisorAgent.CreateThreadAsync(SessionId, cancellationToken);
        
        // Process the input through the supervisor
        var supervisorResponse = await supervisorAgent.ProcessPlayerInputAsync(userInput, _currentContext!, thread, cancellationToken);
        
        // Add response to conversation history
        _conversationHistory.AddAssistantMessage(supervisorResponse.FinalNarration);

        // Update game context with latest events
        var recentEvents = await _eventLog.GetRecentEventsAsync(10, cancellationToken);
        _currentContext = GameContext.Create(SessionId, _adventureModule!, _currentPlayerState!, recentEvents);

        yield return new GameTurnResult(
            SessionId: SessionId,
            PlayerInput: userInput,
            Response: supervisorResponse.FinalNarration,
            UpdatedContext: _currentContext,
            TurnNumber: _conversationHistory.Count / 2, // Rough turn counter
            Timestamp: DateTime.UtcNow
        );

        await _eventLog.AppendEventAsync(
            GameEvent.Create("player_turn_processed", "Player turn completed",
                new Dictionary<string, object> { ["session_id"] = SessionId, ["input"] = userInput }),
            cancellationToken);
    }

    private State.PlayerState CreateInitialGameState(AdventureModule adventureModule)
    {
        var playerState = new State.PlayerState
        {
            Name = "Player", // Default name since PlayerCharacter doesn't exist yet
            Level = 1,
            HP = 20,
            MaxHP = 20,
            Experience = 0,
            Stats = new State.Stats
            {
                Strength = 10,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = 10,
                Charisma = 10,
                CurrentVigor = 10,
                MaxVigor = 10
            },
            CharacterDetails = new State.CharacterDetails
            {
                Class = "Trainer",
                Money = 500
            }
        };

        return playerState;
    }

    private async Task InitializeConversationAsync(CancellationToken cancellationToken)
    {
        // Add initial system context
        var systemMessage = $"""
            Adventure Module: {_adventureModule!.Name}
            Setting: {_adventureModule.Theme}
            Player: {_currentPlayerState!.Name}
            Starting Location: [Location tracking simplified]
            
            Active Quests:
            {string.Join("\n", _adventureModule.Quests.Where(q => q.IsActive).Select(q => $"- {q.Name}: {q.Description}"))}
            """;

        _conversationHistory.AddSystemMessage(systemMessage);

        // Get initial narrative description
        var narratorAgent = _agentManager.GetAgent("narrator-agent") as NarratorAgent
            ?? throw new InvalidOperationException("Narrator agent not found");

        var initialDescription = await narratorAgent.DescribeLocationAsync(
            "[Location tracking simplified]", 
            _currentContext!, 
            null, // No thread for this simple case
            cancellationToken);

        var welcomeMessage = $"""
            Welcome to {_adventureModule.Name}!
            
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
    State.PlayerState PlayerState,
    List<ChatMessageContent> ConversationHistory,
    DateTime SavedAt);