using System.Text.Json;
using PokeLLM.GameRules.Interfaces;

namespace PokeLLM.GameLogic.Services;

/// <summary>
/// Service for validating rulesets against the expected schema
/// </summary>
public interface IRulesetSchemaValidator
{
    /// <summary>
    /// Validate a ruleset against the expected schema
    /// </summary>
    Task<RulesetValidationResult> ValidateRulesetAsync(JsonDocument ruleset);
    
    /// <summary>
    /// Validate a specific section of a ruleset
    /// </summary>
    Task<RulesetValidationResult> ValidateSectionAsync(JsonDocument ruleset, string sectionName);
    
    /// <summary>
    /// Get schema requirements for a specific section
    /// </summary>
    List<string> GetSectionRequirements(string sectionName);
}

/// <summary>
/// Result of ruleset validation
/// </summary>
public class RulesetValidationResult
{
    public bool IsValid => !HasErrors;
    public bool HasErrors => Errors.Any();
    public bool HasWarnings => Warnings.Any();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string Summary => IsValid ? "Validation passed" : $"{Errors.Count} errors, {Warnings.Count} warnings";
}

/// <summary>
/// Implementation of ruleset schema validator
/// </summary>
public class RulesetSchemaValidator : IRulesetSchemaValidator
{
    private readonly IRulesetManager _rulesetManager;

    public RulesetSchemaValidator(IRulesetManager rulesetManager)
    {
        _rulesetManager = rulesetManager;
    }

    public async Task<RulesetValidationResult> ValidateRulesetAsync(JsonDocument ruleset)
    {
        var result = new RulesetValidationResult();
        var root = ruleset.RootElement;

        // Validate metadata section
        ValidateMetadata(root, result);
        
        // Validate game state schema
        ValidateGameStateSchema(root, result);
        
        // Validate function definitions
        ValidateFunctionDefinitions(root, result);
        
        // Validate prompt templates
        ValidatePromptTemplates(root, result);
        
        // Validate overall structure consistency
        ValidateOverallConsistency(root, result);

        return result;
    }

    public async Task<RulesetValidationResult> ValidateSectionAsync(JsonDocument ruleset, string sectionName)
    {
        var result = new RulesetValidationResult();
        var root = ruleset.RootElement;

        switch (sectionName.ToLower())
        {
            case "metadata":
                ValidateMetadata(root, result);
                break;
            case "gamestateschema":
                ValidateGameStateSchema(root, result);
                break;
            case "functiondefinitions":
                ValidateFunctionDefinitions(root, result);
                break;
            case "prompttemplates":
                ValidatePromptTemplates(root, result);
                break;
            default:
                result.Errors.Add($"Unknown section: {sectionName}");
                break;
        }

        return result;
    }

    public List<string> GetSectionRequirements(string sectionName)
    {
        return sectionName.ToLower() switch
        {
            "metadata" => new List<string>
            {
                "id - Unique identifier for the ruleset",
                "name - Human-readable name",
                "version - Semantic version (e.g., 1.0.0)",
                "description - Description of the game",
                "authors - Array of author names",
                "tags - Array of descriptive tags"
            },
            "gamestateschema" => new List<string>
            {
                "requiredCollections - Array of collection names needed for game state",
                "playerFields - Array of fields the player object must have",
                "dynamicCollections - Object mapping collection names to entity types"
            },
            "functiondefinitions" => new List<string>
            {
                "GameSetup - Functions for character creation and game initialization",
                "WorldGeneration - Functions for creating locations and NPCs",
                "Exploration - Functions for main gameplay interactions",
                "Combat - Functions for battle mechanics (if applicable)",
                "LevelUp - Functions for character progression"
            },
            "prompttemplates" => new List<string>
            {
                "GameSetup - System prompt for character creation phase",
                "WorldGeneration - System prompt for world building phase",
                "Exploration - System prompt for main gameplay phase",
                "Combat - System prompt for combat phase",
                "LevelUp - System prompt for progression phase"
            },
            _ => new List<string> { $"Unknown section: {sectionName}" }
        };
    }

