using PokeLLM.Game.Plugins;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState;

namespace PokeLLM.Tests.TestUtilities;

/// <summary>
/// Testable version of RulesetManagementPlugin that allows overriding the rulesets directory for testing
/// </summary>
public class TestableRulesetManagementPlugin : RulesetManagementPlugin
{
    public TestableRulesetManagementPlugin(
        IRulesetManager rulesetManager,
        IJavaScriptRuleEngine jsEngine,
        IGameStateRepository gameStateRepo,
        string rulesetsDirectory) : base(rulesetManager, jsEngine, gameStateRepo)
    {
        // Use reflection to set the private _rulesetsDirectory field
        var field = typeof(RulesetManagementPlugin).GetField("_rulesetsDirectory", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(this, rulesetsDirectory);
    }
}