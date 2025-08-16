using Xunit;
using System.Text.Json;

namespace PokeLLM.Tests;

/// <summary>
/// Comprehensive validation tests for both Pokemon and D&D 5e rulesets to ensure 
/// they work equally well through the dynamic system
/// </summary>
[Trait("Category", "Integration")]
[Trait("System", "ComprehensiveValidation")]
public class ComprehensiveRulesetValidationTests
{
    [Fact]
    public async Task BothRulesets_ExistAndLoadSuccessfully()
    {
        // Arrange
        var pokemonPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        var dndPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");

        // Act & Assert - Both files should exist and be valid JSON
        Assert.True(File.Exists(pokemonPath), "Pokemon ruleset file should exist");
        Assert.True(File.Exists(dndPath), "D&D 5e ruleset file should exist");

        var pokemonContent = await File.ReadAllTextAsync(pokemonPath);
        var dndContent = await File.ReadAllTextAsync(dndPath);

        var pokemonDoc = JsonDocument.Parse(pokemonContent);
        var dndDoc = JsonDocument.Parse(dndContent);

        Assert.NotNull(pokemonDoc);
        Assert.NotNull(dndDoc);
    }

    [Fact]
    public async Task BothRulesets_HaveRequiredMetadata()
    {
        // Arrange & Act
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();

        // Assert Pokemon metadata
        var pokemonMetadata = pokemonDoc.RootElement.GetProperty("metadata");
        Assert.Equal("pokemon-adventure", pokemonMetadata.GetProperty("id").GetString());
        Assert.Equal("Pokemon Adventure RPG", pokemonMetadata.GetProperty("name").GetString());
        Assert.True(pokemonMetadata.GetProperty("description").GetString()!.Length > 10);

        // Assert D&D metadata
        var dndMetadata = dndDoc.RootElement.GetProperty("metadata");
        Assert.Equal("dnd5e", dndMetadata.GetProperty("id").GetString());
        Assert.Equal("D&D 5e Adventure Ruleset", dndMetadata.GetProperty("name").GetString());
        Assert.True(dndMetadata.GetProperty("description").GetString()!.Length > 10);
    }

    [Fact]
    public async Task BothRulesets_HaveIdenticalArchitecturalStructure()
    {
        // Arrange & Act
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();

        var pokemonSections = GetTopLevelSections(pokemonDoc.RootElement);
        var dndSections = GetTopLevelSections(dndDoc.RootElement);

        // Assert - Core sections should be identical
        var requiredSections = new[] { "metadata", "gameStateSchema", "functionDefinitions", "promptTemplates" };
        
        foreach (var section in requiredSections)
        {
            Assert.Contains(section, pokemonSections);
            Assert.Contains(section, dndSections);
        }

        // Both should have substantial content
        Assert.True(pokemonSections.Count >= 5, "Pokemon should have multiple sections");
        Assert.True(dndSections.Count >= 5, "D&D should have multiple sections");
    }

    [Fact]
    public async Task BothRulesets_HaveGameStateSchemas()
    {
        // Arrange & Act
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();

        // Assert Pokemon schema
        var pokemonSchema = pokemonDoc.RootElement.GetProperty("gameStateSchema");
        Assert.True(pokemonSchema.TryGetProperty("requiredCollections", out var pokemonCollections));
        Assert.True(pokemonSchema.TryGetProperty("playerFields", out var pokemonFields));
        Assert.True(pokemonCollections.GetArrayLength() > 0);
        Assert.True(pokemonFields.GetArrayLength() > 0);

        // Assert D&D schema
        var dndSchema = dndDoc.RootElement.GetProperty("gameStateSchema");
        Assert.True(dndSchema.TryGetProperty("requiredCollections", out var dndCollections));
        Assert.True(dndSchema.TryGetProperty("playerFields", out var dndFields));
        Assert.True(dndCollections.GetArrayLength() > 0);
        Assert.True(dndFields.GetArrayLength() > 0);
    }

    [Fact]
    public async Task BothRulesets_HaveFunctionDefinitionsForAllPhases()
    {
        // Arrange & Act
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();

        var expectedPhases = new[] { "GameSetup", "WorldGeneration", "Exploration", "Combat", "LevelUp" };

        // Assert Pokemon functions
        var pokemonFunctions = pokemonDoc.RootElement.GetProperty("functionDefinitions");
        foreach (var phase in expectedPhases)
        {
            Assert.True(pokemonFunctions.TryGetProperty(phase, out var pokemonPhaseFunctions),
                $"Pokemon should have functions for {phase}");
            Assert.True(pokemonPhaseFunctions.GetArrayLength() > 0,
                $"Pokemon {phase} should have at least one function");
        }

        // Assert D&D functions
        var dndFunctions = dndDoc.RootElement.GetProperty("functionDefinitions");
        foreach (var phase in expectedPhases)
        {
            Assert.True(dndFunctions.TryGetProperty(phase, out var dndPhaseFunctions),
                $"D&D should have functions for {phase}");
            Assert.True(dndPhaseFunctions.GetArrayLength() > 0,
                $"D&D {phase} should have at least one function");
        }
    }


