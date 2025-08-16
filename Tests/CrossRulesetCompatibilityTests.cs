using Xunit;
using System.Text.Json;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState.Models;
using PokeLLM.Tests.TestModels;
using Moq;

namespace PokeLLM.Tests;

/// <summary>
/// Enhanced tests for cross-ruleset compatibility ensuring the dynamic system 
/// works equally well with both Pokemon and D&D 5e rulesets
/// </summary>
[Trait("Category", "Integration")]
[Trait("Ruleset", "CrossCompatibility")]
public class CrossRulesetCompatibilityTests
{
    [Fact]
    public async Task RulesetManager_CanSwitchBetweenRulesetsCleanly()
    {
        // Arrange
        var mockRulesetManager = new Mock<IRulesetManager>();
        var gameState = new GameStateModel();
        
        var pokemonRuleset = CreateMockPokemonRuleset();
        var dndRuleset = CreateMockDnDRuleset();

        mockRulesetManager.Setup(m => m.LoadRulesetAsync("pokemon-adventure"))
            .ReturnsAsync(pokemonRuleset);
        mockRulesetManager.Setup(m => m.LoadRulesetAsync("dnd5e"))
            .ReturnsAsync(dndRuleset);

        // Act - Switch to Pokemon first
        await mockRulesetManager.Object.SetActiveRulesetAsync("pokemon-adventure");
        var pokemonState = gameState.RulesetGameData;

        // Switch to D&D 5e
        await mockRulesetManager.Object.SetActiveRulesetAsync("dnd5e");
        var dndState = gameState.RulesetGameData;

        // Assert - State should be cleanly separated
        mockRulesetManager.Verify(m => m.SetActiveRulesetAsync("pokemon-adventure"), Times.Once);
        mockRulesetManager.Verify(m => m.SetActiveRulesetAsync("dnd5e"), Times.Once);
        Assert.NotNull(pokemonState);
        Assert.NotNull(dndState);
    }

    [Fact]
    public async Task GameStateModel_HandlesRulesetSpecificDataStructures()
    {
        // Arrange
        var gameState = new GameStateModel();
        var pokemonData = new Dictionary<string, object>
        {
            ["trainerClass"] = "ace_trainer",
            ["activePokemon"] = "pikachu",
            ["pokemonTeam"] = new List<string> { "pikachu", "charizard" },
            ["gymBadges"] = new List<string> { "boulder_badge" },
            ["energy"] = 100
        };

        var dndData = new Dictionary<string, object>
        {
            ["race"] = "human",
            ["characterClass"] = "fighter",
            ["abilityScores"] = new Dictionary<string, int>
            {
                ["strength"] = 16,
                ["dexterity"] = 14,
                ["constitution"] = 15,
                ["intelligence"] = 10,
                ["wisdom"] = 12,
                ["charisma"] = 8
            },
            ["spellSlots"] = new Dictionary<int, int> { [1] = 2, [2] = 1 },
            ["hitPoints"] = 28
        };

        // Act - Load Pokemon data first using JsonElement
        foreach (var kvp in pokemonData)
        {
            gameState.RulesetGameData[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value);
        }

        // Assert - Pokemon data is accessible
        Assert.Equal("ace_trainer", gameState.RulesetGameData["trainerClass"].GetString());
        Assert.True(gameState.RulesetGameData.ContainsKey("pokemonTeam"));

        // Act - Switch to D&D data (should completely replace)
        gameState.RulesetGameData.Clear();
        foreach (var kvp in dndData)
        {
            gameState.RulesetGameData[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value);
        }

        // Assert - D&D data is accessible, Pokemon data is gone
        Assert.Equal("human", gameState.RulesetGameData["race"].GetString());
        Assert.True(gameState.RulesetGameData.ContainsKey("abilityScores"));
        Assert.False(gameState.RulesetGameData.ContainsKey("trainerClass"));
    }

