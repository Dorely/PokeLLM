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
/// Tests for dynamic function loading from rulesets into the Semantic Kernel
/// </summary>
public class DynamicFunctionLoadingTests
{
    private readonly Mock<IJavaScriptRuleEngine> _mockJsEngine;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IRulesetService> _mockRulesetService;
    private readonly DynamicFunctionFactory _functionFactory;

    public DynamicFunctionLoadingTests()
    {
        _mockJsEngine = new Mock<IJavaScriptRuleEngine>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockRulesetService = new Mock<IRulesetService>();
        
        _functionFactory = new DynamicFunctionFactory(_mockJsEngine.Object, _mockServiceProvider.Object);
    }

    [Fact]
    public async Task LoadPokemonAdventureRuleset_GameSetupPhase_AllFunctionsAvailable()
    {
        // Arrange - Load the actual pokemon-adventure.json ruleset
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
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
        Assert.Contains("finalize_game_setup", functionNames);
        
        // Verify we have exactly the expected number of functions
        Assert.Equal(4, functionList.Count);
    }

    [Fact]
    public async Task RulesetManager_LoadsPokemonGameSetupFunctions_IntoKernel()
    {
        // Arrange - Setup real ruleset manager with pokemon ruleset
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetManager = new RulesetManager(_mockRulesetService.Object, _functionFactory);
        await rulesetManager.LoadRulesetAsync("pokemon-adventure");
        await rulesetManager.SetActiveRulesetAsync("pokemon-adventure");

        // Act - Get functions for GameSetup phase
        var functions = await rulesetManager.GetPhaseFunctionsAsync(GamePhase.GameSetup);

        // Assert - Verify functions are available
        var functionList = functions.ToList();
        var functionNames = functionList.Select(f => f.Name).ToList();

        Assert.NotEmpty(functionList);
        Assert.Contains("select_trainer_class", functionNames);
        Assert.Contains("choose_starter_pokemon", functionNames);
        Assert.Contains("set_trainer_name", functionNames);
        Assert.Contains("finalize_game_setup", functionNames);
    }

    [Fact]
    public async Task KernelWithRulesetFunctions_CanInvokeGameSetupFunctions()
    {
        // Arrange - Create kernel and load ruleset functions
        var kernelBuilder = Kernel.CreateBuilder();
        var kernel = kernelBuilder.Build();

        var rulesetManager = new RulesetManager(_mockRulesetService.Object, _functionFactory);
        await rulesetManager.LoadRulesetAsync("pokemon-adventure");
        await rulesetManager.SetActiveRulesetAsync("pokemon-adventure");

        var functions = await rulesetManager.GetPhaseFunctionsAsync(GamePhase.GameSetup);
        
        // Add functions to kernel - create a single plugin with all functions
        kernel.Plugins.AddFromFunctions("GameSetup", functions);

        // Act & Assert - Verify each function is available in the kernel
        var gameSetupPlugin = kernel.Plugins["GameSetup"];
        Assert.NotNull(gameSetupPlugin);

        var availableFunctions = gameSetupPlugin.Select(f => f.Name).ToList();
        Assert.Contains("select_trainer_class", availableFunctions);
        Assert.Contains("choose_starter_pokemon", availableFunctions);
        Assert.Contains("set_trainer_name", availableFunctions);
        Assert.Contains("finalize_game_setup", availableFunctions);
    }

