using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PokeLLM.GameState.Models;

namespace PokeLLM.GameState;

public interface IAdventureModuleRepository
{
    AdventureModule CreateNewModule(string title = "", string summary = "");
    AdventureModule ApplyChanges(AdventureModule module, Action<AdventureModule> updateAction);
    AdventureSessionState CreateBaselineSession(AdventureModule module);
    AdventureSessionState ApplyModuleBaseline(AdventureModule module, AdventureSessionState session, bool preservePlayer);
    Task SaveAsync(AdventureModule module, string? filePath = null, CancellationToken cancellationToken = default);
    Task<AdventureModule> LoadAsync(string filePath, CancellationToken cancellationToken = default);
    Task<AdventureModule> LoadByFileNameAsync(string moduleFileName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureModuleSummary>> ListModulesAsync(CancellationToken cancellationToken = default);
    string GetModuleFilePath(string moduleFileName);
}

public class AdventureModuleRepository : IAdventureModuleRepository
{
    public const string DefaultDirectoryName = "AdventureModules";
    public const string FileExtension = ".json";

    private readonly string _modulesDirectory;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdventureModuleRepository(IOptions<AdventureSessionRepositoryOptions>? options = null)
    {
        var configuredDirectory = options?.Value?.DataDirectory;
        var rootDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), GameStateRepository.DefaultDirectoryName)
            : Path.GetFullPath(configuredDirectory, Directory.GetCurrentDirectory());
        _modulesDirectory = Path.Combine(rootDirectory, DefaultDirectoryName);

