using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

/// <summary>
/// Lightweight description of a stored adventure module used for selection menus.
/// </summary>
public class AdventureModuleSummary
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("isSetupComplete")]
    public bool IsSetupComplete { get; set; }

    [JsonPropertyName("lastModifiedUtc")]
    public DateTime LastModifiedUtc { get; set; }

    [JsonIgnore]
    public string FilePath { get; set; } = string.Empty;
}
