using System.Text.Json.Serialization;

namespace PokeLLM.GameRules.Interfaces;

public class FunctionDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<ParameterDefinition> Parameters { get; set; } = new();

    [JsonPropertyName("ruleValidations")]
    public List<string> RuleValidations { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<ActionEffect> Effects { get; set; } = new();
}

public class ParameterDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; } = false;
}