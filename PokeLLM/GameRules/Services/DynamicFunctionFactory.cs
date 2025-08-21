using Microsoft.SemanticKernel;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Text.Json;

namespace PokeLLM.GameRules.Services;

public class DynamicFunctionFactory : IDynamicFunctionFactory
{
    private readonly IJavaScriptRuleEngine _ruleEngine;

    public DynamicFunctionFactory(IJavaScriptRuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    public async Task<IEnumerable<KernelFunction>> GenerateFunctionsFromRulesetAsync(JsonDocument ruleset, GamePhase phase)
    {
        var functionDefinitions = await GetFunctionsForPhaseAsync(ruleset, phase);
        var functions = new List<KernelFunction>();

        foreach (var definition in functionDefinitions)
        {
            var function = await CreateRulesetFunctionAsync(definition);
            functions.Add(function);
        }

        return functions;
    }

    public async Task<KernelFunction> CreateRulesetFunctionAsync(FunctionDefinition definition)
    {
        await Task.Delay(0); // Make async

        // Create parameter metadata
        var parameters = definition.Parameters.Select(p => new KernelParameterMetadata(p.Name)
        {
            Description = p.Description,
            IsRequired = p.Required,
            ParameterType = GetParameterType(p.Type)
        }).ToList();

        // Create the function delegate
        Func<KernelArguments, Task<string>> functionDelegate = async (args) =>
        {
            try
            {
                // Validate all rule validations
                foreach (var validation in definition.RuleValidations)
                {
                    var isValid = await _ruleEngine.ValidateRuleAsync(validation, args["character"], args);
                    if (!isValid)
                    {
                        return $"Rule validation failed: {validation}";
                    }
                }

                // Apply effects (this would be implemented based on the specific game system)
                var appliedEffects = new List<string>();
                foreach (var effect in definition.Effects)
                {
                    appliedEffects.Add($"Applied effect: {effect.Operation} {effect.Value} to {effect.Target}");
                }

                return $"Function {definition.Name} executed successfully. Effects: {string.Join(", ", appliedEffects)}";
            }
            catch (Exception ex)
            {
                return $"Function execution failed: {ex.Message}";
            }
        };

        // Create the kernel function
        return KernelFunctionFactory.CreateFromMethod(
            functionDelegate,
            functionName: definition.Name,
            description: definition.Description,
            parameters: parameters
        );
    }

    public async Task<List<FunctionDefinition>> GetFunctionsForPhaseAsync(JsonDocument ruleset, GamePhase phase)
    {
        await Task.Delay(0); // Make async

        try
        {
            var root = ruleset.RootElement;
            if (!root.TryGetProperty("functionDefinitions", out var functionDefinitions))
                return new List<FunctionDefinition>();

            var phaseKey = phase.ToString();
            if (!functionDefinitions.TryGetProperty(phaseKey, out var phaseFunctions))
                return new List<FunctionDefinition>();

            var functions = new List<FunctionDefinition>();
            
            foreach (var functionElement in phaseFunctions.EnumerateArray())
            {
                var function = JsonSerializer.Deserialize<FunctionDefinition>(functionElement.GetRawText());
                if (function != null)
                {
                    functions.Add(function);
                }
            }

            return functions;
        }
        catch
        {
            return new List<FunctionDefinition>();
        }
    }

    private Type GetParameterType(string typeName)
    {
        return typeName.ToLower() switch
        {
            "string" => typeof(string),
            "int" => typeof(int),
            "bool" => typeof(bool),
            "double" => typeof(double),
            "float" => typeof(float),
            _ => typeof(string)
        };
    }
}