using Xunit;
using System.Text.Json;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.Tests.TestModels;

namespace PokeLLM.Tests;

/// <summary>
/// Comprehensive validation tests for the D&D 5e ruleset to ensure it meets all schema requirements
/// and provides content equivalent to the Pokemon ruleset
/// </summary>
[Trait("Category", "Unit")]
[Trait("Ruleset", "DnD5e")]
public class DnD5eRulesetValidationTests
{
    [Fact]
    public async Task DnD5eRuleset_LoadsSuccessfully()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        
        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            var content = await File.ReadAllTextAsync(rulesetPath);
            var document = JsonDocument.Parse(content);
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DnD5eRuleset_HasRequiredMetadata()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasMetadata = root.TryGetProperty("metadata", out var metadata);

        // Assert
        Assert.True(hasMetadata, "D&D 5e ruleset must have metadata section");
        Assert.True(metadata.TryGetProperty("id", out var id), "Metadata must have id");
        Assert.True(metadata.TryGetProperty("name", out var name), "Metadata must have name");
        Assert.True(metadata.TryGetProperty("version", out var version), "Metadata must have version");
        Assert.True(metadata.TryGetProperty("description", out var description), "Metadata must have description");
        Assert.True(metadata.TryGetProperty("tags", out var tags), "Metadata must have tags");
        
