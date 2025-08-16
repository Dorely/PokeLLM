using Xunit;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic.Services;
using System.Text.Json;

namespace PokeLLM.Tests;

/// <summary>
/// Tests to verify the framework works with JsonElement-based dynamic data
/// </summary>
[Trait("Category", "Integration")]
[Trait("Priority", "Critical")]
public class JsonElementFrameworkTests
{
    [Fact]
    public void GameStateModel_HandlesDynamicJsonElementData()
    {
        // Arrange
        var gameState = new GameStateModel();

        // Act - Test with Pokemon-style data using JsonElement
        gameState.ActiveRulesetId = "pokemon-adventure";
        gameState.RulesetGameData["trainerClass"] = JsonSerializer.SerializeToElement("ace_trainer");
        gameState.RulesetGameData["pokemonTeam"] = JsonSerializer.SerializeToElement(new List<string> { "pikachu", "charizard" });
        gameState.RulesetGameData["gymBadges"] = JsonSerializer.SerializeToElement(new List<string> { "boulder_badge" });
        gameState.RulesetGameData["energy"] = JsonSerializer.SerializeToElement(100);
        gameState.RulesetGameData["inventory"] = JsonSerializer.SerializeToElement(new Dictionary<string, int> { ["pokeball"] = 10, ["potion"] = 5 });

        // Assert - Pokemon data is stored and retrievable correctly
        Assert.Equal("pokemon-adventure", gameState.ActiveRulesetId);
        Assert.Equal("ace_trainer", gameState.RulesetGameData["trainerClass"].GetString());
        
        var pokemonTeam = JsonSerializer.Deserialize<List<string>>(gameState.RulesetGameData["pokemonTeam"].GetRawText());
        Assert.Equal(2, pokemonTeam.Count);
        Assert.Contains("pikachu", pokemonTeam);
        Assert.Contains("charizard", pokemonTeam);

        Assert.Equal(100, gameState.RulesetGameData["energy"].GetInt32());
        
        var inventory = JsonSerializer.Deserialize<Dictionary<string, int>>(gameState.RulesetGameData["inventory"].GetRawText());
        Assert.Equal(10, inventory["pokeball"]);
        Assert.Equal(5, inventory["potion"]);

        // Act - Switch to D&D-style data
        gameState.ActiveRulesetId = "dnd5e";
        gameState.RulesetGameData.Clear(); // Clear previous data
        gameState.RulesetGameData["race"] = JsonSerializer.SerializeToElement("human");
        gameState.RulesetGameData["characterClass"] = JsonSerializer.SerializeToElement("fighter");
        gameState.RulesetGameData["level"] = JsonSerializer.SerializeToElement(3);
        gameState.RulesetGameData["hitPoints"] = JsonSerializer.SerializeToElement(28);
        gameState.RulesetGameData["abilityScores"] = JsonSerializer.SerializeToElement(new Dictionary<string, int>
        {
            ["strength"] = 16,
            ["dexterity"] = 14,
            ["constitution"] = 15
        });

        // Assert - D&D data is stored and retrievable correctly
        Assert.Equal("dnd5e", gameState.ActiveRulesetId);
        Assert.Equal("human", gameState.RulesetGameData["race"].GetString());
        Assert.Equal("fighter", gameState.RulesetGameData["characterClass"].GetString());
        Assert.Equal(3, gameState.RulesetGameData["level"].GetInt32());
        Assert.Equal(28, gameState.RulesetGameData["hitPoints"].GetInt32());
        
        var abilityScores = JsonSerializer.Deserialize<Dictionary<string, int>>(gameState.RulesetGameData["abilityScores"].GetRawText());
        Assert.Equal(16, abilityScores["strength"]);
        Assert.Equal(14, abilityScores["dexterity"]);
        Assert.Equal(15, abilityScores["constitution"]);
    }

