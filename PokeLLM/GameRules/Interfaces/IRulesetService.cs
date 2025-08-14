using System.Text.Json;

namespace PokeLLM.GameRules.Interfaces;

public interface IRulesetService
{
    Task<JsonDocument> LoadRulesetAsync(string rulesetPath);
    Task<bool> ValidateRulesetAsync(JsonDocument ruleset);
    Task<RulesetMetadata> GetRulesetMetadataAsync(JsonDocument ruleset);
}

public class RulesetMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}