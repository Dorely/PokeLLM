using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Moq;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.VectorStore;
using PokeLLM.Game.VectorStore.Models;
using Qdrant.Client;
using System.Diagnostics;

namespace Tests;

/// <summary>
/// Integration tests for QdrantVectorStoreService that require a locally running Qdrant instance.
/// These tests will connect to Qdrant running on localhost:6334 (default Qdrant port).
/// 
/// To run these tests, start Qdrant locally using Docker:
/// docker run -p 6334:6334 -p 6333:6333 qdrant/qdrant
/// 
/// Or install Qdrant locally and run it on the default port.
/// </summary>
public class QdrantVectorStoreServiceTests : IAsyncLifetime
{
    private QdrantVectorStoreService? _service;
    private Mock<IEmbeddingGenerator<string, Embedding<float>>>? _mockEmbeddingGenerator;
    private IOptions<QdrantConfig>? _qdrantOptions;
    private readonly string _testSessionId = Guid.NewGuid().ToString();

    public async Task InitializeAsync()
    {
        // Setup mock embedding generator
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        
        // Create test embeddings (1536 dimensions for OpenAI compatibility)
        var testEmbedding = new Embedding<float>(Enumerable.Range(0, 1536).Select(i => (float)Random.Shared.NextDouble()).ToArray());
        
        // Mock the GenerateAsync method that returns multiple embeddings
        _mockEmbeddingGenerator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<GeneratedEmbeddings<Embedding<float>>>(new GeneratedEmbeddings<Embedding<float>>(new[] { testEmbedding })));

        // Configure Qdrant to connect to local instance
        _qdrantOptions = Options.Create(new QdrantConfig
        {
            Host = "localhost",
            Port = 6334,
            UseHttps = false
        });

        // Create the service
        _service = new QdrantVectorStoreService(_mockEmbeddingGenerator.Object, _qdrantOptions);