    [Fact]
    public void GameStateModel_HandlesComplexNestedJsonElementData()
    {
        // Arrange
        var gameState = new GameStateModel();

        // Act - Create complex nested data structure with JsonElement
        var complexData = new Dictionary<string, object>
        {
            ["player"] = new Dictionary<string, object>
            {
                ["name"] = "TestPlayer",
                ["stats"] = new Dictionary<string, int> { ["hp"] = 100, ["mp"] = 50 },
                ["equipment"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = "Sword",
                        ["damage"] = 10,
                        ["enchantments"] = new List<string> { "sharp", "glowing" }
                    }
                }
            },
            ["worldState"] = new Dictionary<string, object>
            {
                ["currentLocation"] = "town_square",
                ["weather"] = "sunny",
                ["npcs"] = new List<string> { "blacksmith", "merchant" }
            }
        };

        gameState.RulesetGameData["gameData"] = JsonSerializer.SerializeToElement(complexData);

        // Assert - Complex data is preserved and accessible
        var retrievedData = JsonSerializer.Deserialize<Dictionary<string, object>>(
            gameState.RulesetGameData["gameData"].GetRawText());
        
        Assert.NotNull(retrievedData);
        Assert.True(retrievedData.ContainsKey("player"));
        Assert.True(retrievedData.ContainsKey("worldState"));

