using PokeLLM.State;

namespace PokeLLM.Agents;

public record GameContext(
    AdventureModule AdventureModule,
    State.PlayerState PlayerState,
    IReadOnlyList<GameEvent> RecentEvents,
    DateTime Timestamp)
{
    public static GameContext Create(
        AdventureModule module,
        State.PlayerState playerState,
        IEnumerable<GameEvent> events)
    {
        return new GameContext(
            module,
            playerState,
            events.ToList(),
            DateTime.UtcNow);
    }
}