        // Wait a moment for Qdrant to be ready
        //await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        // Clean up test data by trying to delete test collections
        try
        {
            var client = new QdrantClient(_qdrantOptions!.Value.Host, _qdrantOptions.Value.Port);
            
            // Try to delete test collections (ignore errors if they don't exist)
            try { await client.DeleteCollectionAsync("entities"); } catch { }
            try { await client.DeleteCollectionAsync("locations"); } catch { }
            try { await client.DeleteCollectionAsync("lore"); } catch { }
            try { await client.DeleteCollectionAsync("rules"); } catch { }
            try { await client.DeleteCollectionAsync("narrative_log"); } catch { }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cleanup warning: {ex.Message}");
        }
    }

    #region Entity Tests

    [Fact]
    public async Task AddOrUpdateEntityAsync_WithNewEntity_ShouldReturnValidGuid()
    {
        // Arrange
        var entity = new EntityVectorRecord
        {
            EntityId = "test_entity_" + Guid.NewGuid(),
            EntityType = "Character",
            Name = "Test Character",
            Description = "A test character for unit testing",
            PropertiesJson = "{\"level\": 5}"
        };

        // Act
        var result = await _service!.AddOrUpdateEntityAsync(entity);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public async Task AddOrUpdateEntityAsync_WithExistingId_ShouldKeepSameId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var entity = new EntityVectorRecord
        {
            Id = existingId,
            EntityId = "test_entity_existing_" + Guid.NewGuid(),
            EntityType = "Character",
            Name = "Existing Character",
            Description = "An existing character for testing",
            PropertiesJson = "{\"level\": 10}"
        };

        // Act
        var result = await _service!.AddOrUpdateEntityAsync(entity);

        // Assert
        Assert.Equal(existingId, result);
        Assert.Equal(existingId, entity.Id);
    }

    [Fact]
    public async Task GetEntityByIdAsync_WithExistingEntity_ShouldReturnEntity()
    {
        // Arrange
        var entityId = "test_get_entity_" + Guid.NewGuid();
        var entity = new EntityVectorRecord
        {
            EntityId = entityId,
            EntityType = "PokemonSpecies",
            Name = "Test Pokemon",
            Description = "A test pokemon species",
            PropertiesJson = "{\"type1\": \"Fire\"}"
        };

        await _service!.AddOrUpdateEntityAsync(entity);
        //await Task.Delay(500); // Allow time for indexing

        // Act
        var result = await _service.GetEntityByIdAsync(entityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entityId, result.EntityId);
        Assert.Equal("PokemonSpecies", result.EntityType);
        Assert.Equal("Test Pokemon", result.Name);
    }

    [Fact]
    public async Task GetEntityByIdAsync_WithNonExistentEntity_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = "non_existent_" + Guid.NewGuid();

        // Act
        var result = await _service!.GetEntityByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GameRule Tests

    [Fact]
    public async Task AddOrUpdateGameRuleAsync_WithNewRule_ShouldReturnValidGuid()
    {
        // Arrange
        var rule = new GameRuleVectorRecord
        {
            EntryId = "test_rule_" + Guid.NewGuid(),
            EntryType = "Rule",
            Title = "Test Combat Rule",
            Content = "This is a test rule for combat mechanics in the game.",
            Tags = new[] { "combat", "mechanics", "test" }
        };

        // Act
        var result = await _service!.AddOrUpdateGameRuleAsync(rule);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotEqual(Guid.Empty, rule.Id);
    }

    [Fact]
    public async Task GetGameRuleByIdAsync_WithExistingRule_ShouldReturnRule()
    {
        // Arrange
        var entryId = "test_get_rule_" + Guid.NewGuid();
        var rule = new GameRuleVectorRecord
        {
            EntryId = entryId,
            EntryType = "Class",
            Title = "Test Trainer Class",
            Content = "A test trainer class with special abilities.",
            Tags = new[] { "class", "trainer", "test" }
        };

        await _service!.AddOrUpdateGameRuleAsync(rule);
        //await Task.Delay(500); // Allow time for indexing

        // Act
        var result = await _service.GetGameRuleByIdAsync(entryId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entryId, result.EntryId);
        Assert.Equal("Class", result.EntryType);
        Assert.Equal("Test Trainer Class", result.Title);
    }

    [Fact]
    public async Task SearchGameRulesAsync_WithRelevantQuery_ShouldReturnResults()
    {
        // Arrange
        var rule1 = new GameRuleVectorRecord
        {
            EntryId = "search_rule_1_" + Guid.NewGuid(),
            EntryType = "Rule",
            Title = "Fire Type Effectiveness",
            Content = "Fire type moves are super effective against Grass and Ice types.",
            Tags = new[] { "fire", "type", "effectiveness" }
        };

        var rule2 = new GameRuleVectorRecord
        {
            EntryId = "search_rule_2_" + Guid.NewGuid(),
            EntryType = "Rule", 
            Title = "Water Type Effectiveness",
            Content = "Water type moves are super effective against Fire and Rock types.",
            Tags = new[] { "water", "type", "effectiveness" }
        };

        await _service!.AddOrUpdateGameRuleAsync(rule1);
        await _service.AddOrUpdateGameRuleAsync(rule2);
        //await Task.Delay(1000); // Allow time for indexing

        // Act
        var results = await _service.SearchGameRulesAsync("fire type effectiveness", minRelevanceScore: 0.1, limit: 5);

        // Assert
        Assert.NotNull(results);
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);
        
        // Should find at least the fire type rule
        var fireRule = resultList.FirstOrDefault(r => r.Record.Title.Contains("Fire"));
        Assert.NotNull(fireRule);
    }

    #endregion

    #region Location Tests

    [Fact]
    public async Task AddOrUpdateLocationAsync_WithNewLocation_ShouldReturnValidGuid()
    {
        // Arrange
        var location = new LocationVectorRecord
        {
            LocationId = "test_location_" + Guid.NewGuid(),
            Name = "Test Town",
            Description = "A peaceful test town with friendly NPCs and Pokemon.",
            Region = "Test Region",
            Tags = new[] { "town", "peaceful", "test" }
        };

        // Act
        var result = await _service!.AddOrUpdateLocationAsync(location);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotEqual(Guid.Empty, location.Id);
    }

    [Fact]
    public async Task GetLocationByIdAsync_WithExistingLocation_ShouldReturnLocation()
    {
        // Arrange
        var locationId = "test_get_location_" + Guid.NewGuid();
        var location = new LocationVectorRecord
        {
            LocationId = locationId,
            Name = "Test Cave",
            Description = "A mysterious cave filled with rare Pokemon.",
            Region = "Test Mountains",
            Tags = new[] { "cave", "mysterious", "rare" }
        };

        await _service!.AddOrUpdateLocationAsync(location);
        //await Task.Delay(500); // Allow time for indexing

        // Act
        var result = await _service.GetLocationByIdAsync(locationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(locationId, result.LocationId);
        Assert.Equal("Test Cave", result.Name);
        Assert.Equal("Test Mountains", result.Region);
    }

    #endregion

    #region Lore Tests

    [Fact]
    public async Task AddOrUpdateLoreAsync_WithNewLore_ShouldReturnValidGuid()
    {
        // Arrange
        var lore = new LoreVectorRecord
        {
            EntryId = "test_lore_" + Guid.NewGuid(),
            EntryType = "Legend",
            Title = "The Test Legend",
            Content = "Long ago, in the test realm, there lived a legendary Pokemon of great power.",
            Tags = new[] { "legend", "ancient", "test" }
        };

        // Act
        var result = await _service!.AddOrUpdateLoreAsync(lore);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotEqual(Guid.Empty, lore.Id);
    }

    [Fact]
    public async Task GetLoreByIdAsync_WithExistingLore_ShouldReturnLore()
    {
        // Arrange
        var entryId = "test_get_lore_" + Guid.NewGuid();
        var lore = new LoreVectorRecord
        {
            EntryId = entryId,
            EntryType = "History",
            Title = "Test Historical Event",
            Content = "This event marks an important moment in test history.",
            Tags = new[] { "history", "important", "test" }
        };

        await _service!.AddOrUpdateLoreAsync(lore);
        //await Task.Delay(500); // Allow time for indexing

        // Act
        var result = await _service.GetLoreByIdAsync(entryId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entryId, result.EntryId);
        Assert.Equal("History", result.EntryType);
        Assert.Equal("Test Historical Event", result.Title);
    }

    [Fact]
    public async Task SearchLoreAsync_WithRelevantQuery_ShouldReturnResults()
    {
        // Arrange
        var lore1 = new LoreVectorRecord
        {
            EntryId = "search_lore_1_" + Guid.NewGuid(),
            EntryType = "Legend",
            Title = "Legend of the Sea",
            Content = "A powerful sea Pokemon once ruled the vast oceans with wisdom and strength.",
            Tags = new[] { "sea", "ocean", "legend" }
        };

        var lore2 = new LoreVectorRecord
        {
            EntryId = "search_lore_2_" + Guid.NewGuid(),
            EntryType = "Legend",
            Title = "Legend of the Sky",
            Content = "High above the clouds, a magnificent flying Pokemon watched over all.",
            Tags = new[] { "sky", "flying", "legend" }
        };

        await _service!.AddOrUpdateLoreAsync(lore1);
        await _service.AddOrUpdateLoreAsync(lore2);
        //await Task.Delay(1000); // Allow time for indexing

        // Act
        var results = await _service.SearchLoreAsync("ocean sea powerful", minRelevanceScore: 0.1, limit: 5);

        // Assert
        Assert.NotNull(results);
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);
        
        // Should find at least the sea legend
        var seaLegend = resultList.FirstOrDefault(r => r.Record.Title.Contains("Sea"));
        Assert.NotNull(seaLegend);
    }

    #endregion

    #region Narrative Log Tests

    [Fact]
    public async Task LogNarrativeEventAsync_WithNewEvent_ShouldReturnValidGuid()
    {
        // Arrange
        var narrativeLog = new NarrativeLogVectorRecord
        {
            SessionId = _testSessionId,
            GameTurnNumber = 1,
            EventType = "Dialogue",
            EventSummary = "Player met Professor Oak and received their first Pokemon.",
            FullTranscript = "Professor Oak: 'Welcome to the world of Pokemon! Choose your starter!'",
            InvolvedEntities = new[] { "player", "prof_oak", "starter_pokemon" },
            LocationId = "pallet_town"
        };

        // Act
        var result = await _service!.LogNarrativeEventAsync(narrativeLog);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotEqual(Guid.Empty, narrativeLog.Id);
    }

    [Fact]
    public async Task GetNarrativeEventAsync_WithExistingEvent_ShouldReturnEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var turnNumber = 5;
        var narrativeLog = new NarrativeLogVectorRecord
        {
            SessionId = sessionId,
            GameTurnNumber = turnNumber,
            EventType = "CombatVictory",
            EventSummary = "Player defeated wild Rattata in Route 1.",
            FullTranscript = "Wild Rattata appeared! Player sent out Pikachu! Pikachu used Thunderbolt! It's super effective!",
            InvolvedEntities = new[] { "player", "pikachu", "wild_rattata" },
            LocationId = "route_1"
        };

        await _service!.LogNarrativeEventAsync(narrativeLog);
        //await Task.Delay(500); // Allow time for indexing

        // Act
        var result = await _service.GetNarrativeEventAsync(sessionId, turnNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(turnNumber, result.GameTurnNumber);
        Assert.Equal("CombatVictory", result.EventType);
    }

    [Fact]
    public async Task FindMemoriesAsync_WithSessionFilter_ShouldReturnSessionEvents()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        
        var event1 = new NarrativeLogVectorRecord
        {
            SessionId = sessionId,
            GameTurnNumber = 10,
            EventType = "Dialogue",
            EventSummary = "Player talked to Nurse Joy at Pokemon Center.",
            InvolvedEntities = new[] { "player", "nurse_joy" },
            LocationId = "pokemon_center"
        };

        var event2 = new NarrativeLogVectorRecord
        {
            SessionId = sessionId,
            GameTurnNumber = 11,
            EventType = "Healing",
            EventSummary = "Player's Pokemon were healed at the Pokemon Center.",
            InvolvedEntities = new[] { "player", "pikachu" },
            LocationId = "pokemon_center"
        };

        // Event from different session (should not be returned)
        var differentSessionEvent = new NarrativeLogVectorRecord
        {
            SessionId = Guid.NewGuid().ToString(),
            GameTurnNumber = 10,
            EventType = "Dialogue",
            EventSummary = "Different session event.",
            InvolvedEntities = new[] { "player", "other_npc" },
            LocationId = "other_location"
        };

        await _service!.LogNarrativeEventAsync(event1);
        await _service.LogNarrativeEventAsync(event2);
        await _service.LogNarrativeEventAsync(differentSessionEvent);
        //await Task.Delay(1000); // Allow time for indexing

        // Act
        var results = await _service.FindMemoriesAsync(sessionId, "Pokemon Center healing", new[] { "player" }, minRelevanceScore: 0.1, limit: 10);

        // Assert
        Assert.NotNull(results);
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);
        
        // All results should be from the correct session
        Assert.All(resultList, r => Assert.Equal(sessionId, r.Record.SessionId));
        
        // Should find both events from this session
        Assert.True(resultList.Count >= 2);
    }

    [Fact]
    public async Task FindMemoriesAsync_WithEntityFilter_ShouldReturnEventsWithEntity()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        
        var eventWithPikachu = new NarrativeLogVectorRecord
        {
            SessionId = sessionId,
            GameTurnNumber = 20,
            EventType = "Battle",
            EventSummary = "Pikachu battled against Team Rocket.",
            InvolvedEntities = new[] { "player", "pikachu", "team_rocket" },
            LocationId = "city_square"
        };

        var eventWithoutPikachu = new NarrativeLogVectorRecord
        {
            SessionId = sessionId,
            GameTurnNumber = 21,
            EventType = "Shopping",
            EventSummary = "Player bought items at the Pokemart.",
            InvolvedEntities = new[] { "player", "mart_clerk" },
            LocationId = "pokemart"
        };

        await _service!.LogNarrativeEventAsync(eventWithPikachu);
        await _service.LogNarrativeEventAsync(eventWithoutPikachu);
        //await Task.Delay(1000); // Allow time for indexing

        // Act
        var results = await _service.FindMemoriesAsync(sessionId, "battle", new[] { "pikachu" }, minRelevanceScore: 0.1, limit: 10);

        // Assert
        Assert.NotNull(results);
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);
        
        // All results should contain pikachu in involved entities
        Assert.All(resultList, r => Assert.Contains("pikachu", r.Record.InvolvedEntities));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AddOrUpdateEntityAsync_WithNullEntity_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service!.AddOrUpdateEntityAsync(null!));
    }

    [Fact]
    public async Task GetEntityByIdAsync_WithNullEntityId_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service!.GetEntityByIdAsync(null!));
    }

    [Fact]
    public async Task GetEntityByIdAsync_WithEmptyEntityId_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service!.GetEntityByIdAsync(string.Empty));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullWorkflow_EntityLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var entityId = "integration_test_entity_" + Guid.NewGuid();
        var entity = new EntityVectorRecord
        {
            EntityId = entityId,
            EntityType = "Character",
            Name = "Integration Test NPC",
            Description = "An NPC created for integration testing of the full workflow.",
            PropertiesJson = "{\"level\": 15, \"type\": \"npc\"}"
        };

        // Act 1: Add entity
        var addResult = await _service!.AddOrUpdateEntityAsync(entity);
        //await Task.Delay(500); // Allow time for indexing

        // Act 2: Retrieve entity
        var retrievedEntity = await _service.GetEntityByIdAsync(entityId);

        // Assert first retrieval
        Assert.NotEqual(Guid.Empty, addResult);
        Assert.NotNull(retrievedEntity);
        Assert.Equal(entityId, retrievedEntity.EntityId);
        Assert.Equal("Integration Test NPC", retrievedEntity.Name);

        // Act 3: Update entity
        retrievedEntity.Name = "Updated Integration Test NPC";
        retrievedEntity.Description = "An updated NPC for integration testing.";
        var updateResult = await _service.AddOrUpdateEntityAsync(retrievedEntity);
        //await Task.Delay(500); // Allow time for indexing

        // Act 4: Retrieve updated entity
        var updatedEntity = await _service.GetEntityByIdAsync(entityId);

        // Assert update
        Assert.Equal(addResult, updateResult); // Should be same ID
        Assert.NotNull(updatedEntity);
        Assert.Equal("Updated Integration Test NPC", updatedEntity.Name);
        Assert.Equal("An updated NPC for integration testing.", updatedEntity.Description);
    }

    [Fact]
    public async Task FullWorkflow_NarrativeMemorySearch_ShouldWorkCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var events = new[]
        {
            new NarrativeLogVectorRecord
            {
                SessionId = sessionId,
                GameTurnNumber = 30,
                EventType = "Discovery",
                EventSummary = "Player discovered a rare shiny Pokemon in the deep forest.",
                InvolvedEntities = new[] { "player", "shiny_pokemon" },
                LocationId = "deep_forest"
            },
            new NarrativeLogVectorRecord
            {
                SessionId = sessionId,
                GameTurnNumber = 31,
                EventType = "Capture",
                EventSummary = "Player successfully captured the shiny Pokemon after an intense battle.",
                InvolvedEntities = new[] { "player", "shiny_pokemon", "pokeball" },
                LocationId = "deep_forest"
            },
            new NarrativeLogVectorRecord
            {
                SessionId = sessionId,
                GameTurnNumber = 32,
                EventType = "Celebration",
                EventSummary = "Player celebrated the rare catch with their team.",
                InvolvedEntities = new[] { "player", "pikachu", "shiny_pokemon" },
                LocationId = "deep_forest"
            }
        };

        // Act 1: Log all events
        foreach (var evt in events)
        {
            await _service!.LogNarrativeEventAsync(evt);
        }
        //await Task.Delay(1500); // Allow time for indexing

        // Act 2: Search for memories about shiny Pokemon
        var shinyMemories = await _service.FindMemoriesAsync(sessionId, "shiny Pokemon discovery capture", new[] { "player" }, minRelevanceScore: 0.1, limit: 10);

        // Act 3: Search for specific event by turn
        var specificEvent = await _service.GetNarrativeEventAsync(sessionId, 31);

        // Assert
        var shinyResults = shinyMemories.ToList();
        Assert.NotEmpty(shinyResults);
        Assert.True(shinyResults.Count >= 3); // Should find all three events
        
        // Results should be ordered by relevance (descending)
        for (int i = 0; i < shinyResults.Count - 1; i++)
        {
            Assert.True(shinyResults[i].Score >= shinyResults[i + 1].Score);
        }

        Assert.NotNull(specificEvent);
        Assert.Equal(31, specificEvent.GameTurnNumber);
        Assert.Equal("Capture", specificEvent.EventType);
        Assert.Contains("shiny_pokemon", specificEvent.InvolvedEntities);
    }

    #endregion
}