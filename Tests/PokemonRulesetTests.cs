using Xunit;
using PokeLLM.Tests.TestModels;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameRules.Models;
using PokeLLM.Logging;
using Moq;
using System.Text.Json;

namespace PokeLLM.Tests;

public class PokemonRulesetTests
{
    private readonly Mock<IDebugLogger> _mockDebugLogger;

    public PokemonRulesetTests()
    {
        _mockDebugLogger = new Mock<IDebugLogger>();
    }

    [Fact]
    public void PokemonTrainer_Creation_SetsBasicProperties()
    {
        // Arrange & Act
        var trainer = new PokemonTrainer
        {
            Name = "Test Trainer",
            TrainerClass = "Ace Trainer",
            Level = 1
        };

        // Assert
        Assert.Equal("Test Trainer", trainer.Name);
        Assert.Equal("Ace Trainer", trainer.TrainerClass);
        Assert.Equal(1, trainer.Level);
    }

    [Fact]
    public void PokemonTrainer_Stats_CanBeSetAndRetrieved()
    {
        // Arrange
        var trainer = new PokemonTrainer();

        // Act
        trainer.Vigor = 14;
        trainer.Dexterity = 16;
        trainer.Intelligence = 18;
        trainer.Empathy = 15;
        trainer.Intuition = 12;
        trainer.Charisma = 13;

        // Assert
        Assert.Equal(14, trainer.Vigor);
        Assert.Equal(16, trainer.Dexterity);
        Assert.Equal(18, trainer.Intelligence);
        Assert.Equal(15, trainer.Empathy);
        Assert.Equal(12, trainer.Intuition);
        Assert.Equal(13, trainer.Charisma);
    }

    [Fact]
    public void PokemonTrainer_GetStatModifier_CalculatesCorrectly()
    {
        // Arrange
        var trainer = new PokemonTrainer();
        trainer.Charisma = 16; // +3 modifier for Pokemon capture
        trainer.Dexterity = 10; // +0 modifier
        trainer.Vigor = 8;      // -1 modifier

        // Act & Assert
        Assert.Equal(3, trainer.GetStatModifier("charisma"));
        Assert.Equal(0, trainer.GetStatModifier("dexterity"));
        Assert.Equal(-1, trainer.GetStatModifier("vigor"));
    }

    [Fact]
    public void PokemonTrainer_CanCapturePokemon_RespectsTeamLimit()
    {
        // Arrange
        var trainer = new PokemonTrainer();
        
        // Act & Assert - Can capture when team is empty
        Assert.True(trainer.CanCapturePokemon());
        
        // Fill up the team
        for (int i = 0; i < 6; i++)
        {
            trainer.Pokemon.Add($"pokemon_{i}");
        }
        
        // Should not be able to capture more
        Assert.False(trainer.CanCapturePokemon());
    }

    [Fact]
    public async Task RulesetService_LoadPokemonRuleset_ValidJson_ReturnsRuleset()
    {
        // Arrange
        var service = new RulesetService();
        var testRulesetPath = Path.Combine("TestData", "pokemon-basic.json");

        // Act
        var rulesetDoc = await service.LoadRulesetAsync(testRulesetPath);
        var metadata = await service.GetRulesetMetadataAsync(rulesetDoc);

        // Assert
        Assert.NotNull(rulesetDoc);
        Assert.Equal("pokemon-basic", metadata.Id);
        Assert.Equal("Pokemon Basic Trainer Rules", metadata.Name);
        
        // Validate that the JSON structure has the expected Pokemon content
        var root = rulesetDoc.RootElement;
        Assert.True(root.TryGetProperty("pokemonSystem", out var pokemonSystem));
        Assert.True(pokemonSystem.TryGetProperty("pokemonTypes", out var types));
        Assert.True(types.GetArrayLength() > 0);
        
        Assert.True(root.TryGetProperty("characterCreation", out var charCreation));
        Assert.True(charCreation.TryGetProperty("trainerClasses", out var classes));
        Assert.True(classes.GetArrayLength() > 0);
    }

    [Fact]
    public async Task JavaScriptRuleEngine_ValidatePokemonRule_TeamLimitCheck_ReturnsCorrect()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine(_mockDebugLogger.Object);
        var trainer = new PokemonTrainer 
        { 
            Pokemon = new List<string> { "pikachu", "charizard" } // 2 Pokemon
        };
        var context = new { teamLimit = 6 };

        // Act - Test that trainer can capture more Pokemon
        var exception = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("character.Pokemon.length < 6", trainer, context));

        // Assert - The method should not throw an exception
        Assert.Null(exception);
    }

    [Fact]
    public async Task JavaScriptRuleEngine_ValidatePokemonInventory_ItemCheck_ReturnsCorrect()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine(_mockDebugLogger.Object);
        var trainer = new PokemonTrainer();
        trainer.Inventory["pokeball"] = 5;
        trainer.Inventory["potion"] = 3;
        var context = new { };

        // Act - Test inventory validation doesn't throw
        var exception = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("character.Inventory && character.Inventory['pokeball'] > 0", trainer, context));

        // Assert - The method should not throw an exception
        Assert.Null(exception);
    }

    [Fact]
    public void CharacterCreationResult_PokemonTrainer_SuccessResult_CreatesValidResult()
    {
        // Arrange
        var trainer = new PokemonTrainer { Name = "Ash", TrainerClass = "Ace Trainer" };

        // Act
        var result = CharacterCreationResult.SuccessResult(trainer, "Trainer created successfully");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Trainer created successfully", result.Message);
        Assert.Equal(trainer, result.Character);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void CharacterCreationResult_PokemonTrainer_Failure_CreatesValidResult()
    {
        // Arrange
        var errors = new List<string> { "Invalid trainer class", "No starter Pokemon selected" };

        // Act
        var result = CharacterCreationResult.Failure("Trainer creation failed", errors);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Trainer creation failed", result.Message);
        Assert.Null(result.Character);
        Assert.Equal(2, result.ValidationErrors.Count);
    }

    [Fact]
    public async Task RulesetService_ValidatePokemonRuleset_ReturnsTrue()
    {
        // Arrange
        var service = new RulesetService();
        var testRulesetPath = Path.Combine("TestData", "pokemon-basic.json");
        var rulesetDoc = await service.LoadRulesetAsync(testRulesetPath);

        // Act
        var isValid = await service.ValidateRulesetAsync(rulesetDoc);

        // Assert
        Assert.True(isValid);
    }
}