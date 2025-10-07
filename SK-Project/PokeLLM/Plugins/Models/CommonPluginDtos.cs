using System.ComponentModel;

namespace PokeLLM.Game.Plugins.Models;

/// <summary>
/// DTO for search queries used in various plugins
/// </summary>
public class SearchQueriesDto
{
    [Description("List of search queries to execute")]
    public List<string> Queries { get; set; } = new List<string>();
}

/// <summary>
/// DTO for choices/options in random selection functions
/// </summary>
public class ChoicesDto
{
    [Description("List of choices for random selection")]
    public List<string> Choices { get; set; } = new List<string>();
}

/// <summary>
/// DTO for tags used in content categorization
/// </summary>
public class TagsDto
{
    [Description("List of tags for categorization")]
    public List<string> Tags { get; set; } = new List<string>();
}

/// <summary>
/// DTO for involved entities in events
/// </summary>
public class InvolvedEntitiesDto
{
    [Description("List of entities involved in an event")]
    public List<string> Entities { get; set; } = new List<string>();
}

/// <summary>
/// DTO for scene elements used in context searches
/// </summary>
public class SceneElementsDto
{
    [Description("List of scene elements to search for in narrative context")]
    public List<string> Elements { get; set; } = new List<string>();
}

/// <summary>
/// DTO for scene context to avoid complex anonymous objects in UnifiedContextPlugin
/// </summary>
public class SceneContextDto
{
    [Description("The name of the current location")]
    public string LocationName { get; set; } = string.Empty;

    [Description("Detailed description of the current location")]
    public string LocationDescription { get; set; } = string.Empty;

    [Description("JSON string containing available exits from the current location")]
    public string ExitsJson { get; set; } = "{}";

    [Description("JSON string containing points of interest in the current location")]
    public string PointsOfInterestJson { get; set; } = "{}";

    [Description("List of NPCs present at the current location")]
    public List<string> PresentNpcs { get; set; } = new List<string>();

    [Description("List of Pokemon present at the current location")]
    public List<string> PresentPokemon { get; set; } = new List<string>();

    [Description("Current time of day")]
    public string TimeOfDay { get; set; } = string.Empty;

    [Description("Current weather conditions")]
    public string Weather { get; set; } = string.Empty;

    [Description("The current region")]
    public string Region { get; set; } = string.Empty;

    [Description("List of recent events in chronological order")]
    public List<string> RecentEvents { get; set; } = new List<string>();
}

/// <summary>
/// DTO for simple success/failure responses
/// </summary>
public class SimpleResultDto
{
    [Description("Whether the operation was successful")]
    public bool Success { get; set; } = true;

    [Description("A message describing the result or any errors")]
    public string Message { get; set; } = string.Empty;

    [Description("Optional additional data length or count")]
    public int DataLength { get; set; } = 0;
}