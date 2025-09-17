using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Handles the adventure setup phase, orchestrating module authoring and player customization without external vector dependencies.
/// </summary>
public class GameSetupPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IAdventureModuleRepository _moduleRepository;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameSetupPhasePlugin(
        IGameStateRepository gameStateRepo,
        IAdventureModuleRepository moduleRepository,
        ICharacterManagementService characterManagementService)
    {
        _gameStateRepo = gameStateRepo;
        _moduleRepository = moduleRepository;
        _characterManagementService = characterManagementService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Setup State Helpers

    [KernelFunction("get_setup_state")]
    [Description("Retrieve the current session, module, and player setup state for planning the next setup step.")]
    public async Task<string> GetSetupState()
    {
        try
        {
            var session = await _gameStateRepo.LoadLatestStateAsync();
            var module = await LoadModuleAsync();

            var result = new
            {
                session = new
                {
                    session.SessionId,
                    session.SessionName,
                    session.IsSetupComplete,
                    session.CurrentPhase,
                    session.Region,
                    session.Metadata.SessionStartTime,
                    session.Metadata.LastUpdatedTime,
                    session.Metadata.CurrentContext
                },
                module = new
                {
                    module.Metadata.ModuleId,
                    module.Metadata.Title,
                    module.Metadata.Summary,
                    module.Metadata.IsSetupComplete,
                    module.World.Setting,
                    module.World.TimePeriod,
                    module.World.MaturityRating,
                    module.World.Themes,
                    module.World.AdventureHooks,
                    module.World.SafetyConsiderations,
                    classCount = module.CharacterClasses.Count
                },
                player = new
                {
                    session.Player.Name,
                    ClassId = session.Player.CharacterDetails.Class,
                    session.Player.Level,
                    Stats = new
                    {
                        session.Player.Stats.Strength,
                        session.Player.Stats.Dexterity,
                        session.Player.Stats.Constitution,
                        session.Player.Stats.Intelligence,
                        session.Player.Stats.Wisdom,
                        session.Player.Stats.Charisma
                    }
                }
            };

            return JsonSerializer.Serialize(new { success = true, data = result }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Module Overview

    [KernelFunction("update_module_overview")]
    [Description("Update top-level module overview metadata like title, summary, setting, tone, time period, and safety notes.")]
    public async Task<string> UpdateModuleOverview([Description("Overview fields to upsert")] ModuleOverviewUpdate update)
    {
        try
        {
            if (update is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Update payload cannot be null" }, _jsonOptions);
            }

            var session = await _gameStateRepo.LoadLatestStateAsync();
            var module = await LoadModuleAsync();

            if (!string.IsNullOrWhiteSpace(update.Title))
            {
                module.Metadata.Title = update.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(update.Summary))
            {
                module.Metadata.Summary = update.Summary.Trim();
            }

            if (!string.IsNullOrWhiteSpace(update.Setting))
            {
                module.World.Setting = update.Setting.Trim();
            }

            if (!string.IsNullOrWhiteSpace(update.Tone))
            {
                module.World.Tone = update.Tone.Trim();
            }

            if (!string.IsNullOrWhiteSpace(update.TimePeriod))
            {
                module.World.TimePeriod = update.TimePeriod.Trim();
            }

            if (!string.IsNullOrWhiteSpace(update.MaturityRating))
            {
                module.World.MaturityRating = update.MaturityRating.Trim();
            }

            if (!string.IsNullOrWhiteSpace(update.StartingContext))
            {
                module.World.StartingContext = update.StartingContext.Trim();
            }

            if (update.Themes is not null)
            {
                module.World.Themes = update.Themes.Where(static t => !string.IsNullOrWhiteSpace(t))
                    .Select(static t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (update.AdventureHooks is not null)
            {
                module.World.AdventureHooks = update.AdventureHooks.Where(static h => !string.IsNullOrWhiteSpace(h))
                    .Select(static h => h.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            await SaveModuleAsync(module, session);


            return JsonSerializer.Serialize(new
            {
                success = true,
                moduleTitle = module.Metadata.Title,
                region = session.Region,
                summary = module.Metadata.Summary
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("remove_character_class")]
    [Description("Remove a character class from the module.")]
    public async Task<string> RemoveCharacterClass([Description("Class id to remove")] string classId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Class id cannot be empty" }, _jsonOptions);
            }

            var module = await LoadModuleAsync();
            var removed = module.CharacterClasses.Remove(classId.Trim());

            if (removed)
            {
                await SaveModuleAsync(module);
            }

            return JsonSerializer.Serialize(new
            {
                success = removed,
                classId,
                moduleClassCount = module.CharacterClasses.Count
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("list_character_classes")]
    [Description("List all character classes currently defined in the module.")]
    public async Task<string> ListCharacterClasses()
    {
        try
        {
            var module = await LoadModuleAsync();
            var classes = module.CharacterClasses.Select(pair => new
            {
                id = pair.Key,
                pair.Value.Name,
                pair.Value.Description,
                statModifiers = pair.Value.StatModifiers,
                startingAbilities = pair.Value.StartingAbilities,
                tags = pair.Value.Tags
            });

            return JsonSerializer.Serialize(new { success = true, classes }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("set_player_class_choice")]
    [Description("Assign the player's chosen class based on a module class id and update their trainer data.")]
    public async Task<string> SetPlayerClassChoice([Description("Class id to assign")] string classId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Class id cannot be empty" }, _jsonOptions);
            }

            var module = await LoadModuleAsync();
            if (!module.CharacterClasses.TryGetValue(classId.Trim(), out var classData))
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Class '{classId}' not found in module." }, _jsonOptions);
            }

            var session = await _gameStateRepo.LoadLatestStateAsync();
            session.Player.CharacterDetails.Class = classId.Trim();
            session.Player.TrainerClassData = ConvertModuleClass(classId.Trim(), classData);
            session.Player.Abilities = classData.StartingAbilities?.ToList() ?? new List<string>();

            await _characterManagementService.SetPlayerClass(classId.Trim());
            await _gameStateRepo.SaveStateAsync(session);

            return JsonSerializer.Serialize(new
            {
                success = true,
                classId = classId.Trim(),
                className = classData.Name,
                startingAbilities = session.Player.Abilities
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Player Customization

    [KernelFunction("set_player_name")]
    [Description("Set the player's trainer name.")]
    public async Task<string> SetPlayerName([Description("Trainer name")] string name)
    {
        try
        {
            await _characterManagementService.SetPlayerName(name);
            var session = await _gameStateRepo.LoadLatestStateAsync();
            return JsonSerializer.Serialize(new { success = true, playerName = session.Player.Name }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error setting player name: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("set_player_stats")]
    [Description("Set the player's ability scores using the order Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma.")]
    public async Task<string> SetPlayerStats([Description("Array of 6 ability scores")] int[] stats)
    {
        try
        {
            if (stats is null || stats.Length != 6)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Must provide exactly 6 ability scores" }, _jsonOptions);
            }

            foreach (var stat in stats)
            {
                if (stat < 3 || stat > 20)
                {
                    return JsonSerializer.Serialize(new { success = false, error = $"Ability scores must be between 3 and 20. Invalid value: {stat}" }, _jsonOptions);
                }
            }

            await _characterManagementService.SetPlayerStats(stats);

            var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            var statDict = statNames.Zip(stats, (name, value) => new { name, value }).ToDictionary(x => x.name, x => x.value);

            return JsonSerializer.Serialize(new { success = true, stats = statDict }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error setting player stats: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("generate_random_stats")]
    [Description("Generate random ability scores using the 4d6 drop lowest method.")]
    public async Task<string> GenerateRandomStats()
    {
        try
        {
            var stats = await _characterManagementService.GenerateRandomStats();

            var result = new
            {
                success = true,
                stats,
                total = stats.Sum(),
                average = stats.Average(),
                description = "Generated using 4d6 drop lowest method"
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [KernelFunction("generate_standard_stats")]
    [Description("Provide the standard ability score array (15, 14, 13, 12, 10, 8).")]
    public async Task<string> GenerateStandardStats()
    {
        try
        {
            var stats = await _characterManagementService.GenerateStandardStats();
            var result = new
            {
                success = true,
                stats,
                total = stats.Sum(),
                description = "Standard array: 15, 14, 13, 12, 10, 8"
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Setup Completion

    [KernelFunction("mark_setup_complete")]
    [Description("Mark setup as complete after confirming the player and module metadata are ready. This transitions to WorldGeneration.")]
    public async Task<string> MarkSetupComplete([Description("Summary of setup choices")] string setupSummary)
    {
        try
        {
            var session = await _gameStateRepo.LoadLatestStateAsync();
            var module = await LoadModuleAsync();

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(session.Region)) missing.Add("region");
            if (string.IsNullOrWhiteSpace(session.Player.Name)) missing.Add("playerName");
            if (string.IsNullOrWhiteSpace(session.Player.CharacterDetails.Class)) missing.Add("playerClass");
            if (session.Player.Stats.Strength == 0 && session.Player.Stats.Dexterity == 0 && session.Player.Stats.Constitution == 0)
            {
                missing.Add("playerStats");
            }
            if (string.IsNullOrWhiteSpace(module.World.Setting)) missing.Add("moduleSetting");
            if (module.CharacterClasses.Count == 0) missing.Add("characterClasses");

            if (missing.Count > 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Setup incomplete", missing }, _jsonOptions);
            }

            await SaveModuleAsync(module, session);

            session.IsSetupComplete = true;
            session.Metadata.PhaseChangeSummary = string.IsNullOrWhiteSpace(setupSummary)
                ? "Game setup completed."
                : setupSummary;
            session.CurrentPhase = GamePhase.WorldGeneration;
            session.AdventureSummary = string.IsNullOrWhiteSpace(setupSummary)
                ? module.Metadata.Summary
                : setupSummary;

            await _gameStateRepo.SaveStateAsync(session);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Setup complete. Transitioning to WorldGeneration phase.",
                session.SessionName,
                session.CurrentPhase
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Internal Helpers

    private async Task<AdventureModule> LoadModuleAsync()
    {
        var session = await _gameStateRepo.LoadLatestStateAsync();
        var moduleFileName = !string.IsNullOrWhiteSpace(session.Module.ModuleFileName)
            ? session.Module.ModuleFileName
            : session.Module.ModuleId;

        return await _moduleRepository.LoadByFileNameAsync(moduleFileName);
    }

    private async Task SaveModuleAsync(AdventureModule module, AdventureSessionState? sessionOverride = null)
    {
        var session = sessionOverride ?? await _gameStateRepo.LoadLatestStateAsync();
        var moduleFileName = !string.IsNullOrWhiteSpace(session.Module.ModuleFileName)
            ? session.Module.ModuleFileName
            : module.Metadata.ModuleId;
        var modulePath = _moduleRepository.GetModuleFilePath(moduleFileName);
        await _moduleRepository.SaveAsync(module, modulePath);

        var resolvedFileName = Path.GetFileName(modulePath);
        if (!string.Equals(session.Module.ModuleFileName, resolvedFileName, StringComparison.OrdinalIgnoreCase))
        {
            session.Module.ModuleFileName = resolvedFileName;
            if (sessionOverride is null)
            {
                await _gameStateRepo.SaveStateAsync(session);
            }
        }
    }

    private static TrainerClass ConvertModuleClass(string classId, AdventureModuleCharacterClass classData)
    {
        return new TrainerClass
        {
            Id = classId,
            Name = classData.Name,
            Description = classData.Description,
            StatModifiers = classData.StatModifiers != null
                ? new Dictionary<string, int>(classData.StatModifiers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            StartingAbilities = classData.StartingAbilities?.ToList() ?? new List<string>(),
            StartingItems = new List<string>(),
            Tags = classData.Tags?.ToList() ?? new List<string>(),
            LevelUpTable = ConvertLevelUpTable(classData.LevelUpAbilities)
        };
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

    #endregion

    #region DTOs

    public class ModuleOverviewUpdate
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("setting")]
        public string? Setting { get; set; }

        [JsonPropertyName("tone")]
        public string? Tone { get; set; }

        [JsonPropertyName("timePeriod")]
        public string? TimePeriod { get; set; }

        [JsonPropertyName("maturityRating")]
        public string? MaturityRating { get; set; }

        [JsonPropertyName("startingContext")]
        public string? StartingContext { get; set; }

        [JsonPropertyName("themes")]
        public List<string>? Themes { get; set; }

        [JsonPropertyName("adventureHooks")]
        public List<string>? AdventureHooks { get; set; }

        [JsonPropertyName("safetyConsiderations")]
        public List<string>? SafetyConsiderations { get; set; }
    }

    public class CharacterClassDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("statModifiers")]
        public Dictionary<string, int>? StatModifiers { get; set; }

        [JsonPropertyName("startingAbilities")]
        public List<string>? StartingAbilities { get; set; }

        [JsonPropertyName("levelUpAbilities")]
        public Dictionary<int, List<string>>? LevelUpAbilities { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    #endregion
}
