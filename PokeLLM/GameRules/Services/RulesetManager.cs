using System.Text.Json;
using System.Linq;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState.Models;
using Microsoft.SemanticKernel;

namespace PokeLLM.GameRules.Services;

public class RulesetManager : IRulesetManager
{
    private readonly IRulesetService _rulesetService;
    private readonly IDynamicFunctionFactory _dynamicFunctionFactory;
    private JsonDocument _activeRuleset;
    private string _activeRulesetId = string.Empty;
    private readonly Dictionary<GamePhase, IEnumerable<KernelFunction>> _cachedFunctions = new();

    public RulesetManager(IRulesetService rulesetService, IDynamicFunctionFactory dynamicFunctionFactory)
    {
        _rulesetService = rulesetService;
        _dynamicFunctionFactory = dynamicFunctionFactory;
    }

    public async Task<List<RulesetInfo>> GetAvailableRulesetsAsync()
    {
        var rulesets = new List<RulesetInfo>();
        var rulesetsDirectory = "Rulesets";
        
        if (!Directory.Exists(rulesetsDirectory))
        {
            return rulesets;
        }
        
        var rulesetFiles = Directory.GetFiles(rulesetsDirectory, "*.json");
        
        foreach (var filePath in rulesetFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var document = JsonDocument.Parse(json);
                
                if (document.RootElement.TryGetProperty("metadata", out var metadata))
                {
                    var rulesetInfo = new RulesetInfo();
                    
                    if (metadata.TryGetProperty("id", out var id))
                        rulesetInfo.Id = id.GetString() ?? "";
                    
                    if (metadata.TryGetProperty("name", out var name))
                        rulesetInfo.Name = name.GetString() ?? "";
                    
                    if (metadata.TryGetProperty("description", out var description))
                        rulesetInfo.Description = description.GetString() ?? "";
                    
                    if (metadata.TryGetProperty("version", out var version))
                        rulesetInfo.Version = version.GetString() ?? "";
                    
                    if (metadata.TryGetProperty("tags", out var tags))
                    {
                        foreach (var tag in tags.EnumerateArray())
                        {
                            var tagValue = tag.GetString();
                            if (!string.IsNullOrEmpty(tagValue))
                                rulesetInfo.Tags.Add(tagValue);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(rulesetInfo.Id))
                        rulesets.Add(rulesetInfo);
                }
                
                document.Dispose();
            }
            catch (Exception ex)
            {
                // Skip invalid ruleset files
                Console.WriteLine($"Warning: Could not load ruleset from {filePath}: {ex.Message}");
            }
        }
        
        return rulesets.OrderBy(r => r.Name).ToList();
    }

    public async Task<JsonDocument> LoadRulesetAsync(string rulesetId)
    {
        // Try common locations
        var candidatePaths = new[]
        {
            Path.Combine("Rulesets", $"{rulesetId}.json"),
            Path.Combine("PokeLLM", "Rulesets", $"{rulesetId}.json"),
            Path.Combine(AppContext.BaseDirectory, "Rulesets", $"{rulesetId}.json"),
            Path.Combine(AppContext.BaseDirectory, "PokeLLM", "Rulesets", $"{rulesetId}.json")
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonDocument.Parse(json);
            }
        }
        
