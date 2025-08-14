using Microsoft.SemanticKernel;
using PokeLLM.GameState.Models;
using System.Text.Json;

namespace PokeLLM.GameRules.Interfaces;

public interface IDynamicFunctionFactory
{
    Task<IEnumerable<KernelFunction>> GenerateFunctionsFromRulesetAsync(JsonDocument ruleset, GamePhase phase);
    Task<KernelFunction> CreateRulesetFunctionAsync(FunctionDefinition definition);
    Task<List<FunctionDefinition>> GetFunctionsForPhaseAsync(JsonDocument ruleset, GamePhase phase);
}

public class RulesetActionRequest
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string> RuleValidations { get; set; } = new();
    public List<ActionEffect> Effects { get; set; } = new();
    public GameContext Context { get; set; } = new();
}

public class ActionEffect
{
    public string Target { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public object Value { get; set; } = new();
    public string Source { get; set; } = string.Empty;
    public List<string> Conditions { get; set; } = new();
}

public class GameContext
{
    public GameStateModel GameState { get; set; } = new();
    public object Character { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public JsonDocument Ruleset { get; set; } = JsonDocument.Parse("{}");
}

public class ActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> AppliedEffects { get; set; } = new();
    public GameStateModel GameState { get; set; } = new();
    
    public static ActionResult SuccessResult(string message, List<string> appliedEffects = null)
    {
        return new ActionResult 
        { 
            Success = true, 
            Message = message, 
            AppliedEffects = appliedEffects ?? new List<string>() 
        };
    }
    
    public static ActionResult Failure(string message)
    {
        return new ActionResult { Success = false, Message = message };
    }
}