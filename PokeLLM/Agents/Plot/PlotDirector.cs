using PokeLLM.Game.Agents.Core.Contracts;

namespace PokeLLM.Game.Agents.Plot;

public interface IPlotDirector
{
    Task<PlotDirective> GetDirectiveAsync(ContextPack context, string playerInput, CancellationToken cancellationToken = default);
}

public class PlotDirector : IPlotDirector
{
    public Task<PlotDirective> GetDirectiveAsync(ContextPack context, string playerInput, CancellationToken cancellationToken = default)
    {
        // Dialogue-only prototype: return a simple directive
        return Task.FromResult(new PlotDirective
        {
            Pacing = "short",
            SpotlightNpcs = new List<string>()
        });
    }
}

