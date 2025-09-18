using System;
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

    private const string StartingAbilityId = "ability_starting";
    private const string StartingPassiveAbilityId = "ability_passive_starting";
    private const string LevelAbilityId = "ability_level_reward";
    private const string LevelPassiveAbilityId = "ability_passive_level_reward";

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
        Assert.Collection(storedClass.StartingAbilities, ability => Assert.Equal(StartingAbilityId, ability));
        Assert.Collection(storedClass.StartingPassiveAbilities, ability => Assert.Equal(StartingPassiveAbilityId, ability));
        Assert.NotNull(storedClass.LevelUpChart);
        Assert.Equal(20, storedClass.LevelUpChart.Count);
        Assert.All(Enumerable.Range(1, 20), level =>
        {
            Assert.True(storedClass.LevelUpChart.TryGetValue(level, out var progression));
            Assert.NotNull(progression);
            var levelProgression = progression!;
            Assert.Contains(LevelAbilityId, levelProgression.Abilities);
            Assert.Contains(LevelPassiveAbilityId, levelProgression.PassiveAbilities);
        });
    }

    [Fact]
    public async Task UpsertModuleAbility_CreatesOrUpdatesAbility()
    {
        var (plugin, session) = await CreatePluginAsync();

        var abilityDefinition = new GameSetupPhasePlugin.AbilityDefinition
        {
            Id = "ability_bug_net",
            Name = "Bug Net Expertise",
            Description = "Improves catching Bug-type Pokémon.",
            Effects = "+2 to capture checks for Bug-type Pokémon."
        };

        var result = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertModuleAbility(abilityDefinition));
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("createdNew").GetBoolean());

        var reloadedModule = await _moduleRepository.LoadByFileNameAsync(session.Module.ModuleFileName);
        Assert.True(reloadedModule.Abilities.TryGetValue("ability_bug_net", out var storedAbility));
        Assert.Equal("Bug Net Expertise", storedAbility!.Name);

        abilityDefinition.Name = "Bug Net Mastery";
        var updateResult = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertModuleAbility(abilityDefinition));
        Assert.True(updateResult.GetProperty("success").GetBoolean());
        Assert.False(updateResult.GetProperty("createdNew").GetBoolean());

        reloadedModule = await _moduleRepository.LoadByFileNameAsync(session.Module.ModuleFileName);
        Assert.True(reloadedModule.Abilities.TryGetValue("ability_bug_net", out storedAbility));
        Assert.Equal("Bug Net Mastery", storedAbility!.Name);
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
            StartingAbilities = new List<string> { StartingAbilityId },
            LevelUpChart = new Dictionary<int, GameSetupPhasePlugin.CharacterClassLevelEntry>
            {
                [5] = new()
                {
                    Abilities = new List<string> { LevelAbilityId }
                }
            }
        };

        var result = JsonSerializer.Deserialize<JsonElement>(await plugin.UpsertCharacterClass(incompleteDefinition));

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.True(result.TryGetProperty("validationErrors", out var validationErrors));
        Assert.True(validationErrors.GetArrayLength() > 0);

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
            StartingPassiveAbilities = new List<string> { StartingPassiveAbilityId },
            LevelUpChart = new Dictionary<int, GameSetupPhasePlugin.CharacterClassLevelEntry>
            {
                [1] = new()
                {
                    Abilities = new List<string> { LevelAbilityId }
                },
                [5] = new()
                {
                    PassiveAbilities = new List<string> { LevelPassiveAbilityId }
                }
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
        SeedModuleAbilities(module);
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
            StartingAbilities = new List<string> { StartingAbilityId },
            StartingPassiveAbilities = new List<string> { StartingPassiveAbilityId },
            LevelUpChart = Enumerable.Range(1, 20)
                .ToDictionary(
                    level => level,
                    level => new GameSetupPhasePlugin.CharacterClassLevelEntry
                    {
                        Abilities = new List<string> { LevelAbilityId },
                        PassiveAbilities = new List<string> { LevelPassiveAbilityId }
                    })
        };
    }

    private static void SeedModuleAbilities(AdventureModule module)
    {
        module.Abilities ??= new Dictionary<string, AdventureModuleAbility>(StringComparer.OrdinalIgnoreCase);
        module.Abilities[StartingAbilityId] = CreateAbility("Starting Ability");
        module.Abilities[StartingPassiveAbilityId] = CreateAbility("Starting Passive Ability");
        module.Abilities[LevelAbilityId] = CreateAbility("Level Ability");
        module.Abilities[LevelPassiveAbilityId] = CreateAbility("Level Passive Ability");
    }

    private static AdventureModuleAbility CreateAbility(string name)
    {
        return new AdventureModuleAbility
        {
            Name = name,
            Description = $"{name} description.",
            Effects = $"{name} effects."
        };
    }

}

