using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

/// <summary>
/// Lightweight view of a stored adventure session file used for selection menus.
/// </summary>
public class AdventureSessionSummary
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("sessionName")]
    public string SessionName { get; set; } = string.Empty;

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("moduleTitle")]
    public string ModuleTitle { get; set; } = string.Empty;

    [JsonPropertyName("moduleFileName")]
    public string ModuleFileName { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdatedTime")]
    public DateTime LastUpdatedTime { get; set; }

    [JsonPropertyName("currentPhase")]
    public GamePhase CurrentPhase { get; set; }

    [JsonPropertyName("isSetupComplete")]
    public bool IsSetupComplete { get; set; }

    [JsonIgnore]
    public string FilePath { get; set; } = string.Empty;
}
