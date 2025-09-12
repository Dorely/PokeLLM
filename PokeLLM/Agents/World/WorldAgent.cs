using PokeLLM.Game.Agents.Core.Contracts;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Agents.World;

public interface IWorldAgent
{
    Task ApplyAsync(StateDelta delta, CancellationToken cancellationToken = default);
}

public class WorldAgent : IWorldAgent
{
    private readonly IGameStateRepository _repo;

    public WorldAgent(IGameStateRepository repo)
    {
        _repo = repo;
    }

    public async Task ApplyAsync(StateDelta delta, CancellationToken cancellationToken = default)
    {
        var state = await _repo.LoadLatestStateAsync();

        // Append events as simple RecentEvents entries
        foreach (var ev in delta.NewEvents)
        {
            state.RecentEvents.Add(new EventLog
            {
                TurnNumber = state.GameTurnNumber + 1,
                EventDescription = $"{ev.Type}: {ev.PayloadJson}"
            });
        }

        state.GameTurnNumber += 1;
        state.LastSaveTime = DateTime.UtcNow;
        await _repo.SaveStateAsync(state);
    }
}

