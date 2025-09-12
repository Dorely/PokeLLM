using System.Text.Json.Serialization;

namespace PokeLLM.Game.Agents.Core.Contracts;

public class ContextPack
{
    public string SceneId { get; set; } = string.Empty;
    public string SceneSummary { get; set; } = string.Empty;
    public List<string> DialogueRecap { get; set; } = new();
    public List<EventPreview> RecentEvents { get; set; } = new();
    public string TimeOfDay { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
}

public class EventPreview
{
    public int TurnNumber { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class PlotDirective
{
    public string Pacing { get; set; } = "normal";
    public List<string> SpotlightNpcs { get; set; } = new();
    public string? SuggestedBeat { get; set; }
}

public class GuardDecision
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GuardStatus Status { get; set; } = GuardStatus.Valid;
    public string? Narrative { get; set; }
}

public enum GuardStatus
{
    Valid,
    Reject
}

public class DomainResult
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DomainStatus Status { get; set; } = DomainStatus.Completed;
    public StateDelta? StateDelta { get; set; }
    public string FinalNarrative { get; set; } = string.Empty;
}

public enum DomainStatus
{
    Completed,
    Error
}

public class PendingAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RequestedBy { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
}

public class StateDelta
{
    public List<ProposedEvent> NewEvents { get; set; } = new();
    public string TurnId { get; set; } = Guid.NewGuid().ToString();
}

public class ProposedEvent
{
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

