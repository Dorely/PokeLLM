using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.State;

// <summary>
// Adventure Module represents the persistent game world configuration and story structure.
// This is generated once by the SetupAgent and used throughout the game session.
// </summary>
public class AdventureModule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Theme { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;

    // World Configuration
    public WorldConfiguration World { get; set; } = new();
    
    // Story Elements
    public List<QuestTemplate> Quests { get; set; } = new();
    public List<NPCTemplate> NPCs { get; set; } = new();
    public List<LocationTemplate> Locations { get; set; } = new();
    
    // Game Rules and Mechanics
    public Dictionary<string, object> Rules { get; set; } = new();
    public List<string> AvailableStarters { get; set; } = new();
    
    /// <summary>
    /// Serializes the Adventure Module to JSON for persistence
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Deserializes an Adventure Module from JSON
    /// </summary>
    public static AdventureModule FromJson(string json)
    {
        return JsonSerializer.Deserialize<AdventureModule>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? new AdventureModule();
    }

    /// <summary>
    /// Creates a read-only snapshot of this Adventure Module for use by agents
    /// </summary>
    public AdventureModuleSnapshot CreateSnapshot()
    {
        return new AdventureModuleSnapshot
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Theme = Theme,
            CreatedAt = CreatedAt,
            Version = Version,
            WorldSummary = World.GetSummary(),
            ActiveQuests = Quests.Where(q => q.IsActive).Select(q => q.CreateSummary()).ToList(),
            KeyNPCs = NPCs.Where(n => n.IsImportant).Select(n => n.CreateSummary()).ToList(),
            AvailableLocations = Locations.Select(l => l.CreateSummary()).ToList(),
            CoreRules = Rules.Where(r => r.Key.StartsWith("core_")).ToDictionary(r => r.Key, r => r.Value)
        };
    }
}

/// <summary>
/// Read-only snapshot of Adventure Module for use by agents (prevents accidental modifications)
/// </summary>
public class AdventureModuleSnapshot
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Theme { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int Version { get; init; }
    
    public string WorldSummary { get; init; } = "";
    public List<QuestSummary> ActiveQuests { get; init; } = new();
    public List<NPCSummary> KeyNPCs { get; init; } = new();
    public List<LocationSummary> AvailableLocations { get; init; } = new();
    public Dictionary<string, object> CoreRules { get; init; } = new();
}

public class WorldConfiguration
{
    public string Region { get; set; } = "Kanto";
    public string Setting { get; set; } = "Classic Pokemon Adventure";
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Normal;
    public List<string> EnabledMechanics { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();

    public string GetSummary()
    {
        return $"{Setting} in the {Region} region (Difficulty: {Difficulty})";
    }
}

public enum DifficultyLevel
{
    Easy,
    Normal,
    Hard,
    Expert
}

public class QuestTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public QuestType Type { get; set; } = QuestType.Main;
    public bool IsActive { get; set; } = true;
    public List<string> Prerequisites { get; set; } = new();
    public List<QuestObjective> Objectives { get; set; } = new();
    public Dictionary<string, object> Rewards { get; set; } = new();

    public QuestSummary CreateSummary()
    {
        return new QuestSummary
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Type = Type.ToString(),
            ObjectiveCount = Objectives.Count,
            CompletedObjectives = Objectives.Count(o => o.IsCompleted)
        };
    }
}

public class QuestSummary
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Type { get; init; } = "";
    public int ObjectiveCount { get; init; }
    public int CompletedObjectives { get; init; }
}

public enum QuestType
{
    Main,
    Side,
    Daily,
    Event
}

public class QuestObjective
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = "";
    public bool IsCompleted { get; set; } = false;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class NPCTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Role { get; set; } = "";
    public string Personality { get; set; } = "";
    public string Location { get; set; } = "";
    public bool IsImportant { get; set; } = false;
    public List<string> Dialogue { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();

    public NPCSummary CreateSummary()
    {
        return new NPCSummary
        {
            Id = Id,
            Name = Name,
            Role = Role,
            Location = Location,
            PersonalityBrief = Personality.Length > 100 ? Personality.Substring(0, 100) + "..." : Personality
        };
    }
}

public class NPCSummary
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string Location { get; init; } = "";
    public string PersonalityBrief { get; init; } = "";
}

public class LocationTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Region { get; set; } = "";
    public LocationType Type { get; set; } = LocationType.Route;
    public List<string> ConnectedLocations { get; set; } = new();
    public List<string> AvailablePokemon { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();

    public LocationSummary CreateSummary()
    {
        return new LocationSummary
        {
            Id = Id,
            Name = Name,
            Type = Type.ToString(),
            Region = Region,
            PokemonCount = AvailablePokemon.Count,
            FeatureCount = Features.Count
        };
    }
}

public class LocationSummary
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Region { get; init; } = "";
    public int PokemonCount { get; init; }
    public int FeatureCount { get; init; }
}

public enum LocationType
{
    Route,
    City,
    Town,
    Gym,
    PokemonCenter,
    PokeMart,
    Cave,
    Forest,
    Mountain,
    Lake,
    Ocean,
    Building,
    Landmark
}

// <summary>
// Repository for managing Adventure Module persistence
// </summary>
public interface IAdventureModuleRepository
{
    Task<AdventureModule?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AdventureModule> SaveAsync(AdventureModule module, CancellationToken cancellationToken = default);
    Task<List<AdventureModule>> GetByPlayerAsync(string playerId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

// <summary>
// In-memory implementation of Adventure Module repository for development
// </summary>
public class InMemoryAdventureModuleRepository : IAdventureModuleRepository
{
    private readonly Dictionary<string, AdventureModule> _modules = new();
    private readonly Dictionary<string, List<string>> _playerModules = new();

    public Task<AdventureModule?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _modules.TryGetValue(id, out var module);
        return Task.FromResult(module);
    }

    public Task<AdventureModule> SaveAsync(AdventureModule module, CancellationToken cancellationToken = default)
    {
        _modules[module.Id] = module;
        return Task.FromResult(module);
    }

    public Task<List<AdventureModule>> GetByPlayerAsync(string playerId, CancellationToken cancellationToken = default)
    {
        if (!_playerModules.TryGetValue(playerId, out var moduleIds))
        {
            return Task.FromResult(new List<AdventureModule>());
        }

        var modules = moduleIds
            .Where(id => _modules.ContainsKey(id))
            .Select(id => _modules[id])
            .ToList();

        return Task.FromResult(modules);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _modules.Remove(id);
        return Task.CompletedTask;
    }
}