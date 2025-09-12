using PokeLLM.Game.Agents.Core.Contracts;
using PokeLLM.GameState;

namespace PokeLLM.Game.Agents.ContextBroker;

public interface IContextBroker
{
    Task<ContextPack> BuildContextAsync(string playerInput, CancellationToken cancellationToken = default);
}

public class ContextBroker : IContextBroker
{
    private readonly IGameStateRepository _repo;

    public ContextBroker(IGameStateRepository repo)
    {
        _repo = repo;
    }

    public async Task<ContextPack> BuildContextAsync(string playerInput, CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _repo.LoadLatestStateAsync();

            var cp = new ContextPack
            {
                SceneId = state.CurrentLocationId,
                SceneSummary = state.AdventureSummary,
                TimeOfDay = state.TimeOfDay.ToString(),
                Weather = state.Weather.ToString(),
                DialogueRecap = new List<string>(),
                RecentEvents = state.RecentEvents
                    .OrderByDescending(e => e.TurnNumber)
                    .Take(5)
                    .Select(e => new EventPreview { TurnNumber = e.TurnNumber, Description = e.EventDescription })
                    .ToList()
            };
            return cp;
        }
        catch
        {
            // Return minimal context on failure to avoid failing the turn outright
            return new ContextPack
            {
                SceneId = string.Empty,
                SceneSummary = string.Empty,
                TimeOfDay = string.Empty,
                Weather = string.Empty
            };
        }
    }
}

