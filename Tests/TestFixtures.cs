using System.Text.Json;
using PokeLLM.GameState.Models;

namespace PokeLLM.Tests;

/// <summary>
/// Common test fixtures and helper methods for ruleset testing
/// </summary>
public static class TestFixtures
{
    /// <summary>
    /// Convert a Dictionary<string, object> to the expected Dictionary<string, JsonElement> format
    /// </summary>
    public static Dictionary<string, JsonElement> ConvertToJsonElementDictionary(Dictionary<string, object> data)
    {
        var result = new Dictionary<string, JsonElement>();
        
        foreach (var kvp in data)
        {
            result[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value);
        }
        
        return result;
    }

    /// <summary>
    /// Create a basic test game state with the specified ruleset
    /// </summary>
    public static GameStateModel CreateTestGameState(string rulesetId)
    {
        return new GameStateModel
        {
            ActiveRulesetId = rulesetId,
            RulesetGameData = new Dictionary<string, JsonElement>(),
            CurrentPhase = GamePhase.GameSetup
        };
    }

    /// <summary>
    /// Get a simple string value from the JsonElement
    /// </summary>
    public static string GetStringValue(JsonElement element)
    {
        return element.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Get an integer value from the JsonElement
    /// </summary>
    public static int GetIntValue(JsonElement element)
    {
        return element.GetInt32();
    }

    /// <summary>
    /// Set a simple string value as JsonElement
    /// </summary>
    public static JsonElement CreateStringValue(string value)
    {
        return JsonSerializer.SerializeToElement(value);
    }

    /// <summary>
    /// Set a simple integer value as JsonElement
    /// </summary>
    public static JsonElement CreateIntValue(int value)
    {
        return JsonSerializer.SerializeToElement(value);
    }
}