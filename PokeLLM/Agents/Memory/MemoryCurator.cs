namespace PokeLLM.Game.Agents.Memory;

public interface IMemoryCurator
{
    Task CurateAsync(CancellationToken cancellationToken = default);
}

public class MemoryCurator : IMemoryCurator
{
    public Task CurateAsync(CancellationToken cancellationToken = default)
    {
        // No-op for prototype
        return Task.CompletedTask;
    }
}

