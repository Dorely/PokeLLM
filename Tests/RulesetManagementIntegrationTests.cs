using Microsoft.Extensions.DependencyInjection;
using Moq;
using PokeLLM.Game.Plugins;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;
using PokeLLM.Tests.TestUtilities;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace PokeLLM.Tests;

/// <summary>
/// Integration tests for RulesetManagementPlugin covering end-to-end workflows and system interactions
/// </summary>
public class RulesetManagementIntegrationTests : IDisposable
{
    private readonly Mock<IRulesetManager> _mockRulesetManager;
    private readonly Mock<IJavaScriptRuleEngine> _mockJsEngine;
    private readonly Mock<IGameStateRepository> _mockGameStateRepo;
    private readonly RulesetManagementPlugin _plugin;
    private readonly string _testRulesetsDirectory;
    private readonly GameStateModel _testGameState;

    public RulesetManagementIntegrationTests()
    {
        // Setup test directory
        _testRulesetsDirectory = Path.Combine(Path.GetTempPath(), "IntegrationTestRulesets", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRulesetsDirectory);

        // Create mocks with realistic behavior
        _mockRulesetManager = new Mock<IRulesetManager>();
        _mockJsEngine = new Mock<IJavaScriptRuleEngine>();
        _mockGameStateRepo = new Mock<IGameStateRepository>();

        // Setup test game state
        _testGameState = new GameStateModel
        {
            SessionId = Guid.NewGuid().ToString(),
            ActiveRulesetId = "pokemon-adventure",
            GameTurnNumber = 5,
            LastSaveTime = DateTime.UtcNow,
            RecentEvents = new List<PokeLLM.GameState.Models.EventLog>()
        };

        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ReturnsAsync(_testGameState);
        
        _mockGameStateRepo.Setup(x => x.SaveStateAsync(It.IsAny<GameStateModel>()))
            .Returns(Task.CompletedTask);

        // Setup JavaScript engine with realistic behavior
        _mockJsEngine.Setup(x => x.IsSafeScriptAsync(It.IsAny<string>()))
            .ReturnsAsync((string script) => !script.Contains("eval") && !script.Contains("Function"));
        
        _mockJsEngine.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync((string script, Dictionary<string, object> vars) => 
            {
                if (script.Contains("character.level")) return vars.ContainsKey("character");
                return true;
            });

        // Setup ruleset manager with realistic behavior
        _mockRulesetManager.Setup(x => x.LoadRulesetAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) =>
            {
                var ruleset = $@"{{
                    ""metadata"": {{ ""id"": ""{id}"", ""name"": ""Loaded {id}"", ""version"": ""1.0.0"" }},
                    ""functionDefinitions"": {{}},
                    ""promptTemplates"": {{}}
                }}";
                return JsonDocument.Parse(ruleset);
            });
        
        _mockRulesetManager.Setup(x => x.SetActiveRulesetAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Create plugin with test directory
        _plugin = new TestableRulesetManagementPlugin(
            _mockRulesetManager.Object,
            _mockJsEngine.Object,
            _mockGameStateRepo.Object,
            _testRulesetsDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRulesetsDirectory))
        {
            Directory.Delete(_testRulesetsDirectory, true);
        }
    }

    #region End-to-End Workflow Tests

    [Fact]
    public async Task CompleteRulesetCreationWorkflow_CreatesAndActivatesSuccessfully()
    {
        // Act & Assert - Step 1: Create new ruleset
        var createResult = await _plugin.CreateNewRuleset(
            "custom-adventure", 
            "Custom Adventure Ruleset",
            "2.0.0",
            "A custom adventure ruleset with unique mechanics",
            "Integration Test Author",
            "integration, test, custom");

        var createJson = JsonSerializer.Deserialize<JsonElement>(createResult);
        Assert.True(createJson.GetProperty("success").GetBoolean());

        // Step 2: Add multiple functions across different phases
        var explorationResult = await _plugin.AddFunctionToRuleset(
            "custom-adventure", "Exploration", "custom_explore", "customExplore",
            "Custom exploration function",
            @"[{""name"": ""direction"", ""type"": ""string"", ""description"": ""Direction to explore""}]",
            @"[""return direction && direction.length > 0;""]",
            @"[{""action"": ""updateLocation"", ""parameters"": [""direction""]}]");

        var explorationJson = JsonSerializer.Deserialize<JsonElement>(explorationResult);
        Assert.True(explorationJson.GetProperty("success").GetBoolean());

        var combatResult = await _plugin.AddFunctionToRuleset(
            "custom-adventure", "Combat", "custom_attack", "customAttack",
            "Custom attack function",
            @"[{""name"": ""move"", ""type"": ""string""}, {""name"": ""target"", ""type"": ""string""}]",
            @"[""return move && target;"", ""return character.level >= 5;""]",
            @"[{""action"": ""dealDamage"", ""target"": ""target"", ""amount"": ""move.power""}]");

        var combatJson = JsonSerializer.Deserialize<JsonElement>(combatResult);
        Assert.True(combatJson.GetProperty("success").GetBoolean());

        // Step 3: Add game data
        var gameDataResult = await _plugin.UpdateGameData(
            "custom-adventure", "customMoves",
            @"[
                {""name"": ""Lightning Strike"", ""power"": 85, ""type"": ""electric""},
                {""name"": ""Shadow Punch"", ""power"": 60, ""type"": ""dark""}
            ]");

        var gameDataJson = JsonSerializer.Deserialize<JsonElement>(gameDataResult);
        Assert.True(gameDataJson.GetProperty("success").GetBoolean());

        // Step 4: Validate the complete ruleset
        var validationResult = await _plugin.ValidateRuleset("custom-adventure", true);
        var validationJson = JsonSerializer.Deserialize<JsonElement>(validationResult);
        Assert.True(validationJson.GetProperty("valid").GetBoolean());

        // Step 5: Switch to the new ruleset
        var switchResult = await _plugin.SwitchActiveRuleset("custom-adventure", true);
        var switchJson = JsonSerializer.Deserialize<JsonElement>(switchResult);
        Assert.True(switchJson.GetProperty("success").GetBoolean());
        Assert.Equal("pokemon-adventure", switchJson.GetProperty("previousRulesetId").GetString());
        Assert.Equal("custom-adventure", switchJson.GetProperty("newRulesetId").GetString());

        // Step 6: Verify the complete state
        var detailsResult = await _plugin.GetRulesetDetails("custom-adventure", true, true);
        var detailsJson = JsonSerializer.Deserialize<JsonElement>(detailsResult);
        
        Assert.True(detailsJson.GetProperty("isActive").GetBoolean());
        
        var functionDefs = detailsJson.GetProperty("functionDefinitions");
        Assert.Equal(1, functionDefs.GetProperty("Exploration").GetArrayLength());
        Assert.Equal(1, functionDefs.GetProperty("Combat").GetArrayLength());
        
        // Verify total function count by counting them
        var totalFunctions = functionDefs.GetProperty("Exploration").GetArrayLength() + 
                           functionDefs.GetProperty("Combat").GetArrayLength();
        Assert.Equal(2, totalFunctions);
        
        var gameData = detailsJson.GetProperty("gameData");
        Assert.True(gameData.TryGetProperty("customMoves", out var moves));
        Assert.Equal(2, moves.GetArrayLength());

        // Verify manager interactions
        _mockRulesetManager.Verify(x => x.LoadRulesetAsync("custom-adventure"), Times.AtLeastOnce());
        _mockRulesetManager.Verify(x => x.SetActiveRulesetAsync("custom-adventure"), Times.Once());
        _mockGameStateRepo.Verify(x => x.SaveStateAsync(It.IsAny<GameStateModel>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task RulesetMigrationWorkflow_PreservesGameStateIntegrity()
    {
        // Arrange - Create source and target rulesets
        await CreateCompleteRuleset("source-ruleset");
        await CreateCompleteRuleset("target-ruleset");

        var originalTurnNumber = _testGameState.GameTurnNumber;
        var originalEvents = new List<PokeLLM.GameState.Models.EventLog>(_testGameState.RecentEvents);

        // Act - Switch rulesets multiple times
        await _plugin.SwitchActiveRuleset("source-ruleset", false);
        await _plugin.SwitchActiveRuleset("target-ruleset", false);
        await _plugin.SwitchActiveRuleset("source-ruleset", false);

        // Assert - Verify game state integrity
        _mockGameStateRepo.Verify(x => x.SaveStateAsync(It.IsAny<GameStateModel>()), Times.AtLeast(3));
        
        // Verify that each save maintained proper state structure
        _mockGameStateRepo.Verify(x => x.SaveStateAsync(It.Is<GameStateModel>(gs => 
            gs.SessionId == _testGameState.SessionId &&
            gs.GameTurnNumber >= originalTurnNumber &&
            gs.RecentEvents.Count > originalEvents.Count)), Times.AtLeast(3));
    }

    [Fact]
    public async Task LargeRulesetManagement_HandlesComplexOperationsEfficiently()
    {
        // Arrange - Create a large, complex ruleset
        var rulesetId = "large-complex-ruleset";
        var stopwatch = Stopwatch.StartNew();

        // Act - Create ruleset with extensive content
        await _plugin.CreateNewRuleset(rulesetId, "Large Complex Ruleset", "1.0.0", 
            "A large ruleset for performance testing");

        // Add many functions across all phases
        var phases = new[] { "GameSetup", "WorldGeneration", "Exploration", "Combat", "LevelUp" };
        var totalFunctions = 0;

        foreach (var phase in phases)
        {
            for (int i = 0; i < 20; i++) // 20 functions per phase = 100 total
            {
                await _plugin.AddFunctionToRuleset(
                    rulesetId, phase, $"{phase.ToLower()}-func-{i}", $"{phase}Function{i}",
                    $"Function {i} for {phase} phase",
                    @"[{""name"": ""param1"", ""type"": ""string""}, {""name"": ""param2"", ""type"": ""number""}]",
                    @"[""return param1 && param2 > 0;"", ""return character.level >= 1;""]",
                    @"[{""action"": ""log"", ""message"": ""Function executed""}]");
                totalFunctions++;
            }
        }

        // Add multiple game data sections
        var dataSections = new[]
        {
            ("pokemonSpecies", GeneratePokemonData()),
            ("moves", GenerateMovesData()),
            ("items", GenerateItemsData()),
            ("locations", GenerateLocationsData())
        };

        foreach (var (section, data) in dataSections)
        {
            await _plugin.UpdateGameData(rulesetId, section, data);
        }

        // Validate the complete ruleset
        var validationResult = await _plugin.ValidateRuleset(rulesetId, true);

        stopwatch.Stop();

        // Assert - Performance and correctness
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, 
            $"Large ruleset operations took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");

        var validationJson = JsonSerializer.Deserialize<JsonElement>(validationResult);
        Assert.True(validationJson.GetProperty("valid").GetBoolean());

        // Verify content integrity
        var detailsResult = await _plugin.GetRulesetDetails(rulesetId, false, true);
        var detailsJson = JsonSerializer.Deserialize<JsonElement>(detailsResult);
        
        Assert.Equal(totalFunctions, detailsJson.GetProperty("totalFunctions").GetInt32());
        
        var gameData = detailsJson.GetProperty("gameData");
        Assert.True(gameData.TryGetProperty("pokemonSpecies", out _));
        Assert.True(gameData.TryGetProperty("moves", out _));
        Assert.True(gameData.TryGetProperty("items", out _));
        Assert.True(gameData.TryGetProperty("locations", out _));

        // Verify JavaScript validation was called for all rules
        _mockJsEngine.Verify(x => x.IsSafeScriptAsync(It.IsAny<string>()), Times.AtLeast(totalFunctions * 2));
    }

    [Fact]
    public async Task ConcurrentRulesetOperations_HandlesThreadSafety()
    {
        // Arrange
        var rulesetIds = Enumerable.Range(0, 5).Select(i => $"concurrent-test-{i}").ToArray();
        var tasks = new List<Task<string>>();

        // Act - Perform concurrent operations
        foreach (var rulesetId in rulesetIds)
        {
            tasks.Add(_plugin.CreateNewRuleset(rulesetId, $"Concurrent Test {rulesetId}", "1.0.0", "Concurrent test"));
        }

        var createResults = await Task.WhenAll(tasks);

        // Add functions concurrently
        var functionTasks = new List<Task<string>>();
        foreach (var rulesetId in rulesetIds)
        {
            for (int i = 0; i < 3; i++)
            {
                var funcId = $"func-{i}";
                functionTasks.Add(_plugin.AddFunctionToRuleset(
                    rulesetId, "Exploration", funcId, $"concurrentFunc{i}", "Concurrent function"));
            }
        }

        var functionResults = await Task.WhenAll(functionTasks);

        // Assert - All operations succeeded
        foreach (var result in createResults)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(result);
            Assert.True(json.GetProperty("success").GetBoolean());
        }

        foreach (var result in functionResults)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(result);
            Assert.True(json.GetProperty("success").GetBoolean());
        }

        // Verify all rulesets exist and have correct function counts
        var listResult = await _plugin.ListAvailableRulesets(true);
        var listJson = JsonSerializer.Deserialize<JsonElement>(listResult);
        
        Assert.Equal(rulesetIds.Length, listJson.GetProperty("totalRulesets").GetInt32());
        
        var rulesets = listJson.GetProperty("rulesets");
        foreach (var ruleset in rulesets.EnumerateArray())
        {
            var functionCounts = ruleset.GetProperty("functionCounts");
            Assert.Equal(3, functionCounts.GetProperty("Exploration").GetInt32());
        }
    }

    [Fact]
    public async Task ErrorRecoveryWorkflow_HandlesPartialFailuresGracefully()
    {
        // Arrange
        var rulesetId = "error-recovery-test";
        await _plugin.CreateNewRuleset(rulesetId, "Error Recovery Test", "1.0.0", "Test error recovery");

        // Setup JavaScript engine to fail on specific patterns
        _mockJsEngine.Setup(x => x.IsSafeScriptAsync(It.Is<string>(s => s.Contains("unsafe_function"))))
            .ReturnsAsync(false);

        // Act & Assert - Add valid function
        var validResult = await _plugin.AddFunctionToRuleset(
            rulesetId, "Exploration", "valid-func", "validFunc", "Valid function",
            "[]", @"[""return true;""]", "[]");
        
        var validJson = JsonSerializer.Deserialize<JsonElement>(validResult);
        Assert.True(validJson.GetProperty("success").GetBoolean());

        // Try to add invalid function
        var invalidResult = await _plugin.AddFunctionToRuleset(
            rulesetId, "Exploration", "invalid-func", "invalidFunc", "Invalid function",
            "[]", @"[""unsafe_function();""]", "[]");
        
        var invalidJson = JsonSerializer.Deserialize<JsonElement>(invalidResult);
        Assert.True(invalidJson.GetProperty("success").GetBoolean()); // Function is added but validation will fail

        // Validate ruleset - should fail due to unsafe function
        var validationResult = await _plugin.ValidateRuleset(rulesetId, true);
        var validationJson = JsonSerializer.Deserialize<JsonElement>(validationResult);
        Assert.False(validationJson.GetProperty("valid").GetBoolean());

        // Remove the invalid function by replacing it with a valid one
        var fixResult = await _plugin.AddFunctionToRuleset(
            rulesetId, "Exploration", "invalid-func", "fixedFunc", "Fixed function",
            "[]", @"[""return true;""]", "[]");
        
        var fixJson = JsonSerializer.Deserialize<JsonElement>(fixResult);
        Assert.True(fixJson.GetProperty("success").GetBoolean());

        // Validate again - should now pass
        var finalValidationResult = await _plugin.ValidateRuleset(rulesetId, true);
        var finalValidationJson = JsonSerializer.Deserialize<JsonElement>(finalValidationResult);
        Assert.True(finalValidationJson.GetProperty("valid").GetBoolean());

        // Verify final state
        var detailsResult = await _plugin.GetRulesetDetails(rulesetId, true, false);
        var detailsJson = JsonSerializer.Deserialize<JsonElement>(detailsResult);
        
        var functions = detailsJson.GetProperty("functionDefinitions").GetProperty("Exploration");
        Assert.Equal(2, functions.GetArrayLength());
        
        // Verify the fixed function has the correct name
        var fixedFunction = functions.EnumerateArray()
            .FirstOrDefault(f => f.GetProperty("id").GetString() == "invalid-func");
        Assert.Equal("fixedFunc", fixedFunction.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PluginIntegrationWorkflow_SimulatesRealGameUsage()
    {
        // This test simulates how the plugin would be used in a real game scenario
        
        // Step 1: Game starts with default ruleset
        var listResult = await _plugin.ListAvailableRulesets(false);
        var listJson = JsonSerializer.Deserialize<JsonElement>(listResult);
        Assert.Equal(_testGameState.ActiveRulesetId, listJson.GetProperty("activeRulesetId").GetString());

        // Step 2: Player creates a custom ruleset during game
        var customResult = await _plugin.CreateNewRuleset(
            "player-custom", "Player's Custom Rules", "1.0.0",
            "Custom rules created by the player during gameplay");
        
        var customJson = JsonSerializer.Deserialize<JsonElement>(customResult);
        Assert.True(customJson.GetProperty("success").GetBoolean());

        // Step 3: Add custom exploration mechanics
        await _plugin.AddFunctionToRuleset(
            "player-custom", "Exploration", "inspect_environment", "inspectEnvironment",
            "Allows detailed inspection of the current environment",
            @"[{""name"": ""focus"", ""type"": ""string"", ""description"": ""What to focus on""}]",
            @"[""return focus && focus.length > 2;""]",
            @"[{""action"": ""revealDetails"", ""target"": ""focus""}]");

        // Step 4: Add custom combat mechanics
        await _plugin.AddFunctionToRuleset(
            "player-custom", "Combat", "tactical_analysis", "tacticalAnalysis",
            "Provides tactical analysis of the combat situation",
            @"[{""name"": ""analysisType"", ""type"": ""string""}]",
            @"[""return character.level >= 5;"", ""return analysisType === 'basic' || analysisType === 'advanced';""]",
            @"[{""action"": ""provideTacticalInfo"", ""type"": ""analysisType""}]");

        // Step 5: Add custom Pokemon data
        await _plugin.UpdateGameData("player-custom", "customPokemon", @"{
            ""shadowPikachu"": {
                ""type"": [""electric"", ""dark""],
                ""baseStats"": {""hp"": 40, ""attack"": 60, ""defense"": 35},
                ""abilities"": [""shadowBolt"", ""staticDischarge""],
                ""rarity"": ""legendary""
            },
            ""crystalEevee"": {
                ""type"": [""normal"", ""crystal""],
                ""baseStats"": {""hp"": 55, ""attack"": 45, ""defense"": 50},
                ""abilities"": [""adapt"", ""crystalize""],
                ""rarity"": ""rare""
            }
        }");

        // Step 6: Validate before switching
        var validationResult = await _plugin.ValidateRuleset("player-custom", true);
        var validationJson = JsonSerializer.Deserialize<JsonElement>(validationResult);
        Assert.True(validationJson.GetProperty("valid").GetBoolean());

        // Step 7: Switch to custom ruleset
        var switchResult = await _plugin.SwitchActiveRuleset("player-custom", false);
        var switchJson = JsonSerializer.Deserialize<JsonElement>(switchResult);
        Assert.True(switchJson.GetProperty("success").GetBoolean());

        // Step 8: Verify game state was properly updated
        _mockGameStateRepo.Verify(x => x.SaveStateAsync(It.Is<GameStateModel>(gs => 
            gs.ActiveRulesetId == "player-custom" &&
            gs.RecentEvents.Any(e => e.EventDescription.Contains("Switched active ruleset")))), Times.Once());

        // Step 9: Get details of active ruleset for game usage
        var activeDetailsResult = await _plugin.GetRulesetDetails("player-custom", true, true);
        var activeDetailsJson = JsonSerializer.Deserialize<JsonElement>(activeDetailsResult);
        
        Assert.True(activeDetailsJson.GetProperty("isActive").GetBoolean());
        Assert.Equal(2, activeDetailsJson.GetProperty("totalFunctions").GetInt32());
        
        var gameData = activeDetailsJson.GetProperty("gameData");
        Assert.True(gameData.TryGetProperty("customPokemon", out var pokemonData));
        Assert.True(pokemonData.TryGetProperty("shadowPikachu", out _));
        Assert.True(pokemonData.TryGetProperty("crystalEevee", out _));

        // Step 10: Verify all manager integrations worked correctly
        _mockRulesetManager.Verify(x => x.SetActiveRulesetAsync("player-custom"), Times.Once());
        _mockJsEngine.Verify(x => x.IsSafeScriptAsync(It.IsAny<string>()), Times.AtLeast(4)); // 2 functions * 2 rules each
    }

    #endregion

    #region Helper Methods

    private async Task CreateCompleteRuleset(string rulesetId)
    {
        await _plugin.CreateNewRuleset(rulesetId, $"Complete Test Ruleset {rulesetId}", "1.0.0", "Complete test ruleset");
        
        await _plugin.AddFunctionToRuleset(rulesetId, "Exploration", "test-explore", "testExplore", "Test exploration",
            "[]", @"[""return true;""]", "[]");
        
        await _plugin.AddFunctionToRuleset(rulesetId, "Combat", "test-combat", "testCombat", "Test combat",
            "[]", @"[""return true;""]", "[]");
        
        await _plugin.UpdateGameData(rulesetId, "testData", @"{""test"": true}");
    }

    private string GeneratePokemonData()
    {
        return @"{
            ""bulbasaur"": {""type"": [""grass"", ""poison""], ""baseStats"": {""hp"": 45, ""attack"": 49}},
            ""charmander"": {""type"": [""fire""], ""baseStats"": {""hp"": 39, ""attack"": 52}},
            ""squirtle"": {""type"": [""water""], ""baseStats"": {""hp"": 44, ""attack"": 48}},
            ""pikachu"": {""type"": [""electric""], ""baseStats"": {""hp"": 35, ""attack"": 55}}
        }";
    }

    private string GenerateMovesData()
    {
        return @"[
            {""name"": ""Tackle"", ""power"": 40, ""type"": ""normal""},
            {""name"": ""Thunderbolt"", ""power"": 90, ""type"": ""electric""},
            {""name"": ""Flamethrower"", ""power"": 90, ""type"": ""fire""},
            {""name"": ""Water Gun"", ""power"": 40, ""type"": ""water""}
        ]";
    }

    private string GenerateItemsData()
    {
        return @"[
            {""name"": ""Potion"", ""type"": ""healing"", ""effect"": ""restores 20 HP""},
            {""name"": ""Pokeball"", ""type"": ""capture"", ""effect"": ""captures Pokemon""},
            {""name"": ""Rare Candy"", ""type"": ""enhancement"", ""effect"": ""increases level by 1""}
        ]";
    }

    private string GenerateLocationsData()
    {
        return @"[
            {""name"": ""Pallet Town"", ""type"": ""town"", ""connections"": [""Route 1""]},
            {""name"": ""Route 1"", ""type"": ""route"", ""connections"": [""Pallet Town"", ""Viridian City""]},
            {""name"": ""Viridian City"", ""type"": ""city"", ""connections"": [""Route 1""]}
        ]";
    }

    #endregion
}