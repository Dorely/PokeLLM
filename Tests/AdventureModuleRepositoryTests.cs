using Microsoft.Extensions.Options;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Tests;

public class AdventureModuleRepositoryTests : IDisposable
{
    private readonly string _tempRoot;

    public AdventureModuleRepositoryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PokeLLMAdventureTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    private AdventureModuleRepository CreateRepository()
    {
        var options = Options.Create(new GameStateRepositoryOptions
        {
            DataDirectory = _tempRoot
        });

        return new AdventureModuleRepository(options);
    }

    [Fact]
    public void CreateNewModule_InitializesMetadataAndCollections()
    {
        var repository = CreateRepository();

        var module = repository.CreateNewModule("Test Module", "Summary");

        Assert.NotNull(module.Metadata);
        Assert.Equal("Test Module", module.Metadata.Title);
        Assert.Equal("Summary", module.Metadata.Summary);
        Assert.NotNull(module.Bestiary);
        Assert.NotNull(module.CreatureInstances);
        Assert.NotNull(module.ScenarioScripts);
        Assert.NotNull(module.Moves);
        Assert.NotNull(module.Abilities);
    }

    [Fact]
    public void ApplyChanges_InvokesUpdateAction()
    {
        var repository = CreateRepository();
        var module = repository.CreateNewModule();

        repository.ApplyChanges(module, m =>
        {
            m.World.AdventureHooks.Add("Investigate the solar forge.");
            m.ScenarioScripts.Add(new AdventureModuleScenarioScript
            {
                ScriptId = "scenario_test",
                Title = "Test Scenario",
                Summary = "Test summary",
                Goals = new List<string> { "First goal" }
            });
        });

        Assert.Contains("Investigate the solar forge.", module.World.AdventureHooks);
        Assert.Contains(module.ScenarioScripts, s => s.ScriptId == "scenario_test");
    }

    [Fact]
    public async Task SaveAsyncAndLoadAsync_RoundTripsModule()
    {
        var repository = CreateRepository();
        var module = repository.CreateNewModule("Roundtrip", "Ensures persistence");
        module.Metadata.Tags.Add("test");
        module.Bestiary["creature_test"] = new AdventureModuleCreatureSpecies
        {
            Description = "Test creature",
            Habitats = new List<string> { "loc_test" },
            DefaultMoves = new List<string> { "move_test" },
            BaseLevel = 5,
            BaseStats = new Stats { Strength = 10 },
            LevelUpMoves = new Dictionary<int, List<string>> { { 5, new List<string> { "move_grow" } } },
            EvolutionConditions = new List<string> { "None" },
            AbilityIds = new List<string> { "ability_test" }
        };
        module.Abilities["ability_test"] = new AdventureModuleAbility
        {
            Name = "Test Ability",
            Description = "Does a thing"
        };
        module.Moves["move_test"] = new AdventureModuleMove
        {
            Name = "Test Move",
            Type = "Normal",
            Category = "Status",
            DamageDice = string.Empty,
            VigorCost = 0,
            Description = "Test move description"
        };
        module.Moves["move_grow"] = new AdventureModuleMove
        {
            Name = "Grow Move",
            Type = "Grass",
            Category = "Status",
            DamageDice = string.Empty,
            VigorCost = 0,
            Description = "Supports growth"
        };

        await repository.SaveAsync(module);
        var expectedPath = Path.Combine(_tempRoot, AdventureModuleRepository.DefaultDirectoryName, $"{module.Metadata.ModuleId}.json");
        Assert.True(File.Exists(expectedPath));

        var reloaded = await repository.LoadAsync(expectedPath);

        Assert.Equal(module.Metadata.Title, reloaded.Metadata.Title);
        Assert.Contains("creature_test", reloaded.Bestiary.Keys);
        Assert.Contains("ability_test", reloaded.Abilities.Keys);
    }

    [Fact]
    public async Task LoadAsync_SampleAdventurePopulatesAllSections()
    {
        var repository = CreateRepository();
        var samplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "AdventureModules", "SampleAuroraAdventure.json");
        Assert.True(File.Exists(samplePath));

        var module = await repository.LoadAsync(samplePath);

        Assert.NotNull(module.Metadata);
        Assert.NotEmpty(module.Locations);
        Assert.NotEmpty(module.Npcs);
        Assert.NotEmpty(module.Bestiary);
        Assert.NotEmpty(module.CreatureInstances);
        Assert.NotEmpty(module.Moves);
        Assert.NotEmpty(module.Abilities);
        Assert.NotEmpty(module.ScenarioScripts);
        Assert.NotEmpty(module.CharacterClasses);

        var magmar = module.Bestiary["creature_magmar"];
        Assert.Contains("ability_flame_body", magmar.AbilityIds);

        var solarynInstance = module.CreatureInstances["inst_solaryn_radiant_core"];
        Assert.Contains("move_radiant_pulse", solarynInstance.Moves);

        var solarScout = module.CharacterClasses["class_solar_scout"];
        Assert.Contains("ability_signal_beacon", solarScout.StartingAbilities);
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
            // ignore cleanup failures
        }
    }
}
