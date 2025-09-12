using PokeLLM.Game.Agents.Core.Contracts;
using PokeLLM.Game.Agents.ContextBroker;
using PokeLLM.Game.Agents.Dialogue;
using PokeLLM.Game.Agents.Guard;
using PokeLLM.Game.Agents.Memory;
using PokeLLM.Game.Agents.Plot;
using PokeLLM.Game.Agents.World;

namespace PokeLLM.Game.Orchestration.MultiAgent;

public interface ITurnOrchestrator
{
    Task<string> ProcessTurnAsync(string playerInput, CancellationToken cancellationToken = default);
}

public class HandoffOrchestrator : ITurnOrchestrator
{
    private readonly IContextBroker _contextBroker;
    private readonly IGuardAgent _guard;
    private readonly IPlotDirector _plot;
    private readonly IDialogueAgent _dialogue;
    private readonly IWorldAgent _world;
    private readonly IMemoryCurator _memory;

    public HandoffOrchestrator(
        IContextBroker contextBroker,
        IGuardAgent guard,
        IPlotDirector plot,
        IDialogueAgent dialogue,
        IWorldAgent world,
        IMemoryCurator memory)
    {
        _contextBroker = contextBroker;
        _guard = guard;
        _plot = plot;
        _dialogue = dialogue;
        _world = world;
        _memory = memory;
    }

    public async Task<string> ProcessTurnAsync(string playerInput, CancellationToken cancellationToken = default)
    {
        var context = await _contextBroker.BuildContextAsync(playerInput, cancellationToken);
        var guardDecision = await _guard.AssessAsync(context, playerInput, cancellationToken);
        if (guardDecision.Status == GuardStatus.Reject)
        {
            return guardDecision.Narrative ?? "That action is not possible right now.";
        }

        var directive = await _plot.GetDirectiveAsync(context, playerInput, cancellationToken);

        // Dialogue-only handoff
        var result = await _dialogue.ExecuteAsync(context, directive, playerInput, cancellationToken);
        if (result.StateDelta != null)
        {
            await _world.ApplyAsync(result.StateDelta, cancellationToken);
            await _memory.CurateAsync(cancellationToken);
        }

        return result.FinalNarrative;
    }
}

