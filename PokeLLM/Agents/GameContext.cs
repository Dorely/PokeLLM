using PokeLLM.State;

namespace PokeLLM.Agents;

public record GameContext(
    AdventureModule AdventureModule,
    GameStateSnapshot CurrentState,
    IReadOnlyList<GameEvent> RecentEvents,
    DateTime Timestamp)
{
    public static GameContext Create(
        AdventureModule module,
        GameStateSnapshot state,
        IEnumerable<GameEvent> events)
    {
        return new GameContext(
            module,
            state,
            events.ToList(),
            DateTime.UtcNow);
    }
}