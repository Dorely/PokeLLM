using System.Text.Json.Serialization;

namespace PokeLLM.State;

public record AdventureModule(
    string Id,
    string Title,
    string Setting,
    string Theme,
    PlayerCharacter PlayerCharacter,
    IReadOnlyList<Quest> Quests,
    IReadOnlyList<NpcSeed> NpcSeeds,
    IReadOnlyList<Region> Regions,
    IReadOnlyList<PlotHook> PlotHooks,
    DateTime CreatedAt)
{
}

public record PlayerCharacter(
    string Name,
    string Backstory,
    Dictionary<string, int> Stats,
    List<string> Abilities);

public record Quest(
    string Id,
    string Title,
    string Description,
    QuestStatus Status,
    IReadOnlyList<string> Objectives);

public enum QuestStatus
{
    NotStarted,
    Active,
    Completed,
    Failed
}

public record NpcSeed(
    string Id,
    string Name,
    string Role,
    string Personality,
    string Location);

public record Region(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Locations);

public record PlotHook(
    string Id,
    string Description,
    string Trigger);