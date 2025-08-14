using Microsoft.ClearScript.V8;
using PokeLLM.GameRules.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PokeLLM.GameRules.Services;

public class JavaScriptRuleEngine : IJavaScriptRuleEngine
{
    private readonly HashSet<string> _allowedFunctions = new()
    {
        "Math.floor", "Math.ceil", "Math.max", "Math.min", "Math.abs",
        "console.log", "JSON.stringify", "JSON.parse"
    };

    private readonly HashSet<string> _forbiddenKeywords = new()
    {
        "require", "import", "eval", "Function", "setTimeout", "setInterval",
        "process", "global", "__dirname", "__filename", "module", "exports"
    };

    public async Task<bool> ValidateRuleAsync(string ruleScript, object character, object context)
    {
        try
        {
            // First check if the script is safe
            if (!await IsSafeScriptAsync(ruleScript))
            {
                return false;
            }

            // Try to execute the script in a safe environment
            var variables = new Dictionary<string, object>
            {
                ["character"] = character,
                ["context"] = context
            };

            var result = await ExecuteAsync(ruleScript, variables);
            return result is bool boolResult ? boolResult : Convert.ToBoolean(result);
        }
        catch
        {
            return false;
        }
    }

    public async Task<T> ExecuteRuleAsync<T>(string ruleScript, object character, object context)
    {
        if (!await IsSafeScriptAsync(ruleScript))
        {
            throw new InvalidOperationException("Script contains unsafe operations");
        }

        var variables = new Dictionary<string, object>
        {
            ["character"] = character,
            ["context"] = context
        };

        var result = await ExecuteAsync(ruleScript, variables);
        
        if (result is T typedResult)
        {
            return typedResult;
        }

        // Try to convert the result to the requested type
        try
        {
            return (T)Convert.ChangeType(result, typeof(T));
        }
        catch
        {
            return default(T);
        }
    }

    public async Task<bool> IsSafeScriptAsync(string script)
    {
        await Task.Delay(0); // Make async

        // Check for forbidden keywords
        foreach (var keyword in _forbiddenKeywords)
        {
            if (script.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check for potentially dangerous patterns
        var dangerousPatterns = new[]
        {
            @"new\s+Function",
            @"eval\s*\(",
            @"window\.",
            @"document\.",
            @"location\.",
            @"navigator\.",
            @"XMLHttpRequest",
            @"fetch\s*\(",
            @"import\s*\(",
            @"require\s*\("
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (Regex.IsMatch(script, pattern, RegexOptions.IgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<object> ExecuteAsync(string script, Dictionary<string, object> variables)
    {
        try
        {
            await Task.Delay(0); // Make async
            
            using var engine = new V8ScriptEngine();
            
            // Add utility functions
            engine.AddHostObject("dice", new DiceUtilities());
            engine.AddHostObject("utils", new RuleUtilities());
            
            // Add variables with proper property exposure
            foreach (var kvp in variables)
            {
                if (kvp.Key == "character")
                {
                    // For character objects, expose properties in a JavaScript-friendly way
                    var characterObj = kvp.Value;
                    var characterType = characterObj.GetType();
                    var jsObject = new Dictionary<string, object>();
                    
                    foreach (var prop in characterType.GetProperties())
                    {
                        var value = prop.GetValue(characterObj);
                        // Add both Pascal and camel case versions
                        jsObject[prop.Name] = value; // PascalCase (Race)
                        jsObject[char.ToLower(prop.Name[0]) + prop.Name.Substring(1)] = value; // camelCase (race)
                    }
                    
                    engine.AddHostObject(kvp.Key, jsObject);
                }
                else
                {
                    engine.AddHostObject(kvp.Key, kvp.Value);
                }
            }

            // Execute the script
            var result = engine.Evaluate(script);
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Script execution failed: {ex.Message}", ex);
        }
    }
}

public class DiceUtilities
{
    private readonly Random _random = new();

    public int Roll(int sides) => _random.Next(1, sides + 1);
    public int Roll(int count, int sides) => Enumerable.Range(0, count).Sum(_ => Roll(sides));
    public int D4() => Roll(4);
    public int D6() => Roll(6);
    public int D8() => Roll(8);
    public int D10() => Roll(10);
    public int D12() => Roll(12);
    public int D20() => Roll(20);
    public int D100() => Roll(100);
}

public class RuleUtilities
{
    public int GetAbilityModifier(int abilityScore) => (abilityScore - 10) / 2;
    public int GetProficiencyBonus(int level) => (level - 1) / 4 + 2;
    public bool IsValidRange(int value, int min, int max) => value >= min && value <= max;
}