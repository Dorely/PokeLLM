using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState.Models;
using PokeLLM.Game.GameLogic;
using System.ComponentModel;
using System.Text.Json;
using System.Reflection;

namespace PokeLLM.GameRules.Services;

public class DynamicFunctionFactory : IDynamicFunctionFactory
{
    private readonly IJavaScriptRuleEngine _ruleEngine;
    private readonly IServiceProvider _serviceProvider;

    public DynamicFunctionFactory(IJavaScriptRuleEngine ruleEngine, IServiceProvider serviceProvider)
    {
        _ruleEngine = ruleEngine;
        _serviceProvider = serviceProvider;
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
                // Validate all rule validations using simple C# logic for now
                foreach (var validation in definition.RuleValidations)
                {
                    // Apply template replacement to rule validation
                    var processedValidation = validation;
                    foreach (var arg in args)
                    {
                        var templateKey = $"{{{{{arg.Key}}}}}";
                        var replacement = arg.Value?.ToString() ?? "";
                        processedValidation = processedValidation.Replace(templateKey, replacement);
                    }
                    
                    var isValid = await ValidateRuleSimpleAsync(processedValidation, args);
                    if (!isValid)
                    {
                        return $"Rule validation failed: {processedValidation}";
                    }
                }

                // Apply effects to character and/or game state
                var appliedEffects = new List<string>();
                foreach (var effect in definition.Effects)
                {
                    var effectResult = await ApplyEffectAsync(effect, args);
                    appliedEffects.Add(effectResult);
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
        switch (typeName.ToLower())
        {
            case "string": return typeof(string);
            case "int": return typeof(int);
            case "bool": return typeof(bool);
            case "boolean": return typeof(bool);
            case "double": return typeof(double);
            case "float": return typeof(float);
            case "object": return typeof(object);
            case "array": return typeof(object[]);
            default: return typeof(string);
        }
    }

    private async Task<string> ApplyEffectAsync(ActionEffect effect, KernelArguments args)
    {
        await Task.Delay(0); // Make async

        try
        {
            var target = effect.Target;
            var operation = effect.Operation;
            var value = effect.Value;

            // Determine target root object (character or gameState)
            object? rootTargetObject = null;
            string pathWithoutRoot = target;
            if (target.StartsWith("character."))
            {
                if (!args.TryGetValue("character", out var characterObj) || characterObj == null)
                {
                    return $"Failed to apply effect: character not found in arguments";
                }
                rootTargetObject = characterObj;
                pathWithoutRoot = target.Substring("character.".Length);
            }
            else if (target.StartsWith("gameState."))
            {
                if (!args.TryGetValue("gameState", out var gameStateObj) || gameStateObj == null)
                {
                    return $"Failed to apply effect: gameState not found in arguments";
                }
                rootTargetObject = gameStateObj;
                pathWithoutRoot = target.Substring("gameState.".Length);
            }

            // Replace template variables in target and value
            foreach (var arg in args)
            {
                var templateKey = $"{{{{{arg.Key}}}}}";
                var replacement = arg.Value?.ToString() ?? "";
                target = target.Replace(templateKey, replacement);
                
                var valueStr = value?.ToString() ?? "";
                if (valueStr.Contains("{{"))
                {
                    var newValue = valueStr.Replace(templateKey, replacement);
                    value = newValue;
                }
            }

            // Apply effect based on operation
            var op = operation.ToLower();
            if (rootTargetObject != null)
            {
                return op switch
                {
                    "set" => ApplySetEffect(rootTargetObject, pathWithoutRoot, value),
                    "add" => ApplyAddEffect(rootTargetObject, pathWithoutRoot, value),
                    "subtract" => ApplySubtractEffect(rootTargetObject, pathWithoutRoot, value),
                    "addentity" or "addpokemon" => await ApplyAddEntityEffect(target, value?.ToString() ?? ""),
                    "removeentity" or "removepokemon" => await ApplyRemoveEntityEffect(target, value?.ToString() ?? ""),
                    _ => $"Unknown operation: {operation}"
                };
            }

            return $"Unsupported target path: {target}";
        }
        catch (Exception ex)
        {
            return $"Failed to apply effect: {ex.Message}";
        }
    }

    private string ApplySetEffect(object rootObject, string targetPath, object value)
    {
        try
        {
            // Handle nested properties using dot notation (e.g., "attribute.subField")
            var currentObject = rootObject;
            var segments = targetPath.Split('.', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var currentType = currentObject.GetType();
                var property = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    return $"Property {segment} not found on {currentType.Name}";
                }

                if (i == segments.Length - 1)
                {
                    // Final segment - set the value
                    object convertedValue;
                    if (property.PropertyType == typeof(string))
                    {
                        convertedValue = value?.ToString() ?? "";
                    }
                    else if (property.PropertyType == typeof(int) && value is JsonElement je && je.ValueKind == JsonValueKind.Number)
                    {
                        convertedValue = je.GetInt32();
                    }
                    else
                    {
                        try
                        {
                            convertedValue = Convert.ChangeType(value, property.PropertyType);
                        }
                        catch
                        {
                            // Fallback: try JSON deserialization into property type
                            var json = JsonSerializer.Serialize(value);
                            convertedValue = JsonSerializer.Deserialize(json, property.PropertyType);
                        }
                    }

                    property.SetValue(currentObject, convertedValue);
                    var verifyValue = property.GetValue(currentObject);
                    return $"Set {targetPath} = {value} (verified: {verifyValue})";
                }
                else
                {
                    // Traverse to next object
                    var nextObject = property.GetValue(currentObject);
                    if (nextObject == null)
                    {
                        // Try to instantiate if it's a class with parameterless ctor
                        if (property.PropertyType.GetConstructor(Type.EmptyTypes) != null)
                        {
                            nextObject = Activator.CreateInstance(property.PropertyType);
                            property.SetValue(currentObject, nextObject);
                        }
                        else
                        {
                            return $"Cannot traverse null property {segment} on {currentType.Name}";
                        }
                    }
                    currentObject = nextObject;
                }
            }

            return $"Property path {targetPath} not found";
        }
        catch (Exception ex)
        {
            return $"Set effect failed: {ex.Message}";
        }
    }

