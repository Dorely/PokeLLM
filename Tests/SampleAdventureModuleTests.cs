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

            Assert.NotNull(classData.StartingPassiveAbilities);
            Assert.True(classData.StartingPassiveAbilities.Count > 0, $"Class {classId} is missing starting passive abilities.");

            AssertNoInvalidLevels(classId, classData.LevelUpChart);

            foreach (var level in Enumerable.Range(1, 20))
            {
                Assert.True(
                    HasEntriesAtLevel(classData.LevelUpChart, level),
                    $"Class {classId} must define ability or passive ability rewards at level {level}.");
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

    private static bool HasEntriesAtLevel(Dictionary<int, AdventureModuleClassLevelProgression>? chart, int level)
    {
        if (chart is null)
        {
            return false;
        }

        return chart.TryGetValue(level, out var progression) && HasRewards(progression);
    }

    private static void AssertNoInvalidLevels(string classId, Dictionary<int, AdventureModuleClassLevelProgression>? chart)
    {
        Assert.NotNull(chart);

        foreach (var (level, progression) in chart!)
        {
            Assert.InRange(level, 1, 20);
            Assert.True(HasRewards(progression), $"Class {classId} has an empty reward list at level {level}.");
        }
    }

    private static bool HasRewards(AdventureModuleClassLevelProgression? progression)
    {
        if (progression is null)
        {
            return false;
        }

        var hasAbilities = progression.Abilities is { Count: > 0 } && progression.Abilities.Any(e => !string.IsNullOrWhiteSpace(e));
        var hasPassiveAbilities = progression.PassiveAbilities is { Count: > 0 } && progression.PassiveAbilities.Any(e => !string.IsNullOrWhiteSpace(e));

        return hasAbilities || hasPassiveAbilities;
    }

}
