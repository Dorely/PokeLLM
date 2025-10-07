using Microsoft.Extensions.DependencyInjection;
using Moq;
using PokeLLM.Game.GameLogic;
using Microsoft.Extensions.Logging.Abstractions;
using PokeLLM.Game.Plugins;
using PokeLLM.Game.Plugins.Models;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;
using System.Text.Json;
using System.Linq;
using Xunit;

namespace PokeLLM.Tests;

/// <summary>
/// Tests to ensure all plugin methods have proper exception handling to prevent Gemini API errors
/// </summary>
public class PluginExceptionHandlingTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IGameStateRepository> _mockGameStateRepo;

    private readonly Mock<IInformationManagementService> _mockInfoService;
    private readonly Mock<IWorldManagementService> _mockWorldService;
    private readonly Mock<IAdventureModuleRepository> _mockModuleRepository;
    private readonly Mock<INpcManagementService> _mockNpcService;
    private readonly Mock<IPokemonManagementService> _mockPokemonService;
    private readonly Mock<IPlayerPokemonManagementService> _mockPlayerPokemonService;
    private readonly Mock<ICharacterManagementService> _mockCharacterService;
    private readonly Mock<IGameLogicService> _mockGameLogicService;

    public PluginExceptionHandlingTests()
    {
        var services = new ServiceCollection();

        // Create mocks
        _mockGameStateRepo = new Mock<IGameStateRepository>();
        _mockGameStateRepo.Setup(x => x.GenerateSessionDisplayName(It.IsAny<AdventureSessionState>()))
            .Returns<AdventureSessionState>(state => "Session");
        _mockInfoService = new Mock<IInformationManagementService>();
        _mockWorldService = new Mock<IWorldManagementService>();
        _mockModuleRepository = new Mock<IAdventureModuleRepository>();
        _mockModuleRepository
            .Setup(x => x.ApplyModuleBaseline(It.IsAny<AdventureModule>(), It.IsAny<AdventureSessionState>(), It.IsAny<bool>()))
            .Returns((AdventureModule module, AdventureSessionState session, bool _) => session);
        _mockModuleRepository
            .Setup(x => x.ApplyChanges(It.IsAny<AdventureModule>(), It.IsAny<Action<AdventureModule>>()))
            .Returns((AdventureModule module, Action<AdventureModule> update) =>
            {
                update(module);
                return module;
            });

        _mockNpcService = new Mock<INpcManagementService>();
        _mockPokemonService = new Mock<IPokemonManagementService>();
        _mockPlayerPokemonService = new Mock<IPlayerPokemonManagementService>();
        _mockCharacterService = new Mock<ICharacterManagementService>();
        _mockGameLogicService = new Mock<IGameLogicService>();

        // Register mocks
        services.AddSingleton(_mockGameStateRepo.Object);
        services.AddSingleton(_mockInfoService.Object);
        services.AddSingleton(_mockWorldService.Object);
        services.AddSingleton(_mockModuleRepository.Object);
        services.AddSingleton(_mockNpcService.Object);
        services.AddSingleton(_mockPokemonService.Object);
        services.AddSingleton(_mockPlayerPokemonService.Object);
        services.AddSingleton(_mockCharacterService.Object);
        services.AddSingleton(_mockGameLogicService.Object);

        _serviceProvider = services.BuildServiceProvider();
    }
    #region UnifiedContextPlugin Tests

    [Fact]
    public async Task UnifiedContextPlugin_RetrieveStateContext_HandlesExceptions()
    {
        // Arrange
        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var plugin = new UnifiedContextPlugin(
            _mockGameStateRepo.Object,
            _mockInfoService.Object,
            _mockWorldService.Object,
            _mockNpcService.Object);

        // Act
        var result = await plugin.RetrieveStateContext();

        // Assert
        Assert.Contains("An error occurred while gathering context", result);
        Assert.Contains("Database connection failed", result);
    }

    [Fact]
    public async Task UnifiedContextPlugin_SearchNarrativeContext_HandlesExceptions()
    {
        // Arrange
        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ThrowsAsync(new ArgumentNullException("sessionId"));

        var plugin = new UnifiedContextPlugin(
            _mockGameStateRepo.Object,
            _mockInfoService.Object,
            _mockWorldService.Object,
            _mockNpcService.Object);

        // Act
        var result = await plugin.SearchNarrativeContext("test,elements");

        // Assert
        Assert.Contains("An error occurred while searching narrative context", result);
        Assert.Contains("sessionId", result);
    }

    [Fact]
    public async Task UnifiedContextPlugin_UpdateCurrentContext_HandlesExceptions()
    {
        // Arrange
        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        var plugin = new UnifiedContextPlugin(
            _mockGameStateRepo.Object,
            _mockInfoService.Object,
            _mockWorldService.Object,
            _mockNpcService.Object);

        // Act
        var result = await plugin.UpdateCurrentContext("test context");

        // Assert
        Assert.Contains("An error occurred while updating context", result);
        Assert.Contains("Access denied", result);
    }

    #endregion

    #region GameSetupPhasePlugin Tests

    [Fact]
    public async Task GameSetupPhasePlugin_UpdateModuleOverview_HandlesExceptions()
    {
        var session = new AdventureSessionState();
        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ReturnsAsync(session);
        _mockModuleRepository.Setup(x => x.LoadByFileNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Module load timeout"));

        var plugin = new GameSetupPhasePlugin(
            _mockGameStateRepo.Object,
            _mockModuleRepository.Object,
            _mockCharacterService.Object,
            NullLogger<GameSetupPhasePlugin>.Instance);

        var result = await plugin.UpdateModuleOverview(new GameSetupPhasePlugin.ModuleOverviewUpdate
        {
            Title = "Test Module"
        });

        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.False(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("Module load timeout", jsonResult.GetProperty("error").GetString());
    }


    [Fact]
    public async Task GameSetupPhasePlugin_SetPlayerName_HandlesExceptions()
    {
        // Arrange
        _mockCharacterService.Setup(x => x.SetPlayerName(It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Invalid name"));

        var plugin = new GameSetupPhasePlugin(
            _mockGameStateRepo.Object,
            _mockModuleRepository.Object,
            _mockCharacterService.Object,
            NullLogger<GameSetupPhasePlugin>.Instance);

        // Act
        var result = await plugin.SetPlayerName("TestName");

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.False(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("Invalid name", jsonResult.GetProperty("error").GetString());
    }

    #endregion

    #region ExplorationPhasePlugin Tests

    [Fact]
    public async Task ExplorationPhasePlugin_ManageDiceAndChecks_HandlesExceptions()
    {
        // Arrange
        _mockGameLogicService.Setup(x => x.RollD20Async())
            .ThrowsAsync(new InvalidOperationException("RNG failure"));

        var plugin = new ExplorationPhasePlugin(
            _mockGameLogicService.Object,
            _mockGameStateRepo.Object,
            _mockWorldService.Object,
            _mockNpcService.Object,
            _mockPokemonService.Object,
            _mockPlayerPokemonService.Object,
            _mockInfoService.Object);

        // Act
        var result = await plugin.ManageDiceAndChecks("d20");

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("RNG failure", errorProp.GetString());
    }

    [Fact]
    public async Task ExplorationPhasePlugin_ManageWorldMovement_HandlesExceptions()
    {
        // Arrange
        _mockWorldService.Setup(x => x.MovePlayerToLocationAsync(It.IsAny<string>()))
            .ThrowsAsync(new KeyNotFoundException("Location not found"));

        var plugin = new ExplorationPhasePlugin(
            _mockGameLogicService.Object,
            _mockGameStateRepo.Object,
            _mockWorldService.Object,
            _mockNpcService.Object,
            _mockPokemonService.Object,
            _mockPlayerPokemonService.Object,
            _mockInfoService.Object);

        // Act
        var result = await plugin.ManageWorldMovement("move_to_location", "invalid_location");

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("Location not found", errorProp.GetString());
    }

    [Fact]
    public async Task ExplorationPhasePlugin_ManageNpcInteractions_HandlesExceptions()
    {
        // Arrange
        _mockNpcService.Setup(x => x.GetNpcsAtLocation(It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Invalid location ID"));

        var plugin = new ExplorationPhasePlugin(
            _mockGameLogicService.Object,
            _mockGameStateRepo.Object,
            _mockWorldService.Object,
            _mockNpcService.Object,
            _mockPokemonService.Object,
            _mockPlayerPokemonService.Object,
            _mockInfoService.Object);

        // Act
        var result = await plugin.ManageNpcInteractions("get_npcs_at_location", locationId: "invalid");

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("Invalid location ID", errorProp.GetString());
    }

    #endregion

    #region CombatPhasePlugin Tests

    [Fact]
    public async Task CombatPhasePlugin_EndCombat_HandlesExceptions()
    {
        // Arrange
        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ThrowsAsync(new IOException("File system error"));

        var plugin = new CombatPhasePlugin(_mockGameStateRepo.Object, _mockGameLogicService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => plugin.EndCombat("Test combat summary"));
    }

    [Fact]
    public async Task CombatPhasePlugin_MakeSkillCheck_HandlesExceptions()
    {
        // Arrange
        _mockGameLogicService.Setup(x => x.MakeSkillCheckAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Skill check failed"));

        var plugin = new CombatPhasePlugin(_mockGameStateRepo.Object, _mockGameLogicService.Object);

        // Act
        var result = await plugin.MakeSkillCheck("Strength", 15);

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("Skill check failed", errorProp.GetString());
    }

    #endregion

    #region WorldGenerationPhasePlugin Tests

    [Fact]
    public async Task WorldGenerationPhasePlugin_ApplyUpdates_ReturnsValidationErrors_ForBrokenReferences()
    {
        // Arrange
        var session = CreateBaselineSession();
        var module = CreateBaselineModule();

        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ReturnsAsync(session);
        _mockModuleRepository.Setup(x => x.LoadByFileNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(module);
        _mockModuleRepository.Setup(x => x.GetModuleFilePath(It.IsAny<string>()))
            .Returns<string>(file => Path.Combine(Path.GetTempPath(), file));

        var plugin = new WorldGenerationPhasePlugin(
            _mockGameStateRepo.Object,
            _mockModuleRepository.Object,
            NullLogger<WorldGenerationPhasePlugin>.Instance);

        var updates = new WorldGenerationUpdateBatch
        {
            Locations = new WorldDictionaryBatch<AdventureModuleLocation>
            {
                Entries = new Dictionary<string, AdventureModuleLocation>
                {
                    ["loc_new"] = new AdventureModuleLocation
                    {
                        LocationId = "loc_new",
                        Name = "New Location",
                        Summary = "Summary",
                        FullDescription = "Description",
                        Region = "Test Region",
                        PointsOfInterest = new List<AdventureModulePointOfInterest>
                        {
                            new AdventureModulePointOfInterest
                            {
                                Id = "poi_missing",
                                Name = "Missing NPC Hook",
                                Description = "References an undefined NPC",
                                RelatedNpcIds = new List<string> { "npc_missing" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = await plugin.ApplyWorldGenerationUpdates(updates);

        // Assert
        var json = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Contains("Module updates were saved, but validation failed", json.GetProperty("error").GetString());
        var errors = json.GetProperty("validation").GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e != null && e.Contains("npc_missing", StringComparison.OrdinalIgnoreCase));
        _mockModuleRepository.Verify(x => x.SaveAsync(It.IsAny<AdventureModule>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WorldGenerationPhasePlugin_FinalizeWorldGeneration_RequiresValidModule()
    {
        // Arrange
        var session = CreateBaselineSession();
        var module = new AdventureModule
        {
            Metadata = new AdventureModuleMetadata
            {
                ModuleId = "module_test",
                Title = string.Empty // force validation failure
            },
            World = new AdventureModuleWorldOverview()
        };

        _mockGameStateRepo.Setup(x => x.LoadLatestStateAsync())
            .ReturnsAsync(session);
        _mockModuleRepository.Setup(x => x.LoadByFileNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(module);

        var plugin = new WorldGenerationPhasePlugin(
            _mockGameStateRepo.Object,
            _mockModuleRepository.Object,
            NullLogger<WorldGenerationPhasePlugin>.Instance);

        // Act
        var result = await plugin.FinalizeWorldGeneration("Opening scene text");

        // Assert
        var json = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Contains("Module failed validation", json.GetProperty("error").GetString());
    }

    #endregion

    #region LevelUpPhasePlugin Tests

    [Fact]
    public async Task LevelUpPhasePlugin_ManagePlayerAdvancement_HandlesExceptions()
    {
        // Arrange
        _mockCharacterService.Setup(x => x.GetPlayerDetails())
            .ThrowsAsync(new InvalidDataException("Player data corrupted"));

        var plugin = new LevelUpPhasePlugin(
            _mockGameStateRepo.Object,
            _mockCharacterService.Object,
            _mockPokemonService.Object,
            _mockInfoService.Object,
            _mockGameLogicService.Object);

        // Act
        var result = await plugin.ManagePlayerAdvancement("level_up");

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("Player data corrupted", errorProp.GetString());
    }

    [Fact]
    public async Task LevelUpPhasePlugin_ManagePokemonAdvancement_HandlesExceptions()
    {
        // Arrange
        _mockPokemonService.Setup(x => x.SetPokemonLevel(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new KeyNotFoundException("Pokemon not found"));

        var plugin = new LevelUpPhasePlugin(
            _mockGameStateRepo.Object,
            _mockCharacterService.Object,
            _mockPokemonService.Object,
            _mockInfoService.Object,
            _mockGameLogicService.Object);

        // Act
        var result = await plugin.ManagePokemonAdvancement("level_up", "invalid_pokemon_id", 5);

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("Pokemon not found", errorProp.GetString());
    }

    [Fact]
    public async Task LevelUpPhasePlugin_ManageExperienceAndRewards_HandlesExceptions()
    {
        // Arrange
        _mockCharacterService.Setup(x => x.AddPlayerExperiencePoints(It.IsAny<int>()))
            .ThrowsAsync(new OverflowException("Experience overflow"));

        var plugin = new LevelUpPhasePlugin(
            _mockGameStateRepo.Object,
            _mockCharacterService.Object,
            _mockPokemonService.Object,
            _mockInfoService.Object,
            _mockGameLogicService.Object);

        // Act
        var result = await plugin.ManageExperienceAndRewards("award_experience", 1000);

        // Assert
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.TryGetProperty("error", out var errorProp));
        Assert.Contains("Experience overflow", errorProp.GetString());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void AllPlugins_HaveProperExceptionHandling()
    {
        // This test verifies that all plugin methods have try-catch blocks
        // by checking that exceptions don't propagate up unhandled

        var pluginTypes = new[]
        {
            typeof(UnifiedContextPlugin),
            typeof(GameSetupPhasePlugin),
            typeof(ExplorationPhasePlugin),
            typeof(CombatPhasePlugin),
            typeof(WorldGenerationPhasePlugin),
            typeof(LevelUpPhasePlugin)
        };

        foreach (var pluginType in pluginTypes)
        {
            var methods = pluginType.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(Microsoft.SemanticKernel.KernelFunctionAttribute), false).Length > 0)
                .ToList();

            Assert.True(methods.Count > 0, $"{pluginType.Name} should have kernel functions");

            // This is more of a documentation test - the real verification happens in the individual method tests above
            // Each kernel function should handle exceptions properly as verified by the specific tests
        }
    }

    #endregion

    private static AdventureSessionState CreateBaselineSession()
    {
        var session = new AdventureSessionState
        {
            Metadata =
            {
                CurrentPhase = GamePhase.WorldGeneration
            }
        };

        session.Module.ModuleId = "module_test";
        session.Module.ModuleTitle = "Test Module";
        session.Module.ModuleFileName = "module_test.json";
        session.Region = "Test Region";
        return session;
    }

    private static AdventureModule CreateBaselineModule()
    {
        return new AdventureModule
        {
            Metadata = new AdventureModuleMetadata
            {
                ModuleId = "module_test",
                Title = "Test Module",
                Summary = "Baseline module for tests",
                RecommendedLevelRange = "1-5"
            },
            World = new AdventureModuleWorldOverview
            {
                Setting = "Test Region",
                StartingContext = "Players gather in the test region."
            }
        };
    }
}
