using Xunit;
using PokeLLM.Tests.TestModels;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameRules.Models;
using System.Text.Json;

namespace PokeLLM.Tests;

/// <summary>
/// Tests to verify the dynamic rule engine can handle multiple different rulesets
/// </summary>
public class MultiRulesetTests
{
    [Fact]
    public async Task GenericRuleEngine_CanLoadBothDnDAndPokemonRulesets()
    {
        // Arrange
        var service = new RulesetService();

        // Act - Load both rulesets
        var dndRuleset = await service.LoadRulesetAsync(Path.Combine("TestData", "dnd5e-basic.json"));
        var pokemonRuleset = await service.LoadRulesetAsync(Path.Combine("TestData", "pokemon-basic.json"));

        var dndMetadata = await service.GetRulesetMetadataAsync(dndRuleset);
        var pokemonMetadata = await service.GetRulesetMetadataAsync(pokemonRuleset);

        // Assert - Both rulesets loaded successfully with different metadata
        Assert.NotNull(dndRuleset);
        Assert.NotNull(pokemonRuleset);
        
        Assert.Equal("dnd5e-basic", dndMetadata.Id);
        Assert.Equal("pokemon-basic", pokemonMetadata.Id);
        
        Assert.Equal("D&D 5e Basic Rules", dndMetadata.Name);
        Assert.Equal("Pokemon Basic Trainer Rules", pokemonMetadata.Name);
        
        Assert.Contains("dnd", dndMetadata.Tags);
        Assert.Contains("pokemon", pokemonMetadata.Tags);
    }

    [Fact]
    public async Task GenericRuleEngine_ValidatesCharactersFromDifferentRulesets()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine();
        
        // D&D Character
        var dndCharacter = new DnDCharacter { Level = 3, Strength = 16 };
        var dndContext = new { level = 3 };
        
        // Pokemon Trainer
        var pokemonTrainer = new PokemonTrainer { Level = 5, Charisma = 14 };
        pokemonTrainer.Pokemon.Add("pikachu");
        var pokemonContext = new { teamSize = 1 };

        // Act & Assert - Both character types can be validated
        var dndException = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("character.Level >= 1", dndCharacter, dndContext));
        var pokemonException = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("character.Level >= 1", pokemonTrainer, pokemonContext));

        Assert.Null(dndException);
        Assert.Null(pokemonException);
    }

    [Fact]
    public async Task GenericRuleEngine_HandlesRulesetSpecificValidation()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine();
        
        var dndCharacter = new DnDCharacter();
        dndCharacter.KnownSpells.Add("fireball");
        dndCharacter.SpellSlots[3] = 2; // 2 third level spell slots
        
        var pokemonTrainer = new PokemonTrainer();
        pokemonTrainer.Inventory["pokeball"] = 5;
        pokemonTrainer.Pokemon.Add("bulbasaur");

        // Act & Assert - Rule engine handles different property structures
        var dndSpellException = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("character.KnownSpells && character.KnownSpells.length > 0", dndCharacter, new {}));
        var pokemonItemException = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("character.Inventory && character.Pokemon.length > 0", pokemonTrainer, new {}));

        Assert.Null(dndSpellException);
        Assert.Null(pokemonItemException);
    }

    [Fact]
    public void CharacterCreationResult_WorksWithDifferentCharacterTypes()
    {
        // Arrange
        var dndCharacter = new DnDCharacter { Name = "Gandalf", CharacterClass = "Wizard" };
        var pokemonTrainer = new PokemonTrainer { Name = "Ash", TrainerClass = "Ace Trainer" };

        // Act
        var dndResult = CharacterCreationResult.SuccessResult(dndCharacter, "Wizard created");
        var pokemonResult = CharacterCreationResult.SuccessResult(pokemonTrainer, "Trainer created");

        // Assert - Same result type works with different character models
        Assert.True(dndResult.Success);
        Assert.True(pokemonResult.Success);
        Assert.Equal(dndCharacter, dndResult.Character);
        Assert.Equal(pokemonTrainer, pokemonResult.Character);
    }

    [Fact]
    public async Task RulesetValidation_BothRulesetsAreValid()
    {
        // Arrange
        var service = new RulesetService();

        // Act
        var dndRuleset = await service.LoadRulesetAsync(Path.Combine("TestData", "dnd5e-basic.json"));
        var pokemonRuleset = await service.LoadRulesetAsync(Path.Combine("TestData", "pokemon-basic.json"));

        var dndValid = await service.ValidateRulesetAsync(dndRuleset);
        var pokemonValid = await service.ValidateRulesetAsync(pokemonRuleset);

        // Assert - Both rulesets pass generic validation
        Assert.True(dndValid);
        Assert.True(pokemonValid);
    }

    [Fact]
    public void DifferentRulesets_HaveDifferentStatSystems()
    {
        // Arrange
        var dndCharacter = new DnDCharacter { Strength = 16, Dexterity = 14, Intelligence = 12 };
        var pokemonTrainer = new PokemonTrainer { Vigor = 16, Empathy = 14, Intelligence = 12 };

        // Act & Assert - Different stat systems both work with modifier calculations
        
        // D&D uses standard 6 abilities
        Assert.Equal(3, dndCharacter.GetAbilityModifier("strength")); // 16 -> +3
        Assert.Equal(2, dndCharacter.GetAbilityModifier("dexterity")); // 14 -> +2
        Assert.Equal(1, dndCharacter.GetAbilityModifier("intelligence")); // 12 -> +1
        
        // Pokemon uses trainer-focused stats
        Assert.Equal(3, pokemonTrainer.GetStatModifier("vigor")); // 16 -> +3
        Assert.Equal(2, pokemonTrainer.GetStatModifier("empathy")); // 14 -> +2
        Assert.Equal(1, pokemonTrainer.GetStatModifier("intelligence")); // 12 -> +1
        
        // Both use same underlying formula but different stat names
        Assert.Equal(dndCharacter.GetAbilityModifier("strength"), pokemonTrainer.GetStatModifier("vigor"));
    }

    [Fact]
    public void DifferentRulesets_HaveDifferentGameConcepts()
    {
        // Arrange
        var dndCharacter = new DnDCharacter();
        dndCharacter.KnownSpells.Add("magic_missile");
        dndCharacter.SpellSlots[1] = 2; // Add spell slots for level 1 spells
        dndCharacter.Equipment.Add(new() { Name = "Longsword", Quantity = 1 });
        
        var pokemonTrainer = new PokemonTrainer();
        pokemonTrainer.Pokemon.Add("pikachu");
        pokemonTrainer.Badges.Add("boulder_badge");

        // Act & Assert - Each ruleset has unique concepts
        
        // D&D has spells and equipment
        Assert.True(dndCharacter.KnownSpells.Count > 0);
        Assert.True(dndCharacter.Equipment.Count > 0);
        Assert.True(dndCharacter.CanCastSpell("magic_missile", 1));
        
        // Pokemon has Pokemon and badges
        Assert.True(pokemonTrainer.Pokemon.Count > 0);
        Assert.True(pokemonTrainer.Badges.Count > 0);
        Assert.True(pokemonTrainer.CanCapturePokemon()); // Only 1 Pokemon, can capture more
    }
}