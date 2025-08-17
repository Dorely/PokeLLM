using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Moq;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameRules.Services;
using PokeLLM.GameState.Models;
using System.Text.Json;
using Xunit;

namespace PokeLLM.Tests;

/// <summary>
/// Integration tests for dynamic function loading and execution from rulesets
/// </summary>
public class DynamicFunctionIntegrationTests
{
    private readonly Mock<IJavaScriptRuleEngine> _mockJsEngine;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly DynamicFunctionFactory _functionFactory;

    public DynamicFunctionIntegrationTests()
    {
        _mockJsEngine = new Mock<IJavaScriptRuleEngine>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        
        _functionFactory = new DynamicFunctionFactory(_mockJsEngine.Object, _mockServiceProvider.Object);
    }

    [Fact]
    public async Task LoadPokemonAdventureRuleset_GameSetupPhase_DeserializesCorrectly()
    {
        // Arrange - Load the actual pokemon-adventure.json ruleset
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        Assert.True(File.Exists(rulesetPath), $"Ruleset file not found at {rulesetPath}");
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act - Generate functions for GameSetup phase
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.GameSetup);

        // Assert - Verify all expected GameSetup functions are present
        var functionList = functions.ToList();
        var functionNames = functionList.Select(f => f.Name).ToList();

        Assert.NotEmpty(functionList);
        Assert.Contains("select_trainer_class", functionNames);
        Assert.Contains("choose_starter_pokemon", functionNames);
        Assert.Contains("set_trainer_name", functionNames);
        Assert.Contains("select_region", functionNames);
        Assert.Contains("validate_setup_completion", functionNames);
        
        // Verify we have exactly the expected number of functions
        Assert.Equal(5, functionList.Count);
        
        // Verify each function has proper metadata
        foreach (var function in functionList)
        {
            Assert.NotNull(function.Name);
            Assert.NotEmpty(function.Name);
            Assert.NotNull(function.Description);
            Assert.NotEmpty(function.Description);
        }
    }

    [Fact]
    public async Task LoadPokemonAdventureRuleset_ExplorationPhase_DeserializesCorrectly()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.Exploration);

        // Assert
        var functionList = functions.ToList();
        var functionNames = functionList.Select(f => f.Name).ToList();

        Assert.NotEmpty(functionList);
        Assert.Contains("attempt_capture", functionNames);
        Assert.Contains("use_item", functionNames);
        Assert.Contains("rest_at_pokemon_center", functionNames);
        Assert.Contains("search_area", functionNames);
        
        Assert.Equal(4, functionList.Count);
    }

    [Fact]
    public async Task LoadPokemonAdventureRuleset_CombatPhase_DeserializesCorrectly()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.Combat);

        // Assert
        var functionList = functions.ToList();
        var functionNames = functionList.Select(f => f.Name).ToList();

        Assert.NotEmpty(functionList);
        Assert.Contains("use_move", functionNames);
        Assert.Contains("switch_pokemon", functionNames);
        Assert.Contains("use_item_in_battle", functionNames);
        Assert.Contains("flee_battle", functionNames);
        
        Assert.Equal(4, functionList.Count);
    }

    [Fact]
    public async Task LoadPokemonAdventureRuleset_LevelUpPhase_DeserializesCorrectly()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.LevelUp);

        // Assert
        var functionList = functions.ToList();
        var functionNames = functionList.Select(f => f.Name).ToList();

        Assert.NotEmpty(functionList);
        Assert.Contains("level_up_pokemon", functionNames);
        Assert.Contains("learn_move", functionNames);
        Assert.Contains("evolve_pokemon", functionNames);
        Assert.Contains("upgrade_trainer_skills", functionNames);
        
        Assert.Equal(4, functionList.Count);
    }

    [Fact]
    public async Task LoadPokemonAdventureRuleset_WorldGenerationPhase_DeserializesCorrectly()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.WorldGeneration);

        // Assert
        var functionList = functions.ToList();
        var functionNames = functionList.Select(f => f.Name).ToList();

        Assert.NotEmpty(functionList);
        
        // Original functions
        Assert.Contains("generate_wild_pokemon", functionNames);
        Assert.Contains("create_gym", functionNames);
        Assert.Contains("generate_route", functionNames);
        
        // New functions added to replace plugin functions
        Assert.Contains("create_trainer", functionNames);
        Assert.Contains("create_pokemon", functionNames);
        Assert.Contains("create_location", functionNames);
        Assert.Contains("assign_pokemon_to_trainer", functionNames);
        
        Assert.Equal(7, functionList.Count);
    }

    [Fact]
    public async Task CreateRulesetFunction_ChooseStarterPokemon_CreatesValidKernelFunction()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);
        
        var functionDefinitions = await _functionFactory.GetFunctionsForPhaseAsync(ruleset, GamePhase.GameSetup);
        var chooseStarterDef = functionDefinitions.FirstOrDefault(f => f.Id == "choose_starter_pokemon");
        
        Assert.NotNull(chooseStarterDef);

        // Act
        var kernelFunction = await _functionFactory.CreateRulesetFunctionAsync(chooseStarterDef);

        // Assert
        Assert.NotNull(kernelFunction);
        Assert.Equal("choose_starter_pokemon", kernelFunction.Name);
        Assert.Equal("Choose a starter Pokemon from available options", kernelFunction.Description);
        
        // Verify parameters
        var parameters = kernelFunction.Metadata.Parameters;
        Assert.Equal(2, parameters.Count);
        
        var speciesIdParam = parameters.FirstOrDefault(p => p.Name == "speciesId");
        Assert.NotNull(speciesIdParam);
        Assert.True(speciesIdParam.IsRequired);
        Assert.Equal("Species ID of the starter Pokemon", speciesIdParam.Description);
        
        var nicknameParam = parameters.FirstOrDefault(p => p.Name == "nickname");
        Assert.NotNull(nicknameParam);
        Assert.False(nicknameParam.IsRequired);
        Assert.Equal("Optional nickname for the Pokemon", nicknameParam.Description);
    }

    [Fact]
    public async Task GetFunctionsForPhaseAsync_InvalidPhase_ReturnsEmptyList()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        if (!File.Exists(rulesetPath))
        {
            rulesetPath = Path.Combine("PokeLLM", "Rulesets", "pokemon-adventure.json");
        }
        
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act - Try to get functions for a non-existent phase
        var functions = await _functionFactory.GetFunctionsForPhaseAsync(ruleset, (GamePhase)999);

        // Assert
        Assert.Empty(functions);
    }

    [Fact]
    public async Task GetFunctionsForPhaseAsync_EmptyRuleset_ReturnsEmptyList()
    {
        // Arrange
        var emptyRulesetJson = "{}";
        var ruleset = JsonDocument.Parse(emptyRulesetJson);

        // Act
        var functions = await _functionFactory.GetFunctionsForPhaseAsync(ruleset, GamePhase.GameSetup);

        // Assert
        Assert.Empty(functions);
    }
}