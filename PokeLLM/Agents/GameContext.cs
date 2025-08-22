using PokeLLM.State;

namespace PokeLLM.Agents;

public record GameContext(
    string SessionId,
    AdventureModule AdventureModule,
    State.PlayerState PlayerState,
    IReadOnlyList<GameEvent> RecentEvents,
    DateTime Timestamp)
{
    public int TurnNumber => RecentEvents.Count;

    public static GameContext Create(
        string sessionId,
        AdventureModule module,
        State.PlayerState playerState,
        IEnumerable<GameEvent> events)
    {
        return new GameContext(
            sessionId,
            module,
            playerState,
            events.ToList(),
            DateTime.UtcNow);
    }
}