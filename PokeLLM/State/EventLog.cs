using System.Text.Json;

namespace PokeLLM.State;

public record GameEvent(
    string Id,
    string Type,
    string Description,
    Dictionary<string, object> Data,
    DateTime Timestamp)
{
    public static GameEvent Create(string type, string description, Dictionary<string, object>? data = null)
    {
        return new GameEvent(
            Guid.NewGuid().ToString(),
            type,
            description,
            data ?? new Dictionary<string, object>(),
            DateTime.UtcNow);
    }
}

public interface IEventLog
{
    Task AppendEventAsync(GameEvent gameEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameEvent>> GetEventsAsync(DateTime? since = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameEvent>> GetRecentEventsAsync(int count = 10, CancellationToken cancellationToken = default);
}

public class InMemoryEventLog : IEventLog
{
    private readonly List<GameEvent> _events = new();
    private readonly object _lock = new();

    public Task AppendEventAsync(GameEvent gameEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events.Add(gameEvent);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameEvent>> GetEventsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var filteredEvents = since.HasValue 
                ? _events.Where(e => e.Timestamp >= since.Value).ToList()
                : _events.ToList();
            
            return Task.FromResult<IReadOnlyList<GameEvent>>(filteredEvents);
        }
    }

    public Task<IReadOnlyList<GameEvent>> GetRecentEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var recentEvents = _events.TakeLast(count).ToList();
            return Task.FromResult<IReadOnlyList<GameEvent>>(recentEvents);
        }
    }
}