        Assert.Equal("dnd5e", id.GetString());
        Assert.Equal("D&D 5e Adventure Ruleset", name.GetString());
        Assert.True(tags.GetArrayLength() > 0, "Must have at least one tag");
    }

    [Fact]
    public async Task DnD5eRuleset_HasValidGameStateSchema()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasSchema = root.TryGetProperty("gameStateSchema", out var schema);

        // Assert
        Assert.True(hasSchema, "D&D 5e ruleset must have gameStateSchema");
        Assert.True(schema.TryGetProperty("requiredCollections", out var collections), 
            "Schema must have requiredCollections");
        Assert.True(schema.TryGetProperty("playerFields", out var playerFields), 
            "Schema must have playerFields");
        Assert.True(schema.TryGetProperty("dynamicCollections", out var dynamicCollections), 
            "Schema must have dynamicCollections");
        
        // Validate required collections are present
        var collectionArray = collections.EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains("characters", collectionArray);
        Assert.Contains("npcs", collectionArray);
        Assert.Contains("monsters", collectionArray);
        Assert.Contains("locations", collectionArray);
        Assert.Contains("equipment", collectionArray);
        Assert.Contains("spells", collectionArray);
        Assert.Contains("combatStates", collectionArray);
        
        // Validate player fields are D&D appropriate
        var fieldArray = playerFields.EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("race", fieldArray);
        Assert.Contains("characterClass", fieldArray);
        Assert.Contains("abilityScores", fieldArray);
        Assert.Contains("hitPoints", fieldArray);
        Assert.Contains("spellSlots", fieldArray);
        Assert.Contains("knownSpells", fieldArray);
    }

    [Fact]
    public async Task DnD5eRuleset_Has12Classes()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasClasses = root.TryGetProperty("classes", out var classes);

        // Assert
        Assert.True(hasClasses, "D&D 5e ruleset must have classes section");
        Assert.True(classes.GetArrayLength() >= 12, 
            $"D&D 5e should have at least 12 classes, found {classes.GetArrayLength()}");
        
        // Validate class structure
        var firstClass = classes.EnumerateArray().First();
        Assert.True(firstClass.TryGetProperty("id", out _), "Class must have id");
        Assert.True(firstClass.TryGetProperty("name", out _), "Class must have name");
        Assert.True(firstClass.TryGetProperty("hitDie", out _), "Class must have hitDie");
        Assert.True(firstClass.TryGetProperty("primaryAbility", out _), "Class must have primaryAbility");
        Assert.True(firstClass.TryGetProperty("savingThrowProficiencies", out _), 
            "Class must have savingThrowProficiencies");
    }

    [Fact]
    public async Task DnD5eRuleset_Has9Races()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasRaces = root.TryGetProperty("races", out var races);

        // Assert
        Assert.True(hasRaces, "D&D 5e ruleset must have races section");
        Assert.True(races.GetArrayLength() >= 9, 
            $"D&D 5e should have at least 9 races, found {races.GetArrayLength()}");
        
        // Validate race structure
        var firstRace = races.EnumerateArray().First();
        Assert.True(firstRace.TryGetProperty("id", out _), "Race must have id");
        Assert.True(firstRace.TryGetProperty("name", out _), "Race must have name");
        Assert.True(firstRace.TryGetProperty("abilityScoreIncrease", out var abilityIncrease), 
            "Race must have abilityScoreIncrease");
        Assert.True(firstRace.TryGetProperty("traits", out _), "Race must have traits");
        
        // Validate ability score increases use proper D&D ability names
        var abilities = abilityIncrease.EnumerateObject().Select(p => p.Name).ToList();
        var validAbilities = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };
        Assert.All(abilities, ability => Assert.Contains(ability, validAbilities));
    }

    [Fact]
    public async Task DnD5eRuleset_HasComprehensiveSpellSystem()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasSpells = root.TryGetProperty("spells", out var spells);

        // Assert
        Assert.True(hasSpells, "D&D 5e ruleset must have spells section");
        Assert.True(spells.GetArrayLength() >= 40, 
            $"D&D 5e should have at least 40 spells, found {spells.GetArrayLength()}");
        
        // Validate spell levels 0-3 are covered
        var spellLevels = spells.EnumerateArray()
            .Select(s => s.GetProperty("level").GetInt32())
            .Distinct()
            .OrderBy(l => l)
            .ToList();
        
        Assert.Contains(0, spellLevels); // Cantrips
        Assert.Contains(1, spellLevels);
        Assert.Contains(2, spellLevels);
        Assert.Contains(3, spellLevels);
        
        // Validate spell structure
        var firstSpell = spells.EnumerateArray().First();
        Assert.True(firstSpell.TryGetProperty("id", out _), "Spell must have id");
        Assert.True(firstSpell.TryGetProperty("name", out _), "Spell must have name");
        Assert.True(firstSpell.TryGetProperty("level", out _), "Spell must have level");
        Assert.True(firstSpell.TryGetProperty("school", out _), "Spell must have school");
        Assert.True(firstSpell.TryGetProperty("classes", out _), "Spell must have classes");
    }

    [Fact]
    public async Task DnD5eRuleset_HasFunctionDefinitionsForAllPhases()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);
        var expectedPhases = new[] { "GameSetup", "WorldGeneration", "Exploration", "Combat", "LevelUp" };

        // Act
        var root = document.RootElement;
        var hasFunctions = root.TryGetProperty("functionDefinitions", out var functions);

        // Assert
        Assert.True(hasFunctions, "D&D 5e ruleset must have functionDefinitions");
        
        foreach (var phase in expectedPhases)
        {
            Assert.True(functions.TryGetProperty(phase, out var phaseFunctions), 
                $"Must have function definitions for {phase} phase");
            Assert.True(phaseFunctions.GetArrayLength() > 0, 
                $"{phase} phase must have at least one function");
            
            // Validate function structure
            var firstFunction = phaseFunctions.EnumerateArray().First();
            Assert.True(firstFunction.TryGetProperty("id", out _), "Function must have id");
            Assert.True(firstFunction.TryGetProperty("name", out _), "Function must have name");
            Assert.True(firstFunction.TryGetProperty("description", out _), "Function must have description");
            Assert.True(firstFunction.TryGetProperty("parameters", out _), "Function must have parameters");
        }
    }

    [Fact]
    public async Task DnD5eRuleset_FunctionComplexityComparableToPokemon()
    {
        // Arrange
        var dndPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var pokemonPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        
        var dndContent = await File.ReadAllTextAsync(dndPath);
        var pokemonContent = await File.ReadAllTextAsync(pokemonPath);
        
        var dndDoc = JsonDocument.Parse(dndContent);
        var pokemonDoc = JsonDocument.Parse(pokemonContent);

        // Act
        var dndFunctions = dndDoc.RootElement.GetProperty("functionDefinitions");
        var pokemonFunctions = pokemonDoc.RootElement.GetProperty("functionDefinitions");

        var dndTotalFunctions = 0;
        var pokemonTotalFunctions = 0;

        foreach (var phase in dndFunctions.EnumerateObject())
        {
            dndTotalFunctions += phase.Value.GetArrayLength();
        }

        foreach (var phase in pokemonFunctions.EnumerateObject())
        {
            pokemonTotalFunctions += phase.Value.GetArrayLength();
        }

        // Assert - D&D should have comparable or more functions than Pokemon
        Assert.True(dndTotalFunctions >= pokemonTotalFunctions * 0.8, 
            $"D&D 5e function count ({dndTotalFunctions}) should be comparable to Pokemon ({pokemonTotalFunctions})");
        
        // Validate both have similar phase coverage
        var dndPhases = dndFunctions.EnumerateObject().Select(p => p.Name).ToHashSet();
        var pokemonPhases = pokemonFunctions.EnumerateObject().Select(p => p.Name).ToHashSet();
        
        Assert.True(dndPhases.SetEquals(pokemonPhases), 
            "D&D 5e and Pokemon should have the same game phases");
    }

    [Fact]
    public async Task DnD5eRuleset_HasPromptTemplatesForAllPhases()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasPrompts = root.TryGetProperty("promptTemplates", out var prompts);

        // Assert
        Assert.True(hasPrompts, "D&D 5e ruleset must have promptTemplates");
        
        // Validate each phase has proper prompt structure
        foreach (var phase in prompts.EnumerateObject())
        {
            var phasePrompts = phase.Value;
            Assert.True(phasePrompts.TryGetProperty("systemPrompt", out _), 
                $"{phase.Name} must have systemPrompt");
            Assert.True(phasePrompts.TryGetProperty("phaseObjective", out _), 
                $"{phase.Name} must have phaseObjective");
            Assert.True(phasePrompts.TryGetProperty("availableFunctions", out var functions), 
                $"{phase.Name} must have availableFunctions");
            Assert.True(functions.GetArrayLength() > 0, 
                $"{phase.Name} must have at least one available function");
        }
    }

    [Fact]
    public async Task DnD5eRuleset_ValidationRulesAreComplete()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasValidation = root.TryGetProperty("validationRules", out var validation);

        // Assert
        Assert.True(hasValidation, "D&D 5e ruleset must have validationRules");
        
        // Key validation rules for D&D
        Assert.True(validation.TryGetProperty("characterCreation", out _), 
            "Must have characterCreation validation");
        Assert.True(validation.TryGetProperty("spellCasting", out _), 
            "Must have spellCasting validation");
        Assert.True(validation.TryGetProperty("combatActions", out _), 
            "Must have combatActions validation");
    }

    [Fact]
    public async Task DnD5eRuleset_EquipmentSystemIsComprehensive()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasEquipment = root.TryGetProperty("equipment", out var equipment);

        // Assert
        Assert.True(hasEquipment, "D&D 5e ruleset must have equipment section");
        
        // Should have weapons, armor, and adventuring gear
        Assert.True(equipment.TryGetProperty("weapons", out var weapons), "Must have weapons");
        Assert.True(equipment.TryGetProperty("armor", out var armor), "Must have armor");
        Assert.True(equipment.TryGetProperty("adventuringGear", out var gear), "Must have adventuring gear");
        
        Assert.True(weapons.GetArrayLength() >= 20, "Should have at least 20 weapons");
        Assert.True(armor.GetArrayLength() >= 10, "Should have at least 10 armor types");
        Assert.True(gear.GetArrayLength() >= 30, "Should have at least 30 gear items");
    }

    [Fact]
    public async Task DnD5eRuleset_BackgroundsProvideVariety()
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var hasBackgrounds = root.TryGetProperty("backgrounds", out var backgrounds);

        // Assert
        Assert.True(hasBackgrounds, "D&D 5e ruleset must have backgrounds section");
        Assert.True(backgrounds.GetArrayLength() >= 8, 
            $"Should have at least 8 backgrounds, found {backgrounds.GetArrayLength()}");
        
        // Validate background structure
        var firstBackground = backgrounds.EnumerateArray().First();
        Assert.True(firstBackground.TryGetProperty("id", out _), "Background must have id");
        Assert.True(firstBackground.TryGetProperty("name", out _), "Background must have name");
        Assert.True(firstBackground.TryGetProperty("skillProficiencies", out _), 
            "Background must have skillProficiencies");
        Assert.True(firstBackground.TryGetProperty("equipment", out _), 
            "Background must have equipment");
    }

    [Theory]
    [InlineData("GameSetup", "create_character")]
    [InlineData("GameSetup", "select_race")]
    [InlineData("GameSetup", "select_class")]
    [InlineData("Combat", "cast_spell")]
    [InlineData("Combat", "make_attack")]
    [InlineData("Exploration", "investigation_check")]
    [InlineData("LevelUp", "gain_level")]
    public async Task DnD5eRuleset_HasExpectedCoreFunctions(string phase, string functionId)
    {
        // Arrange
        var rulesetPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var content = await File.ReadAllTextAsync(rulesetPath);
        var document = JsonDocument.Parse(content);

        // Act
        var root = document.RootElement;
        var functions = root.GetProperty("functionDefinitions").GetProperty(phase);
        var hasFunction = functions.EnumerateArray()
            .Any(f => f.GetProperty("id").GetString() == functionId);

        // Assert
        Assert.True(hasFunction, $"D&D 5e {phase} phase must have {functionId} function");
    }

    [Fact]
    public async Task DnD5eRuleset_JsonStructureMatchesPokemonSchema()
    {
        // Arrange
        var dndPath = Path.Combine("PokeLLM", "Rulesets", "dnd5e.json");
        var pokemonPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        
        var dndContent = await File.ReadAllTextAsync(dndPath);
        var pokemonContent = await File.ReadAllTextAsync(pokemonPath);
        
        var dndDoc = JsonDocument.Parse(dndContent);
        var pokemonDoc = JsonDocument.Parse(pokemonContent);

        // Act & Assert - Both should have same top-level structure
        var dndRoot = dndDoc.RootElement;
        var pokemonRoot = pokemonDoc.RootElement;

        var dndProperties = dndRoot.EnumerateObject().Select(p => p.Name).ToHashSet();
        var pokemonProperties = pokemonRoot.EnumerateObject().Select(p => p.Name).ToHashSet();

        // Core schema sections must match
        var requiredSections = new[] { "metadata", "gameStateSchema", "functionDefinitions", "promptTemplates", "validationRules" };
        
        foreach (var section in requiredSections)
        {
            Assert.True(dndProperties.Contains(section), $"D&D 5e must have {section} section");
            Assert.True(pokemonProperties.Contains(section), $"Pokemon must have {section} section");
        }

        // Both should have similar schema structure even if content differs
        var dndSchema = dndRoot.GetProperty("gameStateSchema");
        var pokemonSchema = pokemonRoot.GetProperty("gameStateSchema");
        
        Assert.True(dndSchema.TryGetProperty("requiredCollections", out _), 
            "D&D schema must have requiredCollections");
        Assert.True(pokemonSchema.TryGetProperty("requiredCollections", out _), 
            "Pokemon schema must have requiredCollections");
        
        Assert.True(dndSchema.TryGetProperty("playerFields", out _), 
            "D&D schema must have playerFields");
        Assert.True(pokemonSchema.TryGetProperty("playerFields", out _), 
            "Pokemon schema must have playerFields");
    }
}