    [Fact]
    public async Task FinalizeGameSetupFunction_HasCorrectParameters()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.GameSetup);
        var finalizeFunction = functions.FirstOrDefault(f => f.Name == "finalize_game_setup");

        // Assert
        Assert.NotNull(finalizeFunction);
        Assert.Equal("finalize_game_setup", finalizeFunction.Name);
        Assert.Equal("Complete game setup and transition to World Generation phase", finalizeFunction.Description);
        
        var parameters = finalizeFunction.Metadata.Parameters;
        Assert.Single(parameters);
        Assert.Equal("setupSummary", parameters.First().Name);
        Assert.True(parameters.First().IsRequired);
    }

    [Fact]
    public async Task GameSetupFunctions_HaveValidStructure()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, GamePhase.GameSetup);

        // Assert - Each function should have valid metadata
        foreach (var function in functions)
        {
            Assert.NotNull(function.Name);
            Assert.NotEmpty(function.Name);
            Assert.NotNull(function.Description);
            Assert.NotEmpty(function.Description);
            Assert.NotNull(function.Metadata);
            Assert.NotNull(function.Metadata.Parameters);
        }
    }

    [Fact]
    public async Task LoadRulesetFunctions_WithInvalidPhase_ReturnsEmpty()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act - Try to load functions for a non-existent phase
        var functions = await _functionFactory.GenerateFunctionsFromRulesetAsync(ruleset, (GamePhase)999);

        // Assert
        Assert.Empty(functions);
    }

    [Fact]
    public async Task GetFunctionsForPhase_GameSetup_ReturnsCorrectFunctionDefinitions()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functionDefinitions = await _functionFactory.GetFunctionsForPhaseAsync(ruleset, GamePhase.GameSetup);

        // Assert
        Assert.Equal(4, functionDefinitions.Count);
        
        var functionIds = functionDefinitions.Select(f => f.Id).ToList();
        Assert.Contains("select_trainer_class", functionIds);
        Assert.Contains("choose_starter_pokemon", functionIds);
        Assert.Contains("set_trainer_name", functionIds);
        Assert.Contains("finalize_game_setup", functionIds);

        // Verify each function has the required properties
        foreach (var func in functionDefinitions)
        {
            Assert.NotNull(func.Id);
            Assert.NotNull(func.Name);
            Assert.NotNull(func.Description);
            Assert.NotNull(func.Parameters);
            Assert.NotNull(func.RuleValidations);
            Assert.NotNull(func.Effects);
        }
    }

    [Fact]
    public async Task FinalizeGameSetupFunction_HasCorrectRuleValidations()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functionDefinitions = await _functionFactory.GetFunctionsForPhaseAsync(ruleset, GamePhase.GameSetup);
        var finalizeFunction = functionDefinitions.FirstOrDefault(f => f.Id == "finalize_game_setup");

        // Assert
        Assert.NotNull(finalizeFunction);
        Assert.Equal(2, finalizeFunction.RuleValidations.Count);
        Assert.Contains("character.name != null && character.name != ''", finalizeFunction.RuleValidations);
        Assert.Contains("gameState.region != null && gameState.region != ''", finalizeFunction.RuleValidations);
    }

    [Fact]
    public async Task FinalizeGameSetupFunction_HasCorrectEffects()
    {
        // Arrange
        var rulesetPath = Path.Combine("Rulesets", "pokemon-adventure.json");
        var rulesetJson = await File.ReadAllTextAsync(rulesetPath);
        var ruleset = JsonDocument.Parse(rulesetJson);

        // Act
        var functionDefinitions = await _functionFactory.GetFunctionsForPhaseAsync(ruleset, GamePhase.GameSetup);
        var finalizeFunction = functionDefinitions.FirstOrDefault(f => f.Id == "finalize_game_setup");

        // Assert
        Assert.NotNull(finalizeFunction);
        Assert.Equal(3, finalizeFunction.Effects.Count);
        
        var effects = finalizeFunction.Effects;
        
        // Check phase transition effect
        var phaseEffect = effects.FirstOrDefault(e => e.Target == "gameState.currentPhase");
        Assert.NotNull(phaseEffect);
        Assert.Equal("set", phaseEffect.Operation);
        
        // Handle potential JsonElement or string value
        var phaseValue = phaseEffect.Value;
        if (phaseValue is JsonElement jsonElement)
        {
            Assert.Equal("WorldGeneration", jsonElement.GetString());
        }
        else
        {
            Assert.Equal("WorldGeneration", phaseValue?.ToString());
        }
        
        // Check adventure summary effect
        var summaryEffect = effects.FirstOrDefault(e => e.Target == "gameState.adventureSummary");
        Assert.NotNull(summaryEffect);
        Assert.Equal("set", summaryEffect.Operation);
        
        // Check phase change summary effect
        var changeEffect = effects.FirstOrDefault(e => e.Target == "gameState.phaseChangeSummary");
        Assert.NotNull(changeEffect);
        Assert.Equal("set", changeEffect.Operation);
    }
}