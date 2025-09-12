using PokeLLM.Game.Agents.Core.Contracts;

namespace PokeLLM.Game.Agents.Guard;

public interface IGuardAgent
{
    Task<GuardDecision> AssessAsync(ContextPack context, string playerInput, CancellationToken cancellationToken = default);
}

public class GuardAgent : IGuardAgent
{
    public Task<GuardDecision> AssessAsync(ContextPack context, string playerInput, CancellationToken cancellationToken = default)
    {
        var text = (playerInput ?? string.Empty).ToLowerInvariant();
        if (text.Contains("million gold") || text.Contains("instantly win") || text.Contains("cheat"))
        {
            return Task.FromResult(new GuardDecision
            {
                Status = GuardStatus.Reject,
                Narrative = "You look around, but nothing like that is here."
            });
        }

        return Task.FromResult(new GuardDecision { Status = GuardStatus.Valid });
    }
}

