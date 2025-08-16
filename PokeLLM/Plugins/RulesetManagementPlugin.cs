using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

public class RulesetManagementPlugin
{
    private readonly IRulesetManager _rulesetManager;
    private readonly IJavaScriptRuleEngine _jsEngine;
    private readonly IGameStateRepository _gameStateRepo;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _rulesetsDirectory = "Rulesets";

    public RulesetManagementPlugin(
        IRulesetManager rulesetManager,
        IJavaScriptRuleEngine jsEngine,
        IGameStateRepository gameStateRepo)
    {
        _rulesetManager = rulesetManager;
        _jsEngine = jsEngine;
        _gameStateRepo = gameStateRepo;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Ensure Rulesets directory exists
        if (!Directory.Exists(_rulesetsDirectory))
        {
            Directory.CreateDirectory(_rulesetsDirectory);
        }
    }

    [KernelFunction("create_new_ruleset")]
    [Description("Create a new ruleset with metadata and basic structure")]
    public async Task<string> CreateNewRuleset(
        [Description("Unique identifier for the ruleset")] string rulesetId,
        [Description("Display name for the ruleset")] string name,
        [Description("Version string (e.g., '1.0.0')")] string version,
        [Description("Description of the ruleset")] string description,
        [Description("Authors as comma-separated names")] string authors = "",
        [Description("Tags as comma-separated values")] string tags = "")
    {
        Debug.WriteLine($"[RulesetManagementPlugin] CreateNewRuleset called: {rulesetId}");
        
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(rulesetId) || !IsValidRulesetId(rulesetId))
            {
                return JsonSerializer.Serialize(new { 
                    error = "Invalid ruleset ID. Must contain only letters, numbers, hyphens, and underscores." 
                }, _jsonOptions);
            }

            var rulesetPath = Path.Combine(_rulesetsDirectory, $"{rulesetId}.json");
            
