using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace PokeLLM.Agents;

public interface IGameAgentManager
{
    void RegisterAgent(IGameAgent agent);
    IGameAgent? GetAgent(string agentId);
    T? GetAgent<T>() where T : class, IGameAgent;
    IEnumerable<IGameAgent> GetAllAgents();
    Task<Dictionary<string, object>> GetMetricsAsync();
}

public class GameAgentManager : IGameAgentManager, IDisposable
{
    private readonly Dictionary<string, IGameAgent> _agents = new();
    private readonly Dictionary<string, AgentMetrics> _metrics = new();
    private readonly ILogger<GameAgentManager> _logger;
    private readonly object _lock = new();

    public GameAgentManager(ILogger<GameAgentManager> logger)
    {
        _logger = logger;
    }

    public void RegisterAgent(IGameAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        
        lock (_lock)
        {
            if (_agents.ContainsKey(agent.Id))
            {
                throw new InvalidOperationException($"Agent with ID '{agent.Id}' is already registered.");
            }
            
            _agents[agent.Id] = agent;
            _metrics[agent.Id] = new AgentMetrics();
            
            _logger.LogInformation("Registered agent: {AgentId} ({AgentName})", agent.Id, agent.Name);
        }
    }

    public IGameAgent? GetAgent(string agentId)
    {
        lock (_lock)
        {
            return _agents.GetValueOrDefault(agentId);
        }
    }

    public T? GetAgent<T>() where T : class, IGameAgent
    {
        lock (_lock)
        {
            return _agents.Values.OfType<T>().FirstOrDefault();
        }
    }

    public IEnumerable<IGameAgent> GetAllAgents()
    {
        lock (_lock)
        {
            return _agents.Values.ToList();
        }
    }

    public Task<Dictionary<string, object>> GetMetricsAsync()
    {
        lock (_lock)
        {
            var metrics = new Dictionary<string, object>
            {
                ["total_agents"] = _agents.Count,
                ["agent_metrics"] = _metrics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)new
                    {
                        invocation_count = kvp.Value.InvocationCount,
                        total_latency_ms = kvp.Value.TotalLatencyMs,
                        average_latency_ms = kvp.Value.AverageLatencyMs,
                        last_invocation = kvp.Value.LastInvocation
                    })
            };
            
            return Task.FromResult(metrics);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _agents.Clear();
            _metrics.Clear();
        }
    }

    internal void RecordInvocation(string agentId, TimeSpan latency)
    {
        lock (_lock)
        {
            if (_metrics.TryGetValue(agentId, out var metrics))
            {
                metrics.RecordInvocation(latency);
            }
        }
    }
}

internal class AgentMetrics
{
    public int InvocationCount { get; private set; }
    public long TotalLatencyMs { get; private set; }
    public DateTime? LastInvocation { get; private set; }
    
    public double AverageLatencyMs => InvocationCount > 0 ? (double)TotalLatencyMs / InvocationCount : 0;

    public void RecordInvocation(TimeSpan latency)
    {
        InvocationCount++;
        TotalLatencyMs += (long)latency.TotalMilliseconds;
        LastInvocation = DateTime.UtcNow;
    }
}