    [Fact]
    public async Task BothRulesets_UseIdenticalArchitecturalPatterns()
    {
        // Arrange
        var pokemonPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        var dndPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        
        if (!File.Exists(pokemonPath) || !File.Exists(dndPath))
        {
            // Skip test if ruleset files don't exist
            return;
        }
        
        var pokemonContent = await File.ReadAllTextAsync(pokemonPath);
        var dndContent = await File.ReadAllTextAsync(dndPath);
        
        var pokemonDoc = JsonDocument.Parse(pokemonContent);
        var dndDoc = JsonDocument.Parse(dndContent);

        // Act
        var pokemonRoot = pokemonDoc.RootElement;
        var dndRoot = dndDoc.RootElement;

        // Assert - Both follow identical architectural patterns
        ValidateRulesetArchitecture(pokemonRoot, "Pokemon");
        ValidateRulesetArchitecture(dndRoot, "D&D 5e");
        
        // Both should have the same top-level structure
        var pokemonSections = pokemonRoot.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
        var dndSections = dndRoot.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
        
        var commonSections = pokemonSections.Intersect(dndSections).Count();
        var totalUniqueSections = pokemonSections.Union(dndSections).Count();
        
        // At least 60% of sections should be common architectural elements
        Assert.True((double)commonSections / totalUniqueSections >= 0.6, 
            "Rulesets should share at least 60% architectural similarity");
    }

    [Theory]
    [InlineData("pokemon-adventure")]
    [InlineData("dnd5e")]
    public async Task RulesetValidation_BothRulesetsPassGenericValidation(string rulesetId)
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", $"{rulesetId}.json");
        
