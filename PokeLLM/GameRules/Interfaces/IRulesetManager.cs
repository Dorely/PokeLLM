using System.Text.Json;
using PokeLLM.GameState.Models;
using Microsoft.SemanticKernel;

namespace PokeLLM.GameRules.Interfaces;

/// <summary>
/// Manages loading and applying rulesets to game state and plugin functionality
/// </summary>
public interface IRulesetManager : IDisposable
{
    /// <summary>
    /// Get all available rulesets with their metadata
    /// </summary>
    Task<List<RulesetInfo>> GetAvailableRulesetsAsync();
    
    /// <summary>
    /// Load a ruleset by ID and cache it
    /// </summary>
    Task<JsonDocument> LoadRulesetAsync(string rulesetId);
    
    /// <summary>
    /// Get the currently active ruleset
    /// </summary>
    JsonDocument? GetActiveRuleset();
    
    /// <summary>
    /// Set the active ruleset for the game
    /// </summary>
    Task SetActiveRulesetAsync(string rulesetId);
    
    /// <summary>
    /// Initialize game state schema based on active ruleset
    /// </summary>
    void InitializeGameStateFromRuleset(GameStateModel gameState, JsonDocument ruleset);
    
    /// <summary>
    /// Validate that game state matches the expected schema for the active ruleset
    /// </summary>
    bool ValidateGameStateSchema(GameStateModel gameState, JsonDocument ruleset);
    
    /// <summary>
    /// Get functions for a specific game phase from the active ruleset
    /// </summary>
    Task<IEnumerable<KernelFunction>> GetPhaseFunctionsAsync(GamePhase phase);
    
    /// <summary>
    /// Get prompt template for a specific phase from the active ruleset
    /// </summary>
    string GetPhasePromptTemplate(GamePhase phase);
    
    /// <summary>
    /// Get context elements for a specific phase from the active ruleset
    /// </summary>
    List<string> GetPhaseContextElements(GamePhase phase);
}

/// <summary>
/// Information about an available ruleset
/// </summary>
public class RulesetInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}