using System.Text.Json;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Tests;

public class SampleAdventureModuleTests
{
    [Fact]
    public void SampleAdventureModule_ClassesMeetStructureRequirements()
    {
        var module = LoadSampleModule();
        Assert.NotNull(module);
        Assert.True(module.CharacterClasses.Count >= 5, "Sample adventure must define at least five character classes.");

        foreach (var (classId, classData) in module.CharacterClasses)
        {
            Assert.NotNull(classData.StartingAbilities);
            Assert.True(classData.StartingAbilities.Count > 0, $"Class {classId} is missing starting abilities.");

            Assert.NotNull(classData.StartingPerks);
            Assert.True(classData.StartingPerks.Count > 0, $"Class {classId} is missing starting perks.");

            AssertNoInvalidLevels(classId, classData.LevelUpAbilities);
            AssertNoInvalidLevels(classId, classData.LevelUpPerks);

            foreach (var level in Enumerable.Range(1, 20))
            {
                var hasAbility = HasEntriesAtLevel(classData.LevelUpAbilities, level);
                var hasPerk = HasEntriesAtLevel(classData.LevelUpPerks, level);

                Assert.True(
                    hasAbility || hasPerk,
                    $"Class {classId} must define either an ability or perk reward at level {level}.");
            }
        }
    }

    [Fact]
    public void SampleAdventureModule_PassesStructuralValidation()
    {
        var module = LoadSampleModule();
        var validator = new AdventureModuleValidator();

        var result = validator.Validate(module);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    private static AdventureModule LoadSampleModule()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "AdventureModules", "SampleAuroraAdventure.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AdventureModule>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize sample adventure module.");
    }

    private static bool HasEntriesAtLevel(Dictionary<int, List<string>>? table, int level)
    {
        if (table is null)
        {
            return false;
        }

        return table.TryGetValue(level, out var entries) && entries.Any(e => !string.IsNullOrWhiteSpace(e));
    }

    private static void AssertNoInvalidLevels(string classId, Dictionary<int, List<string>>? table)
    {
        if (table is null)
        {
            return;
        }

        foreach (var key in table.Keys)
        {
            Assert.InRange(key, 1, 20);
            var entries = table[key];
            Assert.True(entries is { Count: > 0 } && entries.All(e => !string.IsNullOrWhiteSpace(e)),
                $"Class {classId} has an empty reward list at level {key}.");
        }
    }
}