        // Fall back to service if an absolute/relative path was provided
        var directPath = Path.Combine("Rulesets", $"{rulesetId}.json");
        return await _rulesetService.LoadRulesetAsync(directPath);
    }

    public JsonDocument GetActiveRuleset()
    {
        return _activeRuleset;
    }

    public async Task SetActiveRulesetAsync(string rulesetId)
    {
        if (_activeRulesetId != rulesetId)
        {
            _activeRuleset?.Dispose();
            _activeRuleset = await LoadRulesetAsync(rulesetId);
            _activeRulesetId = rulesetId;
            _cachedFunctions.Clear(); // Clear cached functions when ruleset changes
        }
    }

    public async Task SetActiveRulesetFromDocumentAsync(JsonDocument document, string rulesetId)
    {
        await Task.Yield();
        
        if (_activeRulesetId != rulesetId || _activeRuleset != document)
        {
            _activeRuleset?.Dispose();
            _activeRuleset = document;
            _activeRulesetId = rulesetId;
            _cachedFunctions.Clear(); // Clear cached functions when ruleset changes
        }
    }

    public void InitializeGameStateFromRuleset(GameStateModel gameState, JsonDocument ruleset)
    {
        if (!ruleset.RootElement.TryGetProperty("gameStateSchema", out var schema))
        {
            return;
        }

        // Set the active ruleset ID
        if (ruleset.RootElement.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("id", out var idElement))
        {
            gameState.ActiveRulesetId = idElement.GetString() ?? "unknown";
        }

        // Initialize dynamic collections based on schema
        if (schema.TryGetProperty("dynamicCollections", out var collections))
        {
            foreach (var collection in collections.EnumerateObject())
            {
                if (!gameState.RulesetGameData.ContainsKey(collection.Name))
                {
                    gameState.RulesetGameData[collection.Name] = JsonSerializer.SerializeToElement(new List<object>());
                }
            }
        }

        // Initialize required collections
        if (schema.TryGetProperty("requiredCollections", out var required))
        {
            foreach (var collectionElement in required.EnumerateArray())
            {
                var collectionName = collectionElement.GetString();
                if (!string.IsNullOrEmpty(collectionName) && 
                    !gameState.RulesetGameData.ContainsKey(collectionName))
                {
                    gameState.RulesetGameData[collectionName] = JsonSerializer.SerializeToElement(new List<object>());
                }
            }
        }
    }

    public bool ValidateGameStateSchema(GameStateModel gameState, JsonDocument ruleset)
    {
        if (!ruleset.RootElement.TryGetProperty("gameStateSchema", out var schema))
        {
            return true; // No schema defined, assume valid
        }

        // Check required collections exist
        if (schema.TryGetProperty("requiredCollections", out var required))
        {
            foreach (var collectionElement in required.EnumerateArray())
            {
                var collectionName = collectionElement.GetString();
                if (!string.IsNullOrEmpty(collectionName) && 
                    !gameState.RulesetGameData.ContainsKey(collectionName))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public async Task<IEnumerable<KernelFunction>> GetPhaseFunctionsAsync(GamePhase phase)
    {
        if (_activeRuleset == null)
        {
            return Enumerable.Empty<KernelFunction>();
        }

        // Return cached functions if available
        if (_cachedFunctions.TryGetValue(phase, out var cachedFunctions))
        {
            return cachedFunctions;
        }

        // Generate functions from ruleset
        var functions = await _dynamicFunctionFactory.GenerateFunctionsFromRulesetAsync(_activeRuleset, phase);
        _cachedFunctions[phase] = functions;
        
        return functions;
    }

    public string GetPhasePromptTemplate(GamePhase phase)
    {
        if (_activeRuleset == null)
        {
            return string.Empty;
        }

        if (!_activeRuleset.RootElement.TryGetProperty("promptTemplates", out var templates))
        {
            return string.Empty;
        }

        var phaseKey = phase.ToString();
        if (!templates.TryGetProperty(phaseKey, out var phaseTemplate))
        {
            return string.Empty;
        }

        if (phaseTemplate.TryGetProperty("systemPrompt", out var systemPrompt))
        {
            return systemPrompt.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    public string GetPhaseObjectiveTemplate(GamePhase phase)
    {
        if (_activeRuleset == null)
        {
            return string.Empty;
        }

        if (!_activeRuleset.RootElement.TryGetProperty("promptTemplates", out var templates))
        {
            return string.Empty;
        }

        var phaseKey = phase.ToString();
        if (!templates.TryGetProperty(phaseKey, out var phaseTemplate))
        {
            return string.Empty;
        }

        if (phaseTemplate.TryGetProperty("phaseObjective", out var phaseObjective))
        {
            return phaseObjective.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    public List<string> GetPhaseContextElements(GamePhase phase)
    {
        if (_activeRuleset == null)
        {
            return new List<string>();
        }

        if (!_activeRuleset.RootElement.TryGetProperty("promptTemplates", out var templates))
        {
            return new List<string>();
        }

        var phaseKey = phase.ToString();
        if (!templates.TryGetProperty(phaseKey, out var phaseTemplate))
        {
            return new List<string>();
        }

        if (phaseTemplate.TryGetProperty("contextElements", out var contextElements))
        {
            var elements = new List<string>();
            foreach (var element in contextElements.EnumerateArray())
            {
                var elementStr = element.GetString();
                if (!string.IsNullOrEmpty(elementStr))
                {
                    elements.Add(elementStr);
                }
            }
            return elements;
        }

        return new List<string>();
    }

    public string GetSettingRequirements()
    {
        if (_activeRuleset == null)
        {
            return string.Empty;
        }

        if (!_activeRuleset.RootElement.TryGetProperty("settingRequirements", out var settingRequirements))
        {
            return string.Empty;
        }

        var requirements = new List<string>();
        foreach (var requirement in settingRequirements.EnumerateObject())
        {
            var value = requirement.Value.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                requirements.Add($"• {requirement.Name}: {value}");
            }
        }

        return string.Join("\n", requirements);
    }

    public string GetStorytellingDirective()
    {
        if (_activeRuleset == null)
        {
            return string.Empty;
        }

        if (!_activeRuleset.RootElement.TryGetProperty("storytellingDirective", out var storytellingDirective))
        {
            return string.Empty;
        }

        var directives = new List<string>();
        foreach (var directive in storytellingDirective.EnumerateObject())
        {
            var value = directive.Value.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                directives.Add($"• {directive.Name}: {value}");
            }
        }

        return string.Join("\n", directives);
    }

    public void Dispose()
    {
        _activeRuleset?.Dispose();
    }
}