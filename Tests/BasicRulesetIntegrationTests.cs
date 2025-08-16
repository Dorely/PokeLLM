using Xunit;
using Moq;
using PokeLLM.GameState.Models;
using PokeLLM.GameState;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using System.Text.Json;

namespace PokeLLM.Tests;

/// <summary>
/// Basic integration tests to verify the framework can work with different rulesets
/// </summary>
public class BasicRulesetIntegrationTests
{

    [Fact]
    public async Task GameStateModel_CanStoreGenericRulesetData()
    {
        // Arrange
        var gameState = new GameStateModel();
        gameState.ActiveRulesetId = "pokemon-adventure";
        
        // Act - Add some Pokemon-specific data through dynamic storage
        var pokemonTeam = new[] { new { species = "pikachu", level = 5 } };
        gameState.RulesetGameData["playerTeam"] = JsonSerializer.SerializeToElement(pokemonTeam);
        
        var inventory = new { pokeball = 5, potion = 3 };
        gameState.RulesetGameData["inventory"] = JsonSerializer.SerializeToElement(inventory);
        
        // Assert
        Assert.Equal("pokemon-adventure", gameState.ActiveRulesetId);
        Assert.Equal(2, gameState.RulesetGameData.Count);
        Assert.True(gameState.RulesetGameData.ContainsKey("playerTeam"));
        Assert.True(gameState.RulesetGameData.ContainsKey("inventory"));
    }

    [Fact]
    public async Task GameStateModel_CanSwitchBetweenRulesets()
    {
        // Arrange
        var gameState = new GameStateModel();
        
        // Act - Set up as Pokemon game
        gameState.ActiveRulesetId = "pokemon-adventure";
        var pokemonTeam = new[] { new { species = "pikachu" } };
        gameState.RulesetGameData["playerTeam"] = JsonSerializer.SerializeToElement(pokemonTeam);
        
        // Switch to D&D
        gameState.ActiveRulesetId = "dnd5e";
        gameState.RulesetGameData.Clear(); // Clear old data
        var dndCharacter = new { race = "elf", characterClass = "wizard" };
        gameState.RulesetGameData["character"] = JsonSerializer.SerializeToElement(dndCharacter);
        
        // Assert
        Assert.Equal("dnd5e", gameState.ActiveRulesetId);
        Assert.Single(gameState.RulesetGameData);
        Assert.True(gameState.RulesetGameData.ContainsKey("character"));
        Assert.False(gameState.RulesetGameData.ContainsKey("playerTeam"));
    }

    [Fact]
    public async Task BasicPlayerState_CanBeSerializedAndDeserialized()
    {
        // Arrange
        var player = new BasicPlayerState
        {
            Name = "Test Player",
            Description = "A brave adventurer",
            Level = 5,
            Experience = 2500
        };
        player.Conditions.Add("well_rested");
        player.Relationships["npc_merchant"] = 25;

        // Act
        var json = JsonSerializer.Serialize(player);
        var deserializedPlayer = JsonSerializer.Deserialize<BasicPlayerState>(json);

        // Assert
        Assert.NotNull(deserializedPlayer);
        Assert.Equal("Test Player", deserializedPlayer.Name);
        Assert.Equal("A brave adventurer", deserializedPlayer.Description);
        Assert.Equal(5, deserializedPlayer.Level);
        Assert.Equal(2500, deserializedPlayer.Experience);
        Assert.Contains("well_rested", deserializedPlayer.Conditions);
        Assert.Equal(25, deserializedPlayer.Relationships["npc_merchant"]);
    }

    [Fact]
    public async Task RulesetManager_CanBeCreatedWithoutPokemonDependencies()
    {
        // Arrange
        var mockRulesetService = new Mock<IRulesetService>();
        var mockDynamicFunctionFactory = new Mock<IDynamicFunctionFactory>();
        
        // Act & Assert - Should not throw
        var rulesetManager = new RulesetManager(mockRulesetService.Object, mockDynamicFunctionFactory.Object);
        Assert.NotNull(rulesetManager);
    }

    [Fact]
    public void ItemInstance_WorksAsGenericInventoryItem()
    {
        // Arrange & Act
        var potion = new ItemInstance
        {
            ItemId = "item_health_potion",
            Quantity = 5
        };

        var pokeball = new ItemInstance
        {
            ItemId = "item_pokeball",
            Quantity = 10
        };

        // Assert
        Assert.Equal("item_health_potion", potion.ItemId);
        Assert.Equal(5, potion.Quantity);
        Assert.Equal("item_pokeball", pokeball.ItemId);
        Assert.Equal(10, pokeball.Quantity);
    }
}