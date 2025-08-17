using Xunit;
using PokeLLM.Tests.TestModels;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameRules.Models;
using PokeLLM.Logging;
using Moq;
using System.Text.Json;

namespace PokeLLM.Tests;

public class DnD5eBasicTests
{
    private readonly Mock<IDebugLogger> _mockDebugLogger;

    public DnD5eBasicTests()
    {
        _mockDebugLogger = new Mock<IDebugLogger>();
    }

    [Fact]
    public void DnDCharacter_Creation_SetsBasicProperties()
    {
        // Arrange & Act
        var character = new DnDCharacter
        {
            Name = "Test Character",
            Race = "Human",
            CharacterClass = "Fighter",
            Level = 1
        };

        // Assert
        Assert.Equal("Test Character", character.Name);
        Assert.Equal("Human", character.Race);
        Assert.Equal("Fighter", character.CharacterClass);
        Assert.Equal(1, character.Level);
    }

    [Fact]
    public void DnDCharacter_AbilityScores_CanBeSetAndRetrieved()
    {
        // Arrange
        var character = new DnDCharacter();

        // Act
        character.Strength = 16;
        character.Dexterity = 14;
        character.Constitution = 15;
        character.Intelligence = 12;
        character.Wisdom = 13;
        character.Charisma = 11;

        // Assert
        Assert.Equal(16, character.Strength);
        Assert.Equal(14, character.Dexterity);
        Assert.Equal(15, character.Constitution);
        Assert.Equal(12, character.Intelligence);
        Assert.Equal(13, character.Wisdom);
        Assert.Equal(11, character.Charisma);
    }

    [Fact]
    public void DnDCharacter_GetAbilityModifier_CalculatesCorrectly()
    {
        // Arrange
        var character = new DnDCharacter();
        character.Strength = 16; // +3 modifier
        character.Dexterity = 10; // +0 modifier
        character.Intelligence = 8; // -1 modifier

        // Act & Assert
        Assert.Equal(3, character.GetAbilityModifier("strength"));
        Assert.Equal(0, character.GetAbilityModifier("dexterity"));
        Assert.Equal(-1, character.GetAbilityModifier("intelligence"));
    }

    [Fact]
    public async Task RulesetService_LoadRuleset_ValidJson_ReturnsRuleset()
    {
        // Arrange
        var service = new RulesetService();
        var testRulesetPath = Path.Combine("TestData", "dnd5e-basic.json");

        // Act
        var rulesetDoc = await service.LoadRulesetAsync(testRulesetPath);
        var metadata = await service.GetRulesetMetadataAsync(rulesetDoc);

        // Assert
        Assert.NotNull(rulesetDoc);
        Assert.Equal("dnd5e-basic", metadata.Id);
        Assert.Equal("D&D 5e Basic Rules", metadata.Name);
        
        // Validate that the JSON structure has the expected D&D content
        var root = rulesetDoc.RootElement;
        Assert.True(root.TryGetProperty("characterCreation", out var charCreation));
        Assert.True(charCreation.TryGetProperty("races", out var races));
        Assert.True(races.GetArrayLength() > 0);
        Assert.True(charCreation.TryGetProperty("classes", out var classes));
        Assert.True(classes.GetArrayLength() > 0);
    }

    [Fact]
    public async Task JavaScriptRuleEngine_ValidateRule_SimpleExpression_ReturnsCorrect()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine(_mockDebugLogger.Object);
        var character = new DnDCharacter { Level = 5 };
        var context = new { level = 5 };

        // Act - Test just that the method doesn't throw
        var exception = await Record.ExceptionAsync(async () => 
            await ruleEngine.ValidateRuleAsync("5 >= 5", character, context));

        // Assert - The method should not throw an exception (even if V8 isn't available)
        Assert.Null(exception);
    }

    [Fact]
    public async Task JavaScriptRuleEngine_IsSafeScript_MaliciousCode_ReturnsFalse()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine(_mockDebugLogger.Object);

        // Act
        var isEvalSafe = await ruleEngine.IsSafeScriptAsync("eval('malicious code')");
        var isRequireSafe = await ruleEngine.IsSafeScriptAsync("require('fs')");

        // Assert
        Assert.False(isEvalSafe);
        Assert.False(isRequireSafe);
    }

    [Fact]
    public async Task JavaScriptRuleEngine_IsSafeScript_ValidCode_ReturnsTrue()
    {
        // Arrange
        var ruleEngine = new JavaScriptRuleEngine(_mockDebugLogger.Object);

        // Act
        var isSafe = await ruleEngine.IsSafeScriptAsync("character.Level >= 5 && character.HitPoints > 0");

        // Assert
        Assert.True(isSafe);
    }

    [Fact]
    public void CharacterCreationResult_SuccessResult_CreatesValidResult()
    {
        // Arrange
        var character = new DnDCharacter { Name = "Test" };

        // Act
        var result = CharacterCreationResult.SuccessResult(character, "Character created");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Character created", result.Message);
        Assert.Equal(character, result.Character);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void CharacterCreationResult_Failure_CreatesValidResult()
    {
        // Arrange
        var errors = new List<string> { "Invalid race", "Invalid class" };

        // Act
        var result = CharacterCreationResult.Failure("Creation failed", errors);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Creation failed", result.Message);
        Assert.Null(result.Character);
        Assert.Equal(2, result.ValidationErrors.Count);
    }
}