namespace PokeLLM.GameRules.Interfaces;

public interface IJavaScriptRuleEngine
{
    Task<bool> ValidateRuleAsync(string ruleScript, object character, object context);
    Task<T> ExecuteRuleAsync<T>(string ruleScript, object character, object context);
    Task<bool> IsSafeScriptAsync(string script);
    Task<object> ExecuteAsync(string script, Dictionary<string, object> variables);
}

public class RuleValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public object Result { get; set; }
}