        Directory.CreateDirectory(_modulesDirectory);
    }

    public Task<AdventureModule> LoadByFileNameAsync(string moduleFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetModuleFilePath(moduleFileName);
        return LoadAsync(fullPath, cancellationToken);
    }

    public async Task<IReadOnlyList<AdventureModuleSummary>> ListModulesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var summaries = new List<AdventureModuleSummary>();

        foreach (var file in Directory.EnumerateFiles(_modulesDirectory, $"*{FileExtension}", SearchOption.TopDirectoryOnly))
        {
            if (TryReadModuleSummary(file, out var summary))
            {
                summaries.Add(summary);
            }
        }

        return summaries
            .OrderByDescending(s => s.LastModifiedUtc)
            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string GetModuleFilePath(string moduleFileName)
    {
        if (string.IsNullOrWhiteSpace(moduleFileName))
        {
            throw new ArgumentException("Module file name cannot be null or empty.", nameof(moduleFileName));
        }

        var normalized = moduleFileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
            ? moduleFileName
            : $"{moduleFileName}{FileExtension}";

        var combinedPath = Path.Combine(_modulesDirectory, normalized);
        var fullPath = Path.GetFullPath(combinedPath, _modulesDirectory);
        if (!fullPath.StartsWith(_modulesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Module file path must reside within the configured modules directory.");
        }

        return fullPath;
    }

    public AdventureModule CreateNewModule(string title = "", string summary = "")
    {
        var module = new AdventureModule
        {
            Metadata =
            {
                Title = title,
                Summary = summary,
                CreatedAt = DateTime.UtcNow
            }
        };

        return module;
    }

    public AdventureSessionState ApplyModuleBaseline(AdventureModule module, AdventureSessionState session, bool preservePlayer)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        PlayerState? preservedPlayer = null;
        if (preservePlayer)
        {
            preservedPlayer = session.Player;
        }

        session.Module.ModuleId = module.Metadata.ModuleId;
        session.Module.ModuleTitle = module.Metadata.Title;
        session.Module.ModuleVersion = module.Metadata.Version;
        session.Module.ModuleChecksum = module.Metadata.ModuleId;

        PopulateBaselineFromModule(module, session.Baseline);

        session.Metadata.IsSetupComplete = module.Metadata.IsSetupComplete;
        if (!string.IsNullOrWhiteSpace(module.World.StartingContext) && string.IsNullOrWhiteSpace(session.Metadata.CurrentContext))
        {
            session.Metadata.CurrentContext = module.World.StartingContext;
        }

        if (!string.IsNullOrWhiteSpace(module.Metadata.Summary) && string.IsNullOrWhiteSpace(session.AdventureSummary))
        {
            session.AdventureSummary = module.Metadata.Summary;
        }

        if (preservePlayer && preservedPlayer is not null)
        {
            session.Player = preservedPlayer;
        }

        return session;
    }

    public AdventureModule ApplyChanges(AdventureModule module, Action<AdventureModule> updateAction)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (updateAction is null)
        {
            throw new ArgumentNullException(nameof(updateAction));
        }

        updateAction(module);
        return module;
    }

    public AdventureSessionState CreateBaselineSession(AdventureModule module)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        var session = new AdventureSessionState
        {
            Metadata =
            {
                CurrentPhase = GamePhase.GameSetup,
                CurrentContext = module.World.StartingContext,
                PhaseChangeSummary = string.Empty,
                GameTurnNumber = 0,
                SessionName = string.IsNullOrWhiteSpace(module.Metadata.Title) ? $"Session {DateTime.UtcNow:yyyyMMddHHmmss}" : module.Metadata.Title,
                IsSetupComplete = module.Metadata.IsSetupComplete
            }
        };

        ApplyModuleBaseline(module, session, preservePlayer: false);
        return session;
    }

    private static void PopulateBaselineFromModule(AdventureModule module, AdventureSessionBaselineSnapshot baseline)
    {
        var moduleItems = module.Items != null
            ? new Dictionary<string, AdventureModuleItem>(module.Items, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AdventureModuleItem>(StringComparer.OrdinalIgnoreCase);

        var moduleAbilities = module.Abilities != null
            ? new Dictionary<string, AdventureModuleAbility>(module.Abilities, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AdventureModuleAbility>(StringComparer.OrdinalIgnoreCase);

        var characterClasses = module.CharacterClasses != null
            ? new Dictionary<string, AdventureModuleCharacterClass>(module.CharacterClasses, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AdventureModuleCharacterClass>(StringComparer.OrdinalIgnoreCase);

        var speciesLookup = module.Bestiary != null
            ? new Dictionary<string, AdventureModuleCreatureSpecies>(module.Bestiary, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AdventureModuleCreatureSpecies>(StringComparer.OrdinalIgnoreCase);

        var movesLookup = module.Moves != null
            ? new Dictionary<string, AdventureModuleMove>(module.Moves, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AdventureModuleMove>(StringComparer.OrdinalIgnoreCase);

        var creatureInstances = module.CreatureInstances != null
            ? new Dictionary<string, AdventureModuleCreatureInstance>(module.CreatureInstances, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AdventureModuleCreatureInstance>(StringComparer.OrdinalIgnoreCase);

        baseline.Region = DetermineRegion(module);
        baseline.AdventureSummary = module.Metadata?.Summary ?? string.Empty;
        baseline.CurrentLocationId = DetermineStartingLocationId(module);
        baseline.Player = CreateBaselinePlayer(module, moduleAbilities, characterClasses);

        var pokemonByLocation = creatureInstances
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.LocationId))
            .GroupBy(entry => entry.Value.LocationId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var pokemonByOwner = creatureInstances
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.OwnerNpcId))
            .GroupBy(entry => entry.Value.OwnerNpcId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var locations = module.Locations?.Values ?? Enumerable.Empty<AdventureModuleLocation>();
        baseline.WorldLocations = locations
            .Where(location => !string.IsNullOrWhiteSpace(location.LocationId))
            .Select(location => ConvertLocation(location, pokemonByLocation))
            .ToDictionary(location => location.Id, location => location, StringComparer.OrdinalIgnoreCase);

        var npcs = module.Npcs?.Values ?? Enumerable.Empty<AdventureModuleNpc>();
        baseline.WorldNpcs = npcs
            .Where(npc => !string.IsNullOrWhiteSpace(npc.NpcId))
            .Select(npc => ConvertNpc(npc, pokemonByOwner, moduleItems))
            .ToDictionary(npc => npc.Id, npc => npc, StringComparer.OrdinalIgnoreCase);

        baseline.WorldPokemon = creatureInstances
            .ToDictionary(
                entry => entry.Key,
                entry => ConvertPokemon(entry.Key, entry.Value, speciesLookup, movesLookup),
                StringComparer.OrdinalIgnoreCase);

        baseline.Items = moduleItems
            .ToDictionary(
                entry => entry.Key,
                entry => ConvertItem(entry.Value),
                StringComparer.OrdinalIgnoreCase);

        baseline.QuestStates = module.QuestLines?.Keys
            .ToDictionary(questId => questId, _ => "NotStarted", StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        baseline.RecentEvents = new List<EventLog>();
    }

    private static string DetermineRegion(AdventureModule module)
    {
        if (module.Locations != null)
        {
            foreach (var location in module.Locations.Values)
            {
                if (!string.IsNullOrWhiteSpace(location.Region))
                {
                    return location.Region;
                }
            }
        }

        return module.World?.Setting ?? string.Empty;
    }

    private static string DetermineStartingLocationId(AdventureModule module)
    {
        if (module.Locations is null || module.Locations.Count == 0)
        {
            return string.Empty;
        }

        var taggedStart = module.Locations.Values
            .FirstOrDefault(l => l.Tags?.Any(tag => tag.Equals("starting", StringComparison.OrdinalIgnoreCase)) == true);

        return taggedStart?.LocationId
            ?? module.Locations.Values.First().LocationId
            ?? string.Empty;
    }

    private static PlayerState CreateBaselinePlayer(
        AdventureModule module,
        IReadOnlyDictionary<string, AdventureModuleAbility> moduleAbilities,
        IReadOnlyDictionary<string, AdventureModuleCharacterClass> characterClasses)
    {
        var player = new PlayerState
        {
            Name = "Player",
            Description = module.World?.StartingContext ?? string.Empty,
            TrainerClassData = new TrainerClass(),
            CharacterDetails = new CharacterDetails()
        };

        if (characterClasses.Count == 0)
        {
            player.CharacterDetails.Inventory = new List<ItemInstance>();
            return player;
        }

        var selectedClassEntry = characterClasses.First();
        var classId = selectedClassEntry.Key;
        var classData = selectedClassEntry.Value;

        player.TrainerClassData = new TrainerClass
        {
            Id = classId,
            Name = classData.Name,
            Description = classData.Description,
            StatModifiers = classData.StatModifiers != null
                ? new Dictionary<string, int>(classData.StatModifiers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            StartingAbilities = classData.StartingAbilities?.ToList() ?? new List<string>(),
            StartingPerks = classData.StartingPerks?.ToList() ?? new List<string>(),
            StartingItems = new List<string>(),
            Tags = classData.Tags?.ToList() ?? new List<string>(),
            LevelUpTable = ConvertLevelUpTable(classData.LevelUpAbilities),
            LevelUpPerks = ConvertLevelUpTable(classData.LevelUpPerks)
        };


        var abilityNames = classData.StartingAbilities?
            .Select(id => moduleAbilities.TryGetValue(id, out var ability) ? ability.Name : id)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList() ?? new List<string>();

        player.Abilities = abilityNames;
        player.Perks = classData.StartingPerks?.ToList() ?? new List<string>();
        player.CharacterDetails.Class = classId;
        player.CharacterDetails.Inventory = new List<ItemInstance>();

        return player;
    }

    private static Dictionary<int, string> ConvertLevelUpTable(Dictionary<int, List<string>>? source)
    {
        if (source is null)
        {
            return new Dictionary<int, string>();
        }

        var table = new Dictionary<int, string>();
        foreach (var entry in source)
        {
            var value = entry.Value != null ? string.Join(", ", entry.Value) : string.Empty;
            table[entry.Key] = value;
        }

        return table;
    }

    private static List<ItemInstance> CreateInventoryFromItemInstances(IEnumerable<ItemInstance>? items, IReadOnlyDictionary<string, AdventureModuleItem> moduleItems)
    {
        if (items is null)
        {
            return new List<ItemInstance>();
        }

        return items
            .Where(item => item is not null)
            .Select(item => EnrichItemInstance(item, moduleItems))
            .ToList();
    }

    private static ItemInstance EnrichItemInstance(ItemInstance item, IReadOnlyDictionary<string, AdventureModuleItem> moduleItems)
    {
        if (moduleItems.TryGetValue(item.ItemId, out var moduleItem))
        {
            var enriched = ConvertItem(moduleItem, quantityOverride: item.Quantity > 0 ? item.Quantity : (moduleItem.DefaultQuantity > 0 ? moduleItem.DefaultQuantity : 1));
            if (item.Quantity > 0)
            {
                enriched.Quantity = item.Quantity;
            }
            return enriched;
        }

        return new ItemInstance
        {
            ItemId = item.ItemId,
            Name = string.IsNullOrWhiteSpace(item.Name) ? item.ItemId : item.Name,
            Quantity = item.Quantity > 0 ? item.Quantity : 1
        };
    }

    private static CharacterDetails CloneCharacterDetails(CharacterDetails? source, IReadOnlyDictionary<string, AdventureModuleItem> moduleItems)
    {
        if (source is null)
        {
            return new CharacterDetails();
        }

        return new CharacterDetails
        {
            Class = source.Class,
            Inventory = CreateInventoryFromItemInstances(source.Inventory, moduleItems),
            Money = source.Money,
            GlobalRenown = source.GlobalRenown,
            GlobalNotoriety = source.GlobalNotoriety
        };
    }

    private static ItemInstance ConvertItem(AdventureModuleItem item, int? quantityOverride = null)
    {
        var placement = item.Placement?
            .Select(CloneItemPlacement)
            .ToList() ?? new List<AdventureModuleItemPlacement>();

        var quantity = quantityOverride ?? (item.DefaultQuantity > 0 ? item.DefaultQuantity : 1);

        return new ItemInstance
        {
            ItemId = item.ItemId,
            Name = item.Name,
            Quantity = quantity,
            Rarity = item.Rarity,
            FullDescription = item.FullDescription,
            Effects = item.Effects,
            Notes = string.Empty,
            Placement = placement
        };
    }

    private static AdventureModuleItemPlacement CloneItemPlacement(AdventureModuleItemPlacement placement)
    {
        return new AdventureModuleItemPlacement
        {
            LocationId = placement.LocationId,
            NpcId = placement.NpcId,
            Notes = placement.Notes
        };
    }

    private static Location ConvertLocation(AdventureModuleLocation source, IReadOnlyDictionary<string, List<string>> pokemonByLocation)
    {
        var presentPokemon = pokemonByLocation.TryGetValue(source.LocationId ?? string.Empty, out var pokemonList)
            ? new List<string>(pokemonList)
            : new List<string>();

        return new Location
        {
            Id = source.LocationId ?? string.Empty,
            Name = source.Name,
            Summary = source.Summary,
            FullDescription = source.FullDescription,
            Region = source.Region,
            Tags = source.Tags?.ToList() ?? new List<string>(),
            FactionsPresent = source.FactionsPresent?.ToList() ?? new List<string>(),
            DescriptionVectorId = source.LocationId ?? string.Empty,
            PointsOfInterest = source.PointsOfInterest?.ToDictionary(poi => poi.Id, poi => poi.Description, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PointsOfInterestDetails = source.PointsOfInterest?.Select(ClonePointOfInterest).ToList() ?? new List<AdventureModulePointOfInterest>(),
            Encounters = source.Encounters?.Select(CloneEncounter).ToList() ?? new List<AdventureModuleEncounter>(),
            Exits = source.ConnectedLocations?.ToDictionary(conn => conn.Direction, conn => conn.TargetLocationId, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PresentNpcIds = ExtractNpcIds(source),
            PresentPokemonIds = presentPokemon
        };
    }

    private static List<string> ExtractNpcIds(AdventureModuleLocation location)
    {
        if (location.PointsOfInterest is null)
        {
            return new List<string>();
        }

        return location.PointsOfInterest
            .SelectMany(poi => poi.RelatedNpcIds ?? Enumerable.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdventureModulePointOfInterest ClonePointOfInterest(AdventureModulePointOfInterest poi)
    {
        return new AdventureModulePointOfInterest
        {
            Id = poi.Id,
            Name = poi.Name,
            Description = poi.Description,
            RelatedNpcIds = poi.RelatedNpcIds?.ToList() ?? new List<string>(),
            RelatedItemIds = poi.RelatedItemIds?.ToList() ?? new List<string>()
        };
    }

    private static AdventureModuleEncounter CloneEncounter(AdventureModuleEncounter encounter)
    {
        return new AdventureModuleEncounter
        {
            EncounterId = encounter.EncounterId,
            Type = encounter.Type,
            Trigger = encounter.Trigger,
            Difficulty = encounter.Difficulty,
            Narrative = encounter.Narrative,
            Participants = encounter.Participants?.ToList() ?? new List<string>(),
            Outcomes = encounter.Outcomes?.Select(CloneOutcome).ToList() ?? new List<AdventureModuleOutcome>()
        };
    }

    private static AdventureModuleOutcome CloneOutcome(AdventureModuleOutcome outcome)
    {
        return new AdventureModuleOutcome
        {
            Description = outcome.Description,
            ResultingChanges = outcome.ResultingChanges?.Select(CloneStateChange).ToList() ?? new List<AdventureModuleStateChange>()
        };
    }

    private static AdventureModuleStateChange CloneStateChange(AdventureModuleStateChange change)
    {
        return new AdventureModuleStateChange
        {
            ChangeType = change.ChangeType,
            TargetType = change.TargetType,
            TargetId = change.TargetId,
            Payload = change.Payload
        };
    }

    private static Npc ConvertNpc(
        AdventureModuleNpc source,
        IReadOnlyDictionary<string, List<string>> pokemonByOwner,
        IReadOnlyDictionary<string, AdventureModuleItem> moduleItems)
    {
        var npc = new Npc
        {
            Id = source.NpcId,
            Name = source.Name,
            Role = source.Role,
            Motivation = source.Motivation,
            FullDescription = source.FullDescription,
            Stats = CloneStats(source.Stats),
            CharacterDetails = CloneCharacterDetails(source.CharacterDetails, moduleItems),
            Factions = source.Factions?.ToList() ?? new List<string>(),
            Relationships = source.Relationships?.Select(CloneRelationship).ToList() ?? new List<AdventureModuleRelationship>(),
            DialogueScripts = source.DialogueScripts?.Select(CloneDialogueScript).ToList() ?? new List<AdventureModuleDialogueScript>()
        };

        if (pokemonByOwner.TryGetValue(source.NpcId, out var ownedPokemon))
        {
            npc.PokemonOwned = new List<string>(ownedPokemon);
            npc.IsTrainer = ownedPokemon.Count > 0;
        }
        else
        {
            npc.PokemonOwned = new List<string>();
            npc.IsTrainer = false;
        }

        return npc;
    }

    private static AdventureModuleRelationship CloneRelationship(AdventureModuleRelationship relationship)
    {
        return new AdventureModuleRelationship
        {
            TargetId = relationship.TargetId,
            Type = relationship.Type,
            Summary = relationship.Summary
        };
    }

    private static AdventureModuleDialogueScript CloneDialogueScript(AdventureModuleDialogueScript script)
    {
        return new AdventureModuleDialogueScript
        {
            ScriptId = script.ScriptId,
            Context = script.Context,
            Lines = script.Lines?.Select(CloneDialogueLine).ToList() ?? new List<AdventureModuleDialogueLine>()
        };
    }

    private static AdventureModuleDialogueLine CloneDialogueLine(AdventureModuleDialogueLine line)
    {
        return new AdventureModuleDialogueLine
        {
            Speaker = line.Speaker,
            Content = line.Content,
            Notes = line.Notes
        };
    }

    private static Pokemon ConvertPokemon(
        string instanceId,
        AdventureModuleCreatureInstance creature,
        IReadOnlyDictionary<string, AdventureModuleCreatureSpecies> speciesLookup,
        IReadOnlyDictionary<string, AdventureModuleMove> movesLookup)
    {
        var pokemon = new Pokemon
        {
            Id = instanceId,
            NickName = creature.Nickname,
            Species = creature.SpeciesId,
            Level = creature.Level,
            HeldItem = creature.HeldItem,
            Factions = creature.FactionIds?.ToList() ?? new List<string>(),
            LocationId = creature.LocationId ?? string.Empty,
            OwnerNpcId = creature.OwnerNpcId ?? string.Empty,
            Tags = creature.Tags?.ToList() ?? new List<string>(),
            Notes = creature.Notes ?? string.Empty,
            FullDescription = creature.FullDescription ?? string.Empty
        };

        if (speciesLookup.TryGetValue(creature.SpeciesId, out var species))
        {
            pokemon.Stats = CloneStats(species.BaseStats);
            pokemon.Abilities = species.AbilityIds?.ToList() ?? new List<string>();
        }
        else
        {
            pokemon.Stats = CloneStats(null);
        }

        pokemon.KnownMoves = ConvertMoves(creature.Moves, movesLookup);

        return pokemon;
    }

    private static List<Move> ConvertMoves(IEnumerable<string>? moveIds, IReadOnlyDictionary<string, AdventureModuleMove> movesLookup)
    {
        if (moveIds is null)
        {
            return new List<Move>();
        }

        var moves = new List<Move>();
        foreach (var moveId in moveIds)
        {
            if (string.IsNullOrWhiteSpace(moveId))
            {
                continue;
            }

            if (movesLookup.TryGetValue(moveId, out var moveData))
            {
                moves.Add(ConvertMove(moveId, moveData));
            }
            else
            {
                moves.Add(new Move
                {
                    Id = moveId,
                    Name = moveId
                });
            }
        }

        return moves;
    }

    private static Move ConvertMove(string moveId, AdventureModuleMove moveData)
    {
        var move = new Move
        {
            Id = moveId,
            Name = moveData.Name,
            DamageDice = moveData.DamageDice,
            VigorCost = moveData.VigorCost,
            Description = moveData.Description
        };

        if (Enum.TryParse(moveData.Category, true, out MoveCategory category))
        {
            move.Category = category;
        }

        if (Enum.TryParse(moveData.Type, true, out PokemonType type))
        {
            move.Type = type;
        }

        return move;
    }

    private static Stats CloneStats(Stats? source)
    {
        if (source is null)
        {
            return new Stats();
        }

        return new Stats
        {
            CurrentVigor = source.CurrentVigor,
            MaxVigor = source.MaxVigor,
            Strength = source.Strength,
            Dexterity = source.Dexterity,
            Constitution = source.Constitution,
            Intelligence = source.Intelligence,
            Wisdom = source.Wisdom,
            Charisma = source.Charisma
        };
    }

    public async Task SaveAsync(AdventureModule module, string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        var targetPath = filePath ?? GetDefaultFilePath(module.Metadata.ModuleId);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(targetPath);
        await JsonSerializer.SerializeAsync(stream, module, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdventureModule> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        await using var stream = File.OpenRead(filePath);
        var module = await JsonSerializer.DeserializeAsync<AdventureModule>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);

        if (module is null)
        {
            throw new InvalidOperationException($"Failed to deserialize adventure module from '{filePath}'.");
        }

        return module;
    }

    private bool TryReadModuleSummary(string filePath, out AdventureModuleSummary summary)
    {
        summary = default!;
        try
        {
            var json = File.ReadAllText(filePath);
            var module = JsonSerializer.Deserialize<AdventureModule>(json, _serializerOptions);
            if (module is null)
            {
                return false;
            }

            summary = new AdventureModuleSummary
            {
                ModuleId = module.Metadata.ModuleId,
                Title = module.Metadata.Title,
                Version = module.Metadata.Version,
                IsSetupComplete = module.Metadata.IsSetupComplete,
                LastModifiedUtc = File.GetLastWriteTimeUtc(filePath),
                FilePath = filePath
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetDefaultFilePath(string moduleId)
    {

        var safeId = string.IsNullOrWhiteSpace(moduleId) ? Guid.NewGuid().ToString("N") : moduleId;
        var fileName = safeId.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
            ? safeId
            : $"{safeId}{FileExtension}";
        return Path.Combine(_modulesDirectory, fileName);
    }
}