    [Fact]
    public async Task BothRulesets_HavePromptTemplatesForAllPhases()
    {
        // Arrange & Act
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();

        var pokemonPrompts = pokemonDoc.RootElement.GetProperty("promptTemplates");
        var dndPrompts = dndDoc.RootElement.GetProperty("promptTemplates");

        var pokemonPhases = pokemonPrompts.EnumerateObject().Select(p => p.Name).ToHashSet();
        var dndPhases = dndPrompts.EnumerateObject().Select(p => p.Name).ToHashSet();

        // Assert - Both should have prompts for all major phases
        Assert.True(pokemonPhases.Count >= 3, "Pokemon should have prompts for multiple phases");
        Assert.True(dndPhases.Count >= 3, "D&D should have prompts for multiple phases");

        // Verify prompt structure for each phase
        foreach (var phase in pokemonPhases)
        {
            var pokemonPhasePrompts = pokemonPrompts.GetProperty(phase);
            Assert.True(pokemonPhasePrompts.TryGetProperty("systemPrompt", out var pokemonSystemPrompt));
            Assert.True(pokemonSystemPrompt.GetString()!.Length > 20, 
                $"Pokemon {phase} system prompt should be substantial");
        }

        foreach (var phase in dndPhases)
        {
            var dndPhasePrompts = dndPrompts.GetProperty(phase);
            Assert.True(dndPhasePrompts.TryGetProperty("systemPrompt", out var dndSystemPrompt));
            Assert.True(dndSystemPrompt.GetString()!.Length > 20, 
                $"D&D {phase} system prompt should be substantial");
        }
    }

    [Fact]
    public async Task DnD5eRuleset_HasComprehensiveContent()
    {
        // Arrange & Act
        var dndPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var dndContent = await File.ReadAllTextAsync(dndPath);
        var dndDoc = JsonDocument.Parse(dndContent);
        var root = dndDoc.RootElement;

        // Assert - D&D should have comprehensive content
        Assert.True(root.TryGetProperty("races", out var races), "D&D must have races");
        Assert.True(races.GetArrayLength() >= 9, $"D&D should have at least 9 races, found {races.GetArrayLength()}");

        Assert.True(root.TryGetProperty("classes", out var classes), "D&D must have classes");
        Assert.True(classes.GetArrayLength() >= 12, $"D&D should have at least 12 classes, found {classes.GetArrayLength()}");

        Assert.True(root.TryGetProperty("spells", out var spells), "D&D must have spells");
        Assert.True(spells.GetArrayLength() >= 40, $"D&D should have at least 40 spells, found {spells.GetArrayLength()}");

        Assert.True(root.TryGetProperty("equipment", out var equipment), "D&D must have equipment");
        Assert.True(root.TryGetProperty("backgrounds", out var backgrounds), "D&D must have backgrounds");
        Assert.True(backgrounds.GetArrayLength() >= 8, "D&D should have at least 8 backgrounds");
    }

    [Fact]
    public async Task BothRulesets_LoadWithinPerformanceTargets()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Load both rulesets
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();
        stopwatch.Stop();

        // Assert - Should load within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"Both rulesets should load within 5 seconds, took {stopwatch.ElapsedMilliseconds}ms");

        // Both should be valid
        Assert.NotNull(pokemonDoc);
        Assert.NotNull(dndDoc);
    }

    [Fact]
    public async Task BothRulesets_HaveValidationRules()
    {
        // Arrange & Act
        var (pokemonDoc, dndDoc) = await LoadBothRulesets();

        // Assert Pokemon validation rules
        Assert.True(pokemonDoc.RootElement.TryGetProperty("validationRules", out var pokemonRules));
        Assert.True(pokemonRules.EnumerateObject().Count() > 0, "Pokemon should have validation rules");

        // Assert D&D validation rules
        Assert.True(dndDoc.RootElement.TryGetProperty("validationRules", out var dndRules));
        Assert.True(dndRules.EnumerateObject().Count() > 0, "D&D should have validation rules");
    }

    #region Helper Methods

    private static async Task<(JsonDocument Pokemon, JsonDocument DnD)> LoadBothRulesets()
    {
        var pokemonPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        var dndPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");

        var pokemonContent = await File.ReadAllTextAsync(pokemonPath);
        var dndContent = await File.ReadAllTextAsync(dndPath);

        var pokemonDoc = JsonDocument.Parse(pokemonContent);
        var dndDoc = JsonDocument.Parse(dndContent);

        return (pokemonDoc, dndDoc);
    }

    private static List<string> GetTopLevelSections(JsonElement root)
    {
        return root.EnumerateObject().Select(p => p.Name).ToList();
    }

    private static FunctionComplexityMetrics AnalyzeFunctionComplexity(JsonElement root)
    {
        var functions = root.GetProperty("functionDefinitions");
        var totalFunctions = 0;
        var totalParameters = 0;

        foreach (var phase in functions.EnumerateObject())
        {
            foreach (var function in phase.Value.EnumerateArray())
            {
                totalFunctions++;
                if (function.TryGetProperty("parameters", out var parameters))
                {
                    totalParameters += parameters.GetArrayLength();
                }
            }
        }

        return new FunctionComplexityMetrics
        {
            TotalFunctions = totalFunctions,
            AvgParametersPerFunction = totalFunctions > 0 ? (double)totalParameters / totalFunctions : 0
        };
    }

    #endregion

    #region Data Models

    private class FunctionComplexityMetrics
    {
        public int TotalFunctions { get; set; }
        public double AvgParametersPerFunction { get; set; }
    }

    #endregion
}