    private void ValidateMetadata(JsonElement root, RulesetValidationResult result)
    {
        if (!root.TryGetProperty("metadata", out var metadata))
        {
            result.Errors.Add("Missing required 'metadata' section");
            return;
        }

        // Required fields
        if (!metadata.TryGetProperty("id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
        {
            result.Errors.Add("Metadata missing required 'id' field");
        }
        else
        {
            var idValue = id.GetString();
            if (idValue.Contains(' ') || idValue.Contains('\t'))
            {
                result.Errors.Add("Metadata 'id' cannot contain spaces or tabs");
            }
            if (idValue != idValue.ToLowerInvariant())
            {
                result.Warnings.Add("Metadata 'id' should be lowercase for consistency");
            }
        }

        if (!metadata.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString()))
        {
            result.Errors.Add("Metadata missing required 'name' field");
        }

        if (!metadata.TryGetProperty("version", out var version) || string.IsNullOrWhiteSpace(version.GetString()))
        {
            result.Warnings.Add("Metadata missing 'version' field - defaulting to 1.0.0");
        }
        else
        {
            var versionString = version.GetString();
            if (!IsValidSemanticVersion(versionString))
            {
                result.Warnings.Add($"Version '{versionString}' is not a valid semantic version (e.g., 1.0.0)");
            }
        }

        if (!metadata.TryGetProperty("description", out var description) || string.IsNullOrWhiteSpace(description.GetString()))
        {
            result.Warnings.Add("Metadata missing 'description' field - consider adding a description");
        }

        if (!metadata.TryGetProperty("authors", out var authors) || authors.ValueKind != JsonValueKind.Array)
        {
            result.Warnings.Add("Metadata missing 'authors' array - consider crediting authors");
        }

        if (!metadata.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            result.Warnings.Add("Metadata missing 'tags' array - tags help with discoverability");
        }
    }

    private void ValidateGameStateSchema(JsonElement root, RulesetValidationResult result)
    {
        if (!root.TryGetProperty("gameStateSchema", out var schema))
        {
            result.Errors.Add("Missing required 'gameStateSchema' section");
            return;
        }

        // Required collections
        if (!schema.TryGetProperty("requiredCollections", out var collections) || collections.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add("gameStateSchema missing required 'requiredCollections' array");
        }
        else if (collections.GetArrayLength() == 0)
        {
            result.Warnings.Add("gameStateSchema has empty 'requiredCollections' - most games need at least one collection");
        }

        // Player fields
        if (!schema.TryGetProperty("playerFields", out var playerFields) || playerFields.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add("gameStateSchema missing required 'playerFields' array");
        }
        else if (playerFields.GetArrayLength() == 0)
        {
            result.Warnings.Add("gameStateSchema has empty 'playerFields' - most games need player data");
        }

        // Dynamic collections
        if (!schema.TryGetProperty("dynamicCollections", out var dynamicCollections) || dynamicCollections.ValueKind != JsonValueKind.Object)
        {
            result.Warnings.Add("gameStateSchema missing 'dynamicCollections' object - consider defining entity types");
        }
        else
        {
            // Validate that dynamic collections reference required collections
            var requiredCollectionNames = new HashSet<string>();
            if (collections.ValueKind == JsonValueKind.Array)
            {
                foreach (var collection in collections.EnumerateArray())
                {
                    var collectionName = collection.GetString();
                    if (!string.IsNullOrWhiteSpace(collectionName))
                    {
                        requiredCollectionNames.Add(collectionName);
                    }
                }
            }

            foreach (var dynamicCollection in dynamicCollections.EnumerateObject())
            {
                if (!requiredCollectionNames.Contains(dynamicCollection.Name))
                {
                    result.Warnings.Add($"Dynamic collection '{dynamicCollection.Name}' is not in requiredCollections");
                }
            }
        }
    }

    private void ValidateFunctionDefinitions(JsonElement root, RulesetValidationResult result)
    {
        if (!root.TryGetProperty("functionDefinitions", out var functions))
        {
            result.Errors.Add("Missing required 'functionDefinitions' section");
            return;
        }

        var requiredPhases = new[] { "GameSetup", "WorldGeneration", "Exploration", "Combat", "LevelUp" };
        var foundPhases = new HashSet<string>();

        foreach (var phase in functions.EnumerateObject())
        {
            foundPhases.Add(phase.Name);
            
            if (phase.Value.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add($"Function definitions for phase '{phase.Name}' must be an array");
                continue;
            }

            var functionCount = phase.Value.GetArrayLength();
            if (functionCount == 0)
            {
                result.Warnings.Add($"Phase '{phase.Name}' has no function definitions - consider adding at least one function");
            }

            // Validate individual functions
            foreach (var func in phase.Value.EnumerateArray())
            {
                ValidateFunction(func, phase.Name, result);
            }
        }

        // Check for missing phases
        foreach (var requiredPhase in requiredPhases)
        {
            if (!foundPhases.Contains(requiredPhase))
            {
                result.Warnings.Add($"Missing function definitions for phase '{requiredPhase}' - consider adding functions for this phase");
            }
        }
    }

    private void ValidateFunction(JsonElement function, string phaseName, RulesetValidationResult result)
    {
        if (!function.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString()))
        {
            result.Errors.Add($"Function in phase '{phaseName}' missing required 'name' field");
            return;
        }

        var functionName = name.GetString();

        if (!function.TryGetProperty("description", out var description) || string.IsNullOrWhiteSpace(description.GetString()))
        {
            result.Warnings.Add($"Function '{functionName}' missing 'description' field");
        }

        if (!function.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
        {
            result.Warnings.Add($"Function '{functionName}' missing or invalid 'parameters' object");
        }
        else
        {
            // Validate parameters structure
            if (!parameters.TryGetProperty("type", out var paramType) || paramType.GetString() != "object")
            {
                result.Warnings.Add($"Function '{functionName}' parameters should have type 'object'");
            }

            if (!parameters.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
            {
                result.Warnings.Add($"Function '{functionName}' parameters should have 'properties' object");
            }
        }
    }

    private void ValidatePromptTemplates(JsonElement root, RulesetValidationResult result)
    {
        if (!root.TryGetProperty("promptTemplates", out var prompts))
        {
            result.Errors.Add("Missing required 'promptTemplates' section");
            return;
        }

        var requiredPhases = new[] { "GameSetup", "WorldGeneration", "Exploration", "Combat", "LevelUp" };
        var foundPhases = new HashSet<string>();

        foreach (var phase in prompts.EnumerateObject())
        {
            foundPhases.Add(phase.Name);
            
            if (phase.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(phase.Value.GetString()))
            {
                result.Errors.Add($"Prompt template for phase '{phase.Name}' must be a non-empty string");
                continue;
            }

            var promptText = phase.Value.GetString();
            
            // Check for common placeholders
            var hasPlaceholders = promptText.Contains("{{") && promptText.Contains("}}");
            if (!hasPlaceholders)
            {
                result.Warnings.Add($"Prompt template for phase '{phase.Name}' has no placeholders - consider adding dynamic content like {{character_info}} or {{game_state}}");
            }
        }

        // Check for missing phases
        foreach (var requiredPhase in requiredPhases)
        {
            if (!foundPhases.Contains(requiredPhase))
            {
                result.Warnings.Add($"Missing prompt template for phase '{requiredPhase}' - this phase may not work correctly");
            }
        }
    }

    private void ValidateOverallConsistency(JsonElement root, RulesetValidationResult result)
    {
        // Check that phases mentioned in function definitions have corresponding prompt templates
        if (root.TryGetProperty("functionDefinitions", out var functions) &&
            root.TryGetProperty("promptTemplates", out var prompts))
        {
            var functionPhases = new HashSet<string>();
            var promptPhases = new HashSet<string>();

            foreach (var phase in functions.EnumerateObject())
            {
                functionPhases.Add(phase.Name);
            }

            foreach (var phase in prompts.EnumerateObject())
            {
                promptPhases.Add(phase.Name);
            }

            foreach (var functionPhase in functionPhases)
            {
                if (!promptPhases.Contains(functionPhase))
                {
                    result.Warnings.Add($"Phase '{functionPhase}' has functions but no prompt template");
                }
            }

            foreach (var promptPhase in promptPhases)
            {
                if (!functionPhases.Contains(promptPhase))
                {
                    result.Warnings.Add($"Phase '{promptPhase}' has prompt template but no functions");
                }
            }
        }

        // Validate that game data sections are consistent with schema
        if (root.TryGetProperty("gameStateSchema", out var schema))
        {
            ValidateGameDataConsistency(root, schema, result);
        }
    }

    private void ValidateGameDataConsistency(JsonElement root, JsonElement schema, RulesetValidationResult result)
    {
        // Check if schema mentions certain collections and corresponding data exists
        if (schema.TryGetProperty("requiredCollections", out var collections))
        {
            foreach (var collection in collections.EnumerateArray())
            {
                var collectionName = collection.GetString();
                if (string.IsNullOrWhiteSpace(collectionName)) continue;

                // Check for common collection types
                switch (collectionName.ToLower())
                {
                    case "characters":
                    case "classes":
                    case "trainerclasses":
                    case "characterclasses":
                        if (!root.TryGetProperty("trainerClasses", out _) && 
                            !root.TryGetProperty("characterClasses", out _) &&
                            !root.TryGetProperty("classes", out _))
                        {
                            result.Warnings.Add($"Schema requires '{collectionName}' but no character class data found - consider adding class definitions");
                        }
                        break;
                    case "items":
                    case "equipment":
                        if (!root.TryGetProperty("items", out _) && !root.TryGetProperty("equipment", out _))
                        {
                            result.Warnings.Add($"Schema requires '{collectionName}' but no item data found - consider adding item definitions");
                        }
                        break;
                    case "abilities":
                    case "skills":
                        if (!root.TryGetProperty("abilities", out _) && !root.TryGetProperty("skills", out _))
                        {
                            result.Warnings.Add($"Schema requires '{collectionName}' but no ability data found - consider adding ability definitions");
                        }
                        break;
                }
            }
        }
    }

    private static bool IsValidSemanticVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        
        var parts = version.Split('.');
        if (parts.Length != 3) return false;
        
        return parts.All(part => int.TryParse(part, out _));
    }
}