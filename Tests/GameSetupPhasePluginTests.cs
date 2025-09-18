using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;
using Xunit;

namespace PokeLLM.Tests;

public class GameSetupPhasePluginTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IAdventureModuleRepository _moduleRepository;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly CharacterManagementService _characterService;

    public GameSetupPhasePluginTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PokeLLMGameSetupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var options = Options.Create(new AdventureSessionRepositoryOptions
        {
            DataDirectory = _tempRoot
        });

        _moduleRepository = new AdventureModuleRepository(options);
        _gameStateRepository = new GameStateRepository(options);
        _characterService = new CharacterManagementService(_gameStateRepository);
    }

    [Fact]
    public async Task UpsertCharacterClass_WithCompleteData_Succeeds()
    {
        var (plugin, session) = await CreatePluginAsync();

        var definition = BuildCompleteClassDefinition("class_bug_catcher", "Bug Catcher");
        var result = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertCharacterClass(definition));

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.False(result.TryGetProperty("validationErrors", out _));

        var reloadedModule = await _moduleRepository.LoadByFileNameAsync(session.Module.ModuleFileName);
        Assert.True(reloadedModule.CharacterClasses.TryGetValue("class_bug_catcher", out var storedClass));
        Assert.Equal("Bug Catcher", storedClass!.Name);
        Assert.Collection(storedClass.StartingAbilities, ability => Assert.Equal("Starting Ability", ability));
        Assert.Collection(storedClass.StartingPerks, perk => Assert.Equal("Starting Perk", perk));
        Assert.Equal(20, storedClass.LevelUpAbilities.Count);
        Assert.Equal(20, storedClass.LevelUpPerks.Count);
        Assert.All(Enumerable.Range(1, 20), level =>
        {
            Assert.True(storedClass.LevelUpAbilities.ContainsKey(level));
            Assert.True(storedClass.LevelUpPerks.ContainsKey(level));
        });
    }

    [Fact]
    public async Task UpsertCharacterClass_WithMissingRequirements_ReturnsErrorsButPersists()
    {
        var (plugin, session) = await CreatePluginAsync();

        var incompleteDefinition = new GameSetupPhasePlugin.CharacterClassDefinition
        {
            Id = "class_incomplete",
            Name = "Incomplete",
            Description = "Lacks required structure.",
            StartingAbilities = new List<string>(),
            StartingPerks = null,
            LevelUpAbilities = new Dictionary<int, List<string>>
            {
                [5] = new() { "Swarm Tactics" }
            },
            LevelUpPerks = new Dictionary<int, List<string>>
            {
                [10] = new() { "Sharpened Reflexes" }
            }
        };

        var result = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertCharacterClass(incompleteDefinition));

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.True(result.TryGetProperty("validationErrors", out var errors));
        Assert.True(errors.GetArrayLength() >= 2);

        var reloadedModule = await _moduleRepository.LoadByFileNameAsync(session.Module.ModuleFileName);
        Assert.True(reloadedModule.CharacterClasses.ContainsKey("class_incomplete"));
    }

    [Fact]
    public async Task MarkSetupComplete_FailsUntilClassesValid()
    {
        var (plugin, _) = await CreatePluginAsync();

        var invalidDefinition = new GameSetupPhasePlugin.CharacterClassDefinition
        {
            Id = "class_bard",
            Name = "Bard",
            Description = "Performer with limited prep.",
            StartingAbilities = new List<string> { "Song of Rest" },
            StartingPerks = new List<string> { "Stage Presence" },
            LevelUpAbilities = new Dictionary<int, List<string>>
            {
                [1] = new() { "Verse of Vigor" },
                [5] = new() { "Ballad of Courage" }
            },
            LevelUpPerks = new Dictionary<int, List<string>>
            {
                [10] = new() { "Encore" }
            }
        };

        var upsertInvalid = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertCharacterClass(invalidDefinition));
        Assert.False(upsertInvalid.GetProperty("success").GetBoolean());

        await plugin.SetPlayerClassChoice("class_bard");
        await plugin.SetPlayerName("Seren");
        await plugin.SetPlayerStats(new[] { 15, 14, 13, 12, 10, 8 });
        await plugin.UpdateModuleOverview(new GameSetupPhasePlugin.ModuleOverviewUpdate
        {
            Setting = "Johto"
        });

        var markResult = JsonSerializer.Deserialize<JsonElement>(await plugin.MarkSetupComplete("Initial attempt"));
        Assert.False(markResult.GetProperty("success").GetBoolean());
        var invalidClasses = markResult.GetProperty("invalidClasses");
        Assert.True(invalidClasses.TryGetProperty("class_bard", out var classErrors));
        Assert.True(classErrors.GetArrayLength() > 0);

        var validDefinition = BuildCompleteClassDefinition("class_bard", "Bard");
        var upsertValid = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertCharacterClass(validDefinition));
        Assert.True(upsertValid.GetProperty("success").GetBoolean());

        await plugin.SetPlayerClassChoice("class_bard");

        var markSucceeded = JsonSerializer.Deserialize<JsonElement>(await plugin.MarkSetupComplete("Ready to adventure"));
        Assert.True(markSucceeded.GetProperty("success").GetBoolean());
        Assert.Equal("WorldGeneration", markSucceeded.GetProperty("currentPhase").GetString());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private async Task<(GameSetupPhasePlugin plugin, AdventureSessionState session)> CreatePluginAsync()
    {
        var module = _moduleRepository.CreateNewModule();
        await _moduleRepository.SaveAsync(module);
        var modulePath = _moduleRepository.GetModuleFilePath(module.Metadata.ModuleId);

        var seedSession = new AdventureSessionState
        {
            Module =
            {
                ModuleId = module.Metadata.ModuleId,
                ModuleTitle = module.Metadata.Title,
                ModuleVersion = module.Metadata.Version,
                ModuleFileName = Path.GetFileName(modulePath)
            },
            Metadata =
            {
                CurrentPhase = GamePhase.GameSetup
            }
        };

        var session = await _gameStateRepository.CreateNewGameStateAsync(seedSession);

        var plugin = new GameSetupPhasePlugin(
            _gameStateRepository,
            _moduleRepository,
            _characterService,
            NullLogger<GameSetupPhasePlugin>.Instance);

        return (plugin, session);
    }

    private static GameSetupPhasePlugin.CharacterClassDefinition BuildCompleteClassDefinition(string id, string name)
    {
        return new GameSetupPhasePlugin.CharacterClassDefinition
        {
            Id = id,
            Name = name,
            Description = $"Complete class definition for {name}.",
            StartingAbilities = new List<string> { "Starting Ability" },
            StartingPerks = new List<string> { "Starting Perk" },
            LevelUpAbilities = Enumerable.Range(1, 20)
                .ToDictionary(level => level, level => new List<string> { $"Ability_{level:00}" }),
            LevelUpPerks = Enumerable.Range(1, 20)
                .ToDictionary(level => level, level => new List<string> { $"Perk_{level:00}" })
        };
    }
}
