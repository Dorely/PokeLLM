using PokeLLM.GameRules.Interfaces;
using System.Text.Json;

namespace PokeLLM.GameRules.Services;

public class RulesetService : IRulesetService
{
    public async Task<JsonDocument> LoadRulesetAsync(string rulesetPath)
    {
        if (!File.Exists(rulesetPath))
        {
            throw new FileNotFoundException($"Ruleset file not found: {rulesetPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(rulesetPath);
        return JsonDocument.Parse(jsonContent);
    }

    public async Task<bool> ValidateRulesetAsync(JsonDocument ruleset)
    {
        await Task.Delay(0); // Make async

        try
        {
            // Check for required top-level properties
            var root = ruleset.RootElement;
            
            if (!root.TryGetProperty("metadata", out var metadata))
                return false;
                
            if (!metadata.TryGetProperty("id", out _))
                return false;
                
            if (!metadata.TryGetProperty("name", out _))
                return false;

            // Additional validation can be added here
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<RulesetMetadata> GetRulesetMetadataAsync(JsonDocument ruleset)
    {
        await Task.Delay(0); // Make async

        try
        {
            var root = ruleset.RootElement;
            var metadata = root.GetProperty("metadata");

            return new RulesetMetadata
            {
                Id = metadata.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Name = metadata.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                Version = metadata.TryGetProperty("version", out var version) ? version.GetString() ?? string.Empty : string.Empty,
                Description = metadata.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
                Authors = metadata.TryGetProperty("authors", out var authors) 
                    ? authors.EnumerateArray().Select(a => a.GetString() ?? string.Empty).ToList() 
                    : new List<string>(),
                Tags = metadata.TryGetProperty("tags", out var tags) 
                    ? tags.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToList() 
                    : new List<string>()
            };
        }
        catch
        {
            return new RulesetMetadata();
        }
    }
}