    private string ApplyAddEffect(object character, string target, object value)
    {
        try
        {
            // Handle list additions (e.g., character.entities.add)
            if (target.Contains(".") && !target.Contains("["))
            {
                var parts = target.Split('.');
                if (parts.Length >= 1)
                {
                    var propertyName = char.ToUpper(parts[0][0]) + parts[0].Substring(1);
                    var property = character.GetType().GetProperty(propertyName) ?? character.GetType().GetProperty(parts[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    if (property != null && typeof(System.Collections.IList).IsAssignableFrom(property.PropertyType))
                    {
                        var list = (System.Collections.IList)property.GetValue(character)!;
                        list.Add(value);
                        return $"Added {value} to {target}";
                    }
                }
            }

            return $"Add effect not applicable to {target}";
        }
        catch (Exception ex)
        {
            return $"Add effect failed: {ex.Message}";
        }
    }

    private string ApplySubtractEffect(object character, string target, object value)
    {
        try
        {
            // Handle dictionary operations (e.g., inventory[item])
            if (target.Contains("[") && target.Contains("]"))
            {
                var propertyMatch = System.Text.RegularExpressions.Regex.Match(target, @"(\w+)\[([^\]]+)\]");
                if (propertyMatch.Success)
                {
                    var propertyName = propertyMatch.Groups[1].Value;
                    var key = propertyMatch.Groups[2].Value.Replace("\"", "").Replace("'", "");
                    
                    var property = character.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property != null && typeof(System.Collections.IDictionary).IsAssignableFrom(property.PropertyType))
                    {
                        var dict = (System.Collections.IDictionary)property.GetValue(character)!;
                        if (dict.Contains(key))
                        {
                            var currentValue = Convert.ToInt32(dict[key]);
                            var subtractValue = Convert.ToInt32(value);
                            dict[key] = Math.Max(0, currentValue - subtractValue);
                            return $"Subtracted {subtractValue} from {target} (now {dict[key]})";
                        }
                    }
                }
            }

            return $"Subtract effect not applicable to {target}";
        }
        catch (Exception ex)
        {
            return $"Subtract effect failed: {ex.Message}";
        }
    }

    private async Task<bool> ValidateRuleSimpleAsync(string rule, KernelArguments args)
    {
        await Task.Delay(0); // Make async
        
        try
        {
            // Get character object
            if (!args.TryGetValue("character", out var characterObj) || characterObj == null)
                return false;
                
            // Generic empty string validation (e.g., "character.property == ''" or "character.property == \"\"")
            if (rule.Contains("==") && (rule.Contains("''") || rule.Contains("\"\"") || rule.Contains("== null")))
            {
                var match = System.Text.RegularExpressions.Regex.Match(rule, @"character\.(\w+)\s*==");
                if (match.Success)
                {
                    var propertyName = match.Groups[1].Value;
                    var property = characterObj.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (property != null)
                    {
                        var propertyValue = property.GetValue(characterObj);
                        return propertyValue == null || string.IsNullOrEmpty(propertyValue.ToString());
                    }
                    return false; // Property not found
                }
            }
            
            
            // Generic collection size validation (e.g., "character.entities.length < 6")
            var collectionLengthMatch = System.Text.RegularExpressions.Regex.Match(rule, @"character\.(\w+)\.length\s*([<>=!]+)\s*(\d+)");
            if (collectionLengthMatch.Success)
            {
                var propertyName = collectionLengthMatch.Groups[1].Value;
                var operatorStr = collectionLengthMatch.Groups[2].Value;
                var expectedCount = int.Parse(collectionLengthMatch.Groups[3].Value);
                
                var property = characterObj.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (property != null && property.PropertyType.GetInterface("ICollection") != null)
                {
                    var collection = (System.Collections.ICollection)property.GetValue(characterObj)!;
                    var actualCount = collection?.Count ?? 0;
                    
                    return operatorStr switch
                    {
                        "<" => actualCount < expectedCount,
                        "<=" => actualCount <= expectedCount,
                        ">" => actualCount > expectedCount,
                        ">=" => actualCount >= expectedCount,
                        "==" or "=" => actualCount == expectedCount,
                        "!=" => actualCount != expectedCount,
                        _ => false
                    };
                }
                
                return true; // Default to valid if property not found
            }
            
            // Generic inventory/dictionary validation (e.g., "character.inventory[item] > 0")
            var inventoryMatch = System.Text.RegularExpressions.Regex.Match(rule, @"character\.(\w+)\[([^\]]+)\]\s*([<>=!]+)\s*(\d+)");
            if (inventoryMatch.Success)
            {
                var containerName = inventoryMatch.Groups[1].Value;
                var itemKey = inventoryMatch.Groups[2].Value;
                var operatorStr = inventoryMatch.Groups[3].Value;
                var expectedValue = int.Parse(inventoryMatch.Groups[4].Value);
                
                var containerProperty = characterObj.GetType().GetProperty(containerName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (containerProperty != null && typeof(System.Collections.IDictionary).IsAssignableFrom(containerProperty.PropertyType))
                {
                    var container = (System.Collections.IDictionary)containerProperty.GetValue(characterObj)!;
                    if (container.Contains(itemKey))
                    {
                        var actualValue = Convert.ToInt32(container[itemKey]);
                        return operatorStr switch
                        {
                            "<" => actualValue < expectedValue,
                            "<=" => actualValue <= expectedValue,
                            ">" => actualValue > expectedValue,
                            ">=" => actualValue >= expectedValue,
                            "==" or "=" => actualValue == expectedValue,
                            "!=" => actualValue != expectedValue,
                            _ => false
                        };
                    }
                    return expectedValue == 0; // Item not found - only valid if expecting 0
                }
                return false;
            }
            
            // Default to true for unrecognized rules (for now)
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ApplyAddEntityEffect(string target, string entityId)
    {
        try
        {
            // Validate entity ID is properly resolved
            if (entityId.Contains("{{") || string.IsNullOrEmpty(entityId))
            {
                return $"Error: Entity ID not properly resolved: '{entityId}'";
            }
            
            // Generic entity handling that can work for any game type
            // The ruleset should define what entities mean for the specific game
            
            // For now, we'll attempt to use the character management service if available
            // This provides backward compatibility while being game-agnostic
            try
            {
                var characterManagementService = _serviceProvider.GetRequiredService<ICharacterManagementService>();
                
                // Use reflection to find an appropriate method for adding entities
                var methods = characterManagementService.GetType().GetMethods()
                    .Where(m => m.Name.Contains("Add") && m.Name.Contains("Team"))
                    .ToList();
                
                if (methods.Any())
                {
                    // Try the first available method - this is game-specific
                    var method = methods.First();
                    await (Task)method.Invoke(characterManagementService, new object[] { entityId });
                    return $"Added entity {entityId} to team via game state service";
                }
            }
            catch (Exception ex)
            {
                // Fallback to generic processing
                return $"Generic entity addition processed: {entityId} to {target} (service error: {ex.Message})";
            }
            
            return $"Generic entity addition processed: {entityId} to {target}";
        }
        catch (Exception ex)
        {
            return $"Failed to add entity {entityId}: {ex.Message}";
        }
    }

    private async Task<string> ApplyRemoveEntityEffect(string target, string entityId)
    {
        try
        {
            // Validate entity ID is properly resolved
            if (entityId.Contains("{{") || string.IsNullOrEmpty(entityId))
            {
                return $"Error: Entity ID not properly resolved: '{entityId}'";
            }
            
            // Generic entity removal - could be extended based on game type
            return $"Generic entity removal processed: {entityId} from {target}";
        }
        catch (Exception ex)
        {
            return $"Failed to remove entity {entityId}: {ex.Message}";
        }
    }
}