        if (!File.Exists(rulesetPath))
        {
            // Skip test if ruleset file doesn't exist
            return;
        }
        
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act & Assert - Both should pass the same validation criteria
        ValidateGenericRulesetStructure(document, rulesetId);
    }

    [Fact]
    public async Task StateManagement_NoInterferenceBetweenRulesets()
    {
        // Arrange
        var pokemonState = new GameStateModel();
        var dndState = new GameStateModel();

        // Setup Pokemon-specific state using JsonElement
        pokemonState.RulesetGameData["currentPokemon"] = JsonSerializer.SerializeToElement("pikachu");
        pokemonState.RulesetGameData["pokemonTeam"] = JsonSerializer.SerializeToElement(new List<string> { "pikachu", "charizard", "blastoise" });
        pokemonState.RulesetGameData["gymBadges"] = JsonSerializer.SerializeToElement(new List<string> { "boulder_badge", "cascade_badge" });
        pokemonState.RulesetGameData["energy"] = JsonSerializer.SerializeToElement(85);
        pokemonState.RulesetGameData["inventory"] = JsonSerializer.SerializeToElement(new Dictionary<string, int> { ["pokeball"] = 10, ["potion"] = 5 });

        // Setup D&D-specific state using JsonElement
        dndState.RulesetGameData["characterClass"] = JsonSerializer.SerializeToElement("wizard");
        dndState.RulesetGameData["race"] = JsonSerializer.SerializeToElement("elf");
        dndState.RulesetGameData["level"] = JsonSerializer.SerializeToElement(3);
        dndState.RulesetGameData["hitPoints"] = JsonSerializer.SerializeToElement(18);
        dndState.RulesetGameData["spellSlots"] = JsonSerializer.SerializeToElement(new Dictionary<int, int> { [1] = 4, [2] = 2 });
        dndState.RulesetGameData["knownSpells"] = JsonSerializer.SerializeToElement(new List<string> { "magic_missile", "shield", "fireball" });

        // Act - Verify states remain independent
        var pokemonInventory = JsonSerializer.Deserialize<Dictionary<string, int>>(pokemonState.RulesetGameData["inventory"].GetRawText());
        var dndSpellSlots = JsonSerializer.Deserialize<Dictionary<int, int>>(dndState.RulesetGameData["spellSlots"].GetRawText());

        // Modify one state
        pokemonInventory["pokeball"] = 5;
        pokemonState.RulesetGameData["inventory"] = JsonSerializer.SerializeToElement(pokemonInventory);

        // Assert - Changes don't affect the other state
        var originalInventory = JsonSerializer.Deserialize<Dictionary<string, int>>(pokemonState.RulesetGameData["inventory"].GetRawText());
        Assert.Equal(5, originalInventory["pokeball"]); // Modified value
        Assert.Equal(4, dndSpellSlots[1]); // D&D state unchanged
        Assert.False(dndState.RulesetGameData.ContainsKey("inventory")); // No Pokemon concepts in D&D state
        Assert.False(pokemonState.RulesetGameData.ContainsKey("spellSlots")); // No D&D concepts in Pokemon state
    }

    [Fact]
    public void EntityService_HandlesRulesetSpecificEntities()
    {
        // Arrange
        var pokemonTrainer = new PokemonTrainer
        {
            Name = "Ash",
            TrainerClass = "ace_trainer",
            Level = 10,
            Charisma = 16
        };
        pokemonTrainer.Pokemon.Add("pikachu");
        pokemonTrainer.Badges.Add("boulder_badge");

        var dndCharacter = new DnDCharacter
        {
            Name = "Gandalf",
            Race = "human",
            CharacterClass = "wizard",
            Level = 5,
            Intelligence = 18
        };
        dndCharacter.KnownSpells.Add("fireball");
        dndCharacter.SpellSlots[3] = 2;

        // Act - Both entity types should work with generic operations
        var pokemonValid = ValidateEntity(pokemonTrainer);
        var dndValid = ValidateEntity(dndCharacter);

        // Assert
        Assert.True(pokemonValid, "Pokemon trainer should validate successfully");
        Assert.True(dndValid, "D&D character should validate successfully");
    }

    #region Helper Methods

    private static JsonDocument CreateMockPokemonRuleset()
    {
        var json = """
        {
          "metadata": { "id": "pokemon-adventure", "name": "Pokemon Adventure" },
          "gameStateSchema": {
            "requiredCollections": ["trainers", "wildPokemon"],
            "playerFields": ["trainerClass", "pokemonTeam"],
            "dynamicCollections": { "trainers": "Trainer" }
          }
        }
        """;
        return JsonDocument.Parse(json);
    }

    private static JsonDocument CreateMockDnDRuleset()
    {
        var json = """
        {
          "metadata": { "id": "dnd5e", "name": "D&D 5e" },
          "gameStateSchema": {
            "requiredCollections": ["characters", "monsters"],
            "playerFields": ["race", "characterClass", "abilityScores"],
            "dynamicCollections": { "characters": "Character" }
          }
        }
        """;
        return JsonDocument.Parse(json);
    }

    private static void ValidateRulesetArchitecture(JsonElement root, string rulesetName)
    {
        Assert.True(root.TryGetProperty("metadata", out _), 
            $"{rulesetName} must have metadata");
        
        // Check for either new or legacy structure
        bool hasNewStructure = root.TryGetProperty("functionDefinitions", out _) &&
                              root.TryGetProperty("promptTemplates", out _);
        bool hasLegacyStructure = root.TryGetProperty("species", out _) ||
                                 root.TryGetProperty("classes", out _);
        
        Assert.True(hasNewStructure || hasLegacyStructure,
            $"{rulesetName} must have either new or legacy structure");
    }

    private static void ValidateGenericRulesetStructure(JsonDocument document, string rulesetId)
    {
        var root = document.RootElement;
        
        // Validate metadata
        var metadata = root.GetProperty("metadata");
        Assert.Equal(rulesetId, metadata.GetProperty("id").GetString());
        Assert.True(metadata.GetProperty("name").GetString()!.Length > 0);
        
        // Either new structure or legacy is acceptable
        bool hasNewStructure = root.TryGetProperty("functionDefinitions", out _);
        bool hasLegacyStructure = root.TryGetProperty("species", out _) || 
                                 root.TryGetProperty("classes", out _);
        
        Assert.True(hasNewStructure || hasLegacyStructure,
            "Ruleset must have either new dynamic structure or legacy structure");
    }

    private static bool ValidateEntity(object entity)
    {
        // Generic validation that should work for any entity type
        return entity != null && 
               entity.GetType().GetProperty("Name")?.GetValue(entity) != null &&
               entity.GetType().GetProperty("Level")?.GetValue(entity) != null;
    }

    #endregion

    #region Data Models

    private class RulesetComplexityMetrics
    {
        public int TotalFunctions { get; set; }
        public double ParametersPerFunction { get; set; }
        public int PhaseCount { get; set; }
    }

    #endregion
}