        // Test that we can navigate the nested structure
        var playerData = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(retrievedData["player"]));
        Assert.Equal("TestPlayer", playerData["name"].ToString());
    }

    [Fact]
    public async Task EntityService_WorksWithJsonElementEntities()
    {
        // Arrange
        var entityService = new BasicEntityService();

        // Act - Create Pokemon-style entity with JsonElement representation
        var pokemonData = new Dictionary<string, object>
        {
            ["id"] = "pikachu_001",
            ["species"] = "pikachu",
            ["level"] = 25,
            ["moves"] = new List<string> { "thundershock", "tackle", "tail_whip", "growl" },
            ["stats"] = new Dictionary<string, int>
            {
                ["hp"] = 60, ["attack"] = 55, ["defense"] = 40, ["speed"] = 90
            }
        };

        await entityService.CreateEntity("pikachu_001", "pokemon", pokemonData);

        // Act - Create D&D-style entity
        var characterData = new Dictionary<string, object>
        {
            ["id"] = "gandalf_001",
            ["name"] = "Gandalf",
            ["race"] = "human",
            ["class"] = "wizard",
            ["level"] = 5,
            ["abilityScores"] = new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 13, ["constitution"] = 14,
                ["intelligence"] = 18, ["wisdom"] = 15, ["charisma"] = 16
            },
            ["spells"] = new List<string> { "fireball", "magic_missile", "shield" }
        };

        await entityService.CreateEntity("gandalf_001", "character", characterData);

        // Assert - Both entities can be retrieved and accessed
        var retrievedPokemon = await entityService.GetEntity<Dictionary<string, object>>("pikachu_001");
        var retrievedCharacter = await entityService.GetEntity<Dictionary<string, object>>("gandalf_001");

        Assert.NotNull(retrievedPokemon);
        Assert.NotNull(retrievedCharacter);
        Assert.Equal("pikachu", retrievedPokemon["species"]);
        Assert.Equal("Gandalf", retrievedCharacter["name"]);
        Assert.Equal(25, retrievedPokemon["level"]);
        Assert.Equal(5, retrievedCharacter["level"]);
    }

    [Fact]
    public void GameStateModel_JsonSerializationWorksWithJsonElement()
    {
        // Arrange
        var gameState = new GameStateModel
        {
            SessionId = "test-session",
            ActiveRulesetId = "test-ruleset",
            CurrentLocationId = "test-location"
        };

        gameState.RulesetGameData["playerLevel"] = JsonSerializer.SerializeToElement(10);
        gameState.RulesetGameData["inventory"] = JsonSerializer.SerializeToElement(new Dictionary<string, int> { ["gold"] = 100 });
        gameState.RulesetGameData["flags"] = JsonSerializer.SerializeToElement(new List<string> { "quest_completed", "boss_defeated" });

        // Act - Serialize to JSON
        var json = JsonSerializer.Serialize(gameState, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        // Deserialize back
        var deserializedState = JsonSerializer.Deserialize<GameStateModel>(json);

        // Assert - Data is preserved through serialization
        Assert.NotNull(deserializedState);
        Assert.Equal("test-session", deserializedState.SessionId);
        Assert.Equal("test-ruleset", deserializedState.ActiveRulesetId);
        Assert.Equal("test-location", deserializedState.CurrentLocationId);
        Assert.True(deserializedState.RulesetGameData.ContainsKey("playerLevel"));
        Assert.True(deserializedState.RulesetGameData.ContainsKey("inventory"));
        Assert.True(deserializedState.RulesetGameData.ContainsKey("flags"));

        // Verify data can be extracted correctly
        Assert.Equal(10, deserializedState.RulesetGameData["playerLevel"].GetInt32());
        
        var inventory = JsonSerializer.Deserialize<Dictionary<string, int>>(
            deserializedState.RulesetGameData["inventory"].GetRawText());
        Assert.Equal(100, inventory["gold"]);
        
        var flags = JsonSerializer.Deserialize<List<string>>(
            deserializedState.RulesetGameData["flags"].GetRawText());
        Assert.Contains("quest_completed", flags);
        Assert.Contains("boss_defeated", flags);
    }

    [Theory]
    [InlineData("pokemon-adventure")]
    [InlineData("dnd5e")]
    [InlineData("cyberpunk-2077")]
    [InlineData("star-wars")]
    public void GameStateModel_WorksWithAnyRulesetIdAndJsonElement(string rulesetId)
    {
        // Arrange & Act
        var gameState = new GameStateModel
        {
            ActiveRulesetId = rulesetId
        };

        gameState.RulesetGameData["testData"] = JsonSerializer.SerializeToElement($"Data for {rulesetId}");
        gameState.RulesetGameData["rulesetSpecific"] = JsonSerializer.SerializeToElement(true);
        gameState.RulesetGameData["numericValue"] = JsonSerializer.SerializeToElement(42);

        // Assert - Any ruleset ID is supported with proper JsonElement handling
        Assert.Equal(rulesetId, gameState.ActiveRulesetId);
        Assert.Equal($"Data for {rulesetId}", gameState.RulesetGameData["testData"].GetString());
        Assert.True(gameState.RulesetGameData["rulesetSpecific"].GetBoolean());
        Assert.Equal(42, gameState.RulesetGameData["numericValue"].GetInt32());
    }

    [Fact]
    public void GameStateModel_AllFieldsInitializedCorrectlyWithJsonElement()
    {
        // Arrange & Act
        var gameState = new GameStateModel();

        // Assert - All fields have sensible defaults including JsonElement dictionary
        Assert.NotEmpty(gameState.SessionId);
        Assert.Equal(0, gameState.GameTurnNumber);
        Assert.True(gameState.SessionStartTime <= DateTime.UtcNow);
        Assert.True(gameState.LastSaveTime <= DateTime.UtcNow);
        Assert.NotNull(gameState.Player);
        Assert.Equal(string.Empty, gameState.CurrentLocationId);
        Assert.NotNull(gameState.WorldLocations);
        Assert.Equal(string.Empty, gameState.Region);
        Assert.Equal(TimeOfDay.Morning, gameState.TimeOfDay);
        Assert.Equal(Weather.Clear, gameState.Weather);
        Assert.NotNull(gameState.WorldEntities);
        Assert.Equal(string.Empty, gameState.AdventureSummary);
        Assert.NotNull(gameState.RecentEvents);
        Assert.Equal(GamePhase.GameSetup, gameState.CurrentPhase);
        Assert.Equal(string.Empty, gameState.PhaseChangeSummary);
        Assert.Equal(string.Empty, gameState.CurrentContext);
        Assert.Equal(string.Empty, gameState.ActiveRulesetId);
        Assert.NotNull(gameState.RulesetGameData);
        Assert.Empty(gameState.RulesetGameData);
    }
}