            if (File.Exists(rulesetPath))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Ruleset '{rulesetId}' already exists." 
                }, _jsonOptions);
            }

            // Parse authors and tags
            var authorsList = string.IsNullOrWhiteSpace(authors) 
                ? new List<string>() 
                : authors.Split(',').Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();
            
            var tagsList = string.IsNullOrWhiteSpace(tags) 
                ? new List<string>() 
                : tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

            // Create basic ruleset structure
            var newRuleset = new Dictionary<string, object>
            {
                ["metadata"] = new Dictionary<string, object>
                {
                    ["id"] = rulesetId,
                    ["name"] = name,
                    ["version"] = version,
                    ["description"] = description,
                    ["authors"] = authorsList,
                    ["tags"] = tagsList,
                    ["createdDate"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["lastModified"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                },
                ["gameStateSchema"] = new Dictionary<string, object>
                {
                    ["requiredCollections"] = new List<string>(),
                    ["playerFields"] = new List<string>(),
                    ["dynamicCollections"] = new Dictionary<string, string>()
                },
                ["functionDefinitions"] = new Dictionary<string, List<object>>
                {
                    ["GameSetup"] = new List<object>(),
                    ["WorldGeneration"] = new List<object>(),
                    ["Exploration"] = new List<object>(),
                    ["Combat"] = new List<object>(),
                    ["LevelUp"] = new List<object>()
                },
                ["promptTemplates"] = new Dictionary<string, Dictionary<string, object>>
                {
                    ["GameSetup"] = new Dictionary<string, object>
                    {
                        ["systemPrompt"] = "System prompt for GameSetup phase",
                        ["phaseObjective"] = "Phase objective for GameSetup",
                        ["availableFunctions"] = new List<string>(),
                        ["contextElements"] = new List<string>()
                    },
                    ["WorldGeneration"] = new Dictionary<string, object>
                    {
                        ["systemPrompt"] = "System prompt for WorldGeneration phase",
                        ["phaseObjective"] = "Phase objective for WorldGeneration",
                        ["availableFunctions"] = new List<string>(),
                        ["contextElements"] = new List<string>()
                    },
                    ["Exploration"] = new Dictionary<string, object>
                    {
                        ["systemPrompt"] = "System prompt for Exploration phase",
                        ["phaseObjective"] = "Phase objective for Exploration",
                        ["availableFunctions"] = new List<string>(),
                        ["contextElements"] = new List<string>()
                    },
                    ["Combat"] = new Dictionary<string, object>
                    {
                        ["systemPrompt"] = "System prompt for Combat phase",
                        ["phaseObjective"] = "Phase objective for Combat",
                        ["availableFunctions"] = new List<string>(),
                        ["contextElements"] = new List<string>()
                    },
                    ["LevelUp"] = new Dictionary<string, object>
                    {
                        ["systemPrompt"] = "System prompt for LevelUp phase",
                        ["phaseObjective"] = "Phase objective for LevelUp",
                        ["availableFunctions"] = new List<string>(),
                        ["contextElements"] = new List<string>()
                    }
                }
            };

            // Write to file
            var json = JsonSerializer.Serialize(newRuleset, _jsonOptions);
            await File.WriteAllTextAsync(rulesetPath, json);

            return JsonSerializer.Serialize(new { 
                success = true,
                message = $"Successfully created ruleset '{rulesetId}'",
                rulesetId = rulesetId,
                filePath = rulesetPath
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in CreateNewRuleset: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("add_function_to_ruleset")]
    [Description("Add or update a function definition in a specific phase of a ruleset")]
    public async Task<string> AddFunctionToRuleset(
        [Description("Ruleset ID to modify")] string rulesetId,
        [Description("Game phase (GameSetup, WorldGeneration, Exploration, Combat, LevelUp)")] string phase,
        [Description("Function ID (unique within the phase)")] string functionId,
        [Description("Function name (for LLM calls)")] string functionName,
        [Description("Function description")] string description,
        [Description("Parameters as JSON string")] string parametersJson = "[]",
        [Description("Rule validations as JSON string")] string ruleValidationsJson = "[]",
        [Description("Effects as JSON string")] string effectsJson = "[]")
    {
        Debug.WriteLine($"[RulesetManagementPlugin] AddFunctionToRuleset called: {rulesetId}/{phase}/{functionId}");
        
        try
        {
            // Validate inputs
            if (!IsValidRulesetId(rulesetId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid ruleset ID" }, _jsonOptions);
            }

            if (!Enum.TryParse<GamePhase>(phase, true, out _))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Invalid phase '{phase}'. Must be one of: GameSetup, WorldGeneration, Exploration, Combat, LevelUp" 
                }, _jsonOptions);
            }

            var rulesetPath = Path.Combine(_rulesetsDirectory, $"{rulesetId}.json");
            
            if (!File.Exists(rulesetPath))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Ruleset '{rulesetId}' does not exist." 
                }, _jsonOptions);
            }

            // Parse JSON inputs
            List<object> parameters;
            List<string> ruleValidations;
            List<object> effects;

            try
            {
                parameters = JsonSerializer.Deserialize<List<object>>(parametersJson) ?? new List<object>();
                ruleValidations = JsonSerializer.Deserialize<List<string>>(ruleValidationsJson) ?? new List<string>();
                effects = JsonSerializer.Deserialize<List<object>>(effectsJson) ?? new List<object>();
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Invalid JSON in parameters: {ex.Message}" 
                }, _jsonOptions);
            }

            // Load existing ruleset
            var existingJson = await File.ReadAllTextAsync(rulesetPath);
            var ruleset = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson);
            
            if (ruleset == null)
            {
                return JsonSerializer.Serialize(new { error = "Failed to parse existing ruleset" }, _jsonOptions);
            }

            // Navigate to function definitions
            if (!ruleset.TryGetValue("functionDefinitions", out var functionDefinitionsObj) ||
                functionDefinitionsObj is not JsonElement functionDefinitionsElement)
            {
                return JsonSerializer.Serialize(new { error = "Invalid ruleset structure: missing functionDefinitions" }, _jsonOptions);
            }

            var functionDefinitions = JsonSerializer.Deserialize<Dictionary<string, List<object>>>(functionDefinitionsElement.GetRawText())
                ?? new Dictionary<string, List<object>>();

            // Ensure phase exists
            if (!functionDefinitions.ContainsKey(phase))
            {
                functionDefinitions[phase] = new List<object>();
            }

            // Create function definition
            var functionDef = new Dictionary<string, object>
            {
                ["id"] = functionId,
                ["name"] = functionName,
                ["description"] = description,
                ["parameters"] = parameters,
                ["ruleValidations"] = ruleValidations,
                ["effects"] = effects
            };

            // Remove existing function with same ID if it exists
            var phaseList = functionDefinitions[phase];
            for (int i = phaseList.Count - 1; i >= 0; i--)
            {
                if (phaseList[i] is JsonElement existingFunc)
                {
                    var funcDict = JsonSerializer.Deserialize<Dictionary<string, object>>(existingFunc.GetRawText());
                    if (funcDict != null && funcDict.TryGetValue("id", out var idObj) && 
                        idObj.ToString() == functionId)
                    {
                        phaseList.RemoveAt(i);
                        break;
                    }
                }
            }

            // Add new function
            phaseList.Add(functionDef);

            // Update ruleset
            ruleset["functionDefinitions"] = functionDefinitions;
            
            // Update last modified timestamp
            if (ruleset.TryGetValue("metadata", out var metadataObj) && metadataObj is JsonElement metadataElement)
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataElement.GetRawText()) 
                    ?? new Dictionary<string, object>();
                metadata["lastModified"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                ruleset["metadata"] = metadata;
            }

            // Save updated ruleset
            var updatedJson = JsonSerializer.Serialize(ruleset, _jsonOptions);
            await File.WriteAllTextAsync(rulesetPath, updatedJson);

            return JsonSerializer.Serialize(new { 
                success = true,
                message = $"Successfully added/updated function '{functionId}' in phase '{phase}' of ruleset '{rulesetId}'",
                rulesetId = rulesetId,
                phase = phase,
                functionId = functionId
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in AddFunctionToRuleset: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("update_game_data")]
    [Description("Add or update game data sections (like Pokemon species, moves, items, etc.) in a ruleset")]
    public async Task<string> UpdateGameData(
        [Description("Ruleset ID to modify")] string rulesetId,
        [Description("Data section name (e.g., 'pokemonSpecies', 'moves', 'items')")] string sectionName,
        [Description("Data as JSON string - can be object or array depending on section")] string dataJson)
    {
        Debug.WriteLine($"[RulesetManagementPlugin] UpdateGameData called: {rulesetId}/{sectionName}");
        
        try
        {
            // Validate inputs
            if (!IsValidRulesetId(rulesetId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid ruleset ID" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(sectionName))
            {
                return JsonSerializer.Serialize(new { error = "Section name cannot be empty" }, _jsonOptions);
            }

            var rulesetPath = Path.Combine(_rulesetsDirectory, $"{rulesetId}.json");
            
            if (!File.Exists(rulesetPath))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Ruleset '{rulesetId}' does not exist." 
                }, _jsonOptions);
            }

            // Parse data JSON
            object data;
            try
            {
                data = JsonSerializer.Deserialize<object>(dataJson);
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Invalid JSON in data: {ex.Message}" 
                }, _jsonOptions);
            }

            // Load existing ruleset
            var existingJson = await File.ReadAllTextAsync(rulesetPath);
            var ruleset = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson);
            
            if (ruleset == null)
            {
                return JsonSerializer.Serialize(new { error = "Failed to parse existing ruleset" }, _jsonOptions);
            }

            // Update the data section
            ruleset[sectionName] = data;
            
            // Update last modified timestamp
            if (ruleset.TryGetValue("metadata", out var metadataObj) && metadataObj is JsonElement metadataElement)
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataElement.GetRawText()) 
                    ?? new Dictionary<string, object>();
                metadata["lastModified"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                ruleset["metadata"] = metadata;
            }

            // Save updated ruleset
            var updatedJson = JsonSerializer.Serialize(ruleset, _jsonOptions);
            await File.WriteAllTextAsync(rulesetPath, updatedJson);

            return JsonSerializer.Serialize(new { 
                success = true,
                message = $"Successfully updated '{sectionName}' in ruleset '{rulesetId}'",
                rulesetId = rulesetId,
                sectionName = sectionName
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in UpdateGameData: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("validate_ruleset")]
    [Description("Validate a ruleset for structural integrity and rule syntax")]
    public async Task<string> ValidateRuleset(
        [Description("Ruleset ID to validate")] string rulesetId,
        [Description("Whether to perform comprehensive JavaScript rule validation")] bool validateJavaScriptRules = false)
    {
        Debug.WriteLine($"[RulesetManagementPlugin] ValidateRuleset called: {rulesetId}");
        
        try
        {
            if (!IsValidRulesetId(rulesetId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid ruleset ID" }, _jsonOptions);
            }

            var rulesetPath = Path.Combine(_rulesetsDirectory, $"{rulesetId}.json");
            
            if (!File.Exists(rulesetPath))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Ruleset '{rulesetId}' does not exist." 
                }, _jsonOptions);
            }

            var validationErrors = new List<string>();
            var validationWarnings = new List<string>();

            // Load and parse ruleset
            JsonDocument ruleset;
            try
            {
                var json = await File.ReadAllTextAsync(rulesetPath);
                ruleset = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Invalid JSON format: {ex.Message}",
                    valid = false
                }, _jsonOptions);
            }

            var root = ruleset.RootElement;

            // Validate required top-level sections
            var requiredSections = new[] { "metadata", "functionDefinitions", "promptTemplates" };
            foreach (var section in requiredSections)
            {
                if (!root.TryGetProperty(section, out _))
                {
                    validationErrors.Add($"Missing required section: {section}");
                }
            }

            // Validate metadata
            if (root.TryGetProperty("metadata", out var metadata))
            {
                var requiredMetadata = new[] { "id", "name", "version", "description" };
                foreach (var field in requiredMetadata)
                {
                    if (!metadata.TryGetProperty(field, out var prop) || string.IsNullOrWhiteSpace(prop.GetString()))
                    {
                        validationErrors.Add($"Missing or empty metadata field: {field}");
                    }
                }

                // Validate ID matches filename
                if (metadata.TryGetProperty("id", out var idProp))
                {
                    var metadataId = idProp.GetString();
                    if (metadataId != rulesetId)
                    {
                        validationErrors.Add($"Metadata ID '{metadataId}' does not match filename '{rulesetId}'");
                    }
                }
            }

            // Validate function definitions
            if (root.TryGetProperty("functionDefinitions", out var functionDefs))
            {
                var validPhases = Enum.GetNames<GamePhase>();
                foreach (var phaseProp in functionDefs.EnumerateObject())
                {
                    if (!validPhases.Contains(phaseProp.Name))
                    {
                        validationWarnings.Add($"Unknown game phase: {phaseProp.Name}");
                    }

                    // Validate functions in this phase
                    if (phaseProp.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var func in phaseProp.Value.EnumerateArray())
                        {
                            if (!func.TryGetProperty("id", out _))
                            {
                                validationErrors.Add($"Function in phase {phaseProp.Name} missing 'id'");
                            }
                            if (!func.TryGetProperty("name", out _))
                            {
                                validationErrors.Add($"Function in phase {phaseProp.Name} missing 'name'");
                            }
                            if (!func.TryGetProperty("description", out _))
                            {
                                validationErrors.Add($"Function in phase {phaseProp.Name} missing 'description'");
                            }

                            // Validate JavaScript rules if requested
                            if (validateJavaScriptRules && func.TryGetProperty("ruleValidations", out var ruleValidations))
                            {
                                foreach (var rule in ruleValidations.EnumerateArray())
                                {
                                    var ruleText = rule.GetString();
                                    if (!string.IsNullOrWhiteSpace(ruleText))
                                    {
                                        try
                                        {
                                            // Test JavaScript rule safety and compilation
                                            var isSafe = await _jsEngine.IsSafeScriptAsync(ruleText);
                                            if (!isSafe)
                                            {
                                                validationErrors.Add($"JavaScript rule contains unsafe operations: {ruleText}");
                                            }
                                            
                                            // Test basic execution with empty context
                                            try
                                            {
                                                await _jsEngine.ExecuteAsync(ruleText, new Dictionary<string, object>
                                                {
                                                    ["character"] = new Dictionary<string, object>(),
                                                    ["gameState"] = new Dictionary<string, object>(),
                                                    ["rulesetData"] = new Dictionary<string, object>()
                                                });
                                            }
                                            catch (Exception execEx)
                                            {
                                                validationWarnings.Add($"JavaScript rule may have runtime issues: {execEx.Message}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            validationErrors.Add($"JavaScript rule error in {phaseProp.Name}: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Validate prompt templates
            if (root.TryGetProperty("promptTemplates", out var promptTemplates))
            {
                var validPhases = Enum.GetNames<GamePhase>();
                foreach (var phaseProp in promptTemplates.EnumerateObject())
                {
                    if (!validPhases.Contains(phaseProp.Name))
                    {
                        validationWarnings.Add($"Prompt template for unknown game phase: {phaseProp.Name}");
                    }

                    var requiredPromptFields = new[] { "systemPrompt", "phaseObjective", "availableFunctions", "contextElements" };
                    foreach (var field in requiredPromptFields)
                    {
                        if (!phaseProp.Value.TryGetProperty(field, out _))
                        {
                            validationWarnings.Add($"Prompt template for {phaseProp.Name} missing field: {field}");
                        }
                    }
                }
            }

            // Test loading with ruleset manager
            try
            {
                var loadedRuleset = await _rulesetManager.LoadRulesetAsync(rulesetId);
                loadedRuleset.Dispose();
            }
            catch (Exception ex)
            {
                validationErrors.Add($"Failed to load with RulesetManager: {ex.Message}");
            }

            var isValid = validationErrors.Count == 0;

            return JsonSerializer.Serialize(new { 
                valid = isValid,
                rulesetId = rulesetId,
                errors = validationErrors,
                warnings = validationWarnings,
                errorCount = validationErrors.Count,
                warningCount = validationWarnings.Count,
                message = isValid ? "Ruleset validation passed" : $"Ruleset validation failed with {validationErrors.Count} error(s)"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in ValidateRuleset: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, valid = false }, _jsonOptions);
        }
    }

    [KernelFunction("switch_active_ruleset")]
    [Description("Switch the active ruleset in the current game")]
    public async Task<string> SwitchActiveRuleset(
        [Description("Ruleset ID to switch to")] string rulesetId,
        [Description("Whether to validate the ruleset before switching")] bool validateFirst = true)
    {
        Debug.WriteLine($"[RulesetManagementPlugin] SwitchActiveRuleset called: {rulesetId}");
        
        try
        {
            if (!IsValidRulesetId(rulesetId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid ruleset ID" }, _jsonOptions);
            }

            var rulesetPath = Path.Combine(_rulesetsDirectory, $"{rulesetId}.json");
            
            if (!File.Exists(rulesetPath))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Ruleset '{rulesetId}' does not exist." 
                }, _jsonOptions);
            }

            // Validate first if requested
            if (validateFirst)
            {
                var validationResult = await ValidateRuleset(rulesetId, false);
                var validation = JsonSerializer.Deserialize<Dictionary<string, object>>(validationResult);
                
                if (validation != null && validation.TryGetValue("valid", out var validObj))
                {
                    if (validObj is JsonElement validElement && !validElement.GetBoolean())
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Cannot switch to invalid ruleset. Use validate_ruleset for details.",
                            validationResult = validation
                        }, _jsonOptions);
                    }
                }
            }

            // Load current game state
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var previousRulesetId = gameState.ActiveRulesetId;

            // Switch the active ruleset through the manager
            await _rulesetManager.SetActiveRulesetAsync(rulesetId);

            // Update game state
            gameState.ActiveRulesetId = rulesetId;
            gameState.LastSaveTime = DateTime.UtcNow;

            // Add event log
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"Switched active ruleset from '{previousRulesetId}' to '{rulesetId}'" 
            });

            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true,
                message = $"Successfully switched active ruleset to '{rulesetId}'",
                previousRulesetId = previousRulesetId,
                newRulesetId = rulesetId,
                sessionId = gameState.SessionId
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in SwitchActiveRuleset: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("list_available_rulesets")]
    [Description("List all available rulesets with their metadata")]
    public async Task<string> ListAvailableRulesets(
        [Description("Whether to include detailed metadata for each ruleset")] bool includeMetadata = true)
    {
        Debug.WriteLine($"[RulesetManagementPlugin] ListAvailableRulesets called");
        
        try
        {
            var rulesets = new List<object>();

            if (Directory.Exists(_rulesetsDirectory))
            {
                var rulesetFiles = Directory.GetFiles(_rulesetsDirectory, "*.json");

                foreach (var filePath in rulesetFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var fileInfo = new FileInfo(filePath);

                        if (includeMetadata)
                        {
                            var json = await File.ReadAllTextAsync(filePath);
                            var ruleset = JsonDocument.Parse(json);

                            var rulesetInfo = new Dictionary<string, object>
                            {
                                ["rulesetId"] = fileName,
                                ["filePath"] = filePath,
                                ["fileSize"] = fileInfo.Length,
                                ["lastModifiedFile"] = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
                            };

                            if (ruleset.RootElement.TryGetProperty("metadata", out var metadata))
                            {
                                rulesetInfo["metadata"] = JsonSerializer.Deserialize<object>(metadata.GetRawText());
                            }

                            // Count functions by phase
                            if (ruleset.RootElement.TryGetProperty("functionDefinitions", out var functionDefs))
                            {
                                var functionCounts = new Dictionary<string, int>();
                                var totalFunctions = 0;

                                foreach (var phase in functionDefs.EnumerateObject())
                                {
                                    var count = phase.Value.ValueKind == JsonValueKind.Array ? phase.Value.GetArrayLength() : 0;
                                    functionCounts[phase.Name] = count;
                                    totalFunctions += count;
                                }

                                rulesetInfo["functionCounts"] = functionCounts;
                                rulesetInfo["totalFunctions"] = totalFunctions;
                            }

                            rulesets.Add(rulesetInfo);
                        }
                        else
                        {
                            rulesets.Add(new 
                            {
                                rulesetId = fileName,
                                filePath = filePath,
                                fileSize = fileInfo.Length,
                                lastModifiedFile = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        rulesets.Add(new 
                        {
                            rulesetId = Path.GetFileNameWithoutExtension(filePath),
                            error = $"Failed to read ruleset: {ex.Message}",
                            filePath = filePath
                        });
                    }
                }
            }

            // Get current active ruleset
            var currentGameState = await _gameStateRepo.LoadLatestStateAsync();
            var activeRulesetId = currentGameState.ActiveRulesetId;

            return JsonSerializer.Serialize(new { 
                rulesets = rulesets,
                totalRulesets = rulesets.Count,
                activeRulesetId = activeRulesetId,
                rulesetsDirectory = _rulesetsDirectory
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in ListAvailableRulesets: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("get_ruleset_details")]
    [Description("Get detailed information about a specific ruleset")]
    public async Task<string> GetRulesetDetails(
        [Description("Ruleset ID to examine")] string rulesetId,
        [Description("Whether to include full function definitions")] bool includeFunctions = false,
        [Description("Whether to include game data sections")] bool includeGameData = false)
    {
        Debug.WriteLine($"[RulesetManagementPlugin] GetRulesetDetails called: {rulesetId}");
        
        try
        {
            if (!IsValidRulesetId(rulesetId))
            {
                return JsonSerializer.Serialize(new { error = "Invalid ruleset ID" }, _jsonOptions);
            }

            var rulesetPath = Path.Combine(_rulesetsDirectory, $"{rulesetId}.json");
            
            if (!File.Exists(rulesetPath))
            {
                return JsonSerializer.Serialize(new { 
                    error = $"Ruleset '{rulesetId}' does not exist." 
                }, _jsonOptions);
            }

            var json = await File.ReadAllTextAsync(rulesetPath);
            var ruleset = JsonDocument.Parse(json);
            var root = ruleset.RootElement;

            var details = new Dictionary<string, object>
            {
                ["rulesetId"] = rulesetId,
                ["filePath"] = rulesetPath,
                ["fileSize"] = new FileInfo(rulesetPath).Length
            };

            // Always include metadata
            if (root.TryGetProperty("metadata", out var metadata))
            {
                details["metadata"] = JsonSerializer.Deserialize<object>(metadata.GetRawText());
            }

            // Always include prompt templates
            if (root.TryGetProperty("promptTemplates", out var promptTemplates))
            {
                details["promptTemplates"] = JsonSerializer.Deserialize<object>(promptTemplates.GetRawText());
            }

            // Always include game state schema
            if (root.TryGetProperty("gameStateSchema", out var gameStateSchema))
            {
                details["gameStateSchema"] = JsonSerializer.Deserialize<object>(gameStateSchema.GetRawText());
            }

            // Include functions if requested
            if (includeFunctions && root.TryGetProperty("functionDefinitions", out var functionDefs))
            {
                details["functionDefinitions"] = JsonSerializer.Deserialize<object>(functionDefs.GetRawText());
            }
            else if (root.TryGetProperty("functionDefinitions", out var funcDefsForCounting))
            {
                // Just include counts
                var functionCounts = new Dictionary<string, int>();
                var totalFunctions = 0;

                foreach (var phase in funcDefsForCounting.EnumerateObject())
                {
                    var count = phase.Value.ValueKind == JsonValueKind.Array ? phase.Value.GetArrayLength() : 0;
                    functionCounts[phase.Name] = count;
                    totalFunctions += count;
                }

                details["functionCounts"] = functionCounts;
                details["totalFunctions"] = totalFunctions;
            }

            // Include game data if requested
            if (includeGameData)
            {
                var gameDataSections = new Dictionary<string, object>();
                
                foreach (var property in root.EnumerateObject())
                {
                    // Skip structural sections
                    if (property.Name is "metadata" or "functionDefinitions" or "promptTemplates" or "gameStateSchema")
                        continue;

                    gameDataSections[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText());
                }

                if (gameDataSections.Any())
                {
                    details["gameData"] = gameDataSections;
                }
            }

            // Check if this is the active ruleset
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            details["isActive"] = gameState.ActiveRulesetId == rulesetId;

            return JsonSerializer.Serialize(details, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RulesetManagementPlugin] Error in GetRulesetDetails: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    private static bool IsValidRulesetId(string rulesetId)
    {
        if (string.IsNullOrWhiteSpace(rulesetId))
            return false;

        // Allow letters, numbers, hyphens, and underscores only
        return rulesetId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}