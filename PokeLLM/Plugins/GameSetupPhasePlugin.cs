using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    private readonly ILogger<GameSetupPhasePlugin> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const int MinClassLevel = 1;
    private const int MaxClassLevel = 20;

    public GameSetupPhasePlugin(
        IGameStateRepository gameStateRepo,
        IAdventureModuleRepository moduleRepository,
        ICharacterManagementService characterManagementService,
        ILogger<GameSetupPhasePlugin> logger)
    {
        _gameStateRepo = gameStateRepo;
        _moduleRepository = moduleRepository;
        _characterManagementService = characterManagementService;
        _logger = logger;
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
        const string operation = nameof(GetSetupState);
        LogOperationStart(operation, null);
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

            _logger.LogDebug("{Operation} succeeded", operation);
            var response = JsonSerializer.Serialize(new { success = true, data = result }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var response = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
    }

    #endregion

    #region Module Overview

    [KernelFunction("update_module_overview")]
    [Description("Update top-level module overview metadata like title, summary, setting, tone, time period, and safety notes.")]
    public async Task<string> UpdateModuleOverview([Description("Overview fields to upsert")] ModuleOverviewUpdate update)
    {
        const string operation = nameof(UpdateModuleOverview);
        LogOperationStart(operation, update);

        try
        {
            if (update is null)
            {
                _logger.LogWarning("{Operation} received null update payload", operation);
                var nullResponse = JsonSerializer.Serialize(new { success = false, error = "Update payload cannot be null" }, _jsonOptions);
                LogOperationResult(operation, nullResponse);
                return nullResponse;
            }

            var session = await _gameStateRepo.LoadLatestStateAsync();
            var module = await LoadModuleAsync();

            _logger.LogDebug("{Operation} applying updates to module {ModuleId}", operation, module.Metadata.ModuleId);

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
                module.World.Themes = update.Themes
                    .Where(static t => !string.IsNullOrWhiteSpace(t))
                    .Select(static t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (update.AdventureHooks is not null)
            {
                module.World.AdventureHooks = update.AdventureHooks
                    .Where(static h => !string.IsNullOrWhiteSpace(h))
                    .Select(static h => h.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (update.SafetyConsiderations is not null)
            {
                module.World.SafetyConsiderations = update.SafetyConsiderations
                    .Where(static s => !string.IsNullOrWhiteSpace(s))
                    .Select(static s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            await SaveModuleAsync(module, session);

            if (!string.IsNullOrWhiteSpace(module.World.Setting))
            {
                session.Region = module.World.Setting;
            }

            if (!string.IsNullOrWhiteSpace(module.World.StartingContext))
            {
                session.Metadata.CurrentContext = module.World.StartingContext;
            }

            await SaveSessionAsync(session);

            _logger.LogDebug("{Operation} updated module {ModuleId}", operation, module.Metadata.ModuleId);

            var successResponse = JsonSerializer.Serialize(new
            {
                success = true,
                moduleTitle = module.Metadata.Title,
                region = session.Region,
                summary = module.Metadata.Summary
            }, _jsonOptions);
            LogOperationResult(operation, successResponse);
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    #endregion

    #region Character Classes

    [KernelFunction("upsert_character_class")]
    [Description("Create or update a character class definition in the module.")]
    public async Task<string> UpsertCharacterClass([Description("Full class definition")] CharacterClassDefinition definition)
    {
        const string operation = nameof(UpsertCharacterClass);
        LogOperationStart(operation, definition);

        try
        {
            if (definition is null || string.IsNullOrWhiteSpace(definition.Id))
            {
                _logger.LogWarning("{Operation} missing class id", operation);
                var missingResponse = JsonSerializer.Serialize(new { success = false, error = "Class id is required" }, _jsonOptions);
                LogOperationResult(operation, missingResponse);
                return missingResponse;
            }

            var module = await LoadModuleAsync();
            var classId = definition.Id.Trim();
            var isNew = !module.CharacterClasses.ContainsKey(classId);

            module.CharacterClasses[classId] = new AdventureModuleCharacterClass
            {
                Name = string.IsNullOrWhiteSpace(definition.Name) ? classId : definition.Name!.Trim(),
                Description = definition.Description?.Trim() ?? string.Empty,
                StatModifiers = definition.StatModifiers?.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                StartingAbilities = NormalizeStringList(definition.StartingAbilities),
                LevelUpAbilities = NormalizeLevelTable(definition.LevelUpAbilities),
                StartingPerks = NormalizeStringList(definition.StartingPerks),
                LevelUpPerks = NormalizeLevelTable(definition.LevelUpPerks),
                Tags = NormalizeStringList(definition.Tags)
            };

            var persistedClass = module.CharacterClasses[classId];
            var validationErrors = ValidateCharacterClassStructure(persistedClass);

            await SaveModuleAsync(module);

            _logger.LogDebug("{Operation} {Result} class {ClassId}", operation, isNew ? "created" : "updated", classId);
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning(
                    "{Operation} detected structural issues for class {ClassId}: {Issues}",
                    operation,
                    classId,
                    string.Join("; ", validationErrors));
            }

            var response = JsonSerializer.Serialize(new
            {
                success = validationErrors.Count == 0,
                classId,
                moduleClassCount = module.CharacterClasses.Count,
                createdNew = isNew,
                validationErrors = validationErrors.Count > 0 ? validationErrors : null
            }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("remove_character_class")]
    [Description("Remove a character class from the module.")]
    public async Task<string> RemoveCharacterClass([Description("Class id to remove")] string classId)
    {
        const string operation = nameof(RemoveCharacterClass);
        LogOperationStart(operation, new { classId });

        try
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                _logger.LogWarning("{Operation} missing class id", operation);
                var missingResponse = JsonSerializer.Serialize(new { success = false, error = "Class id cannot be empty" }, _jsonOptions);
                LogOperationResult(operation, missingResponse);
                return missingResponse;
            }

            var module = await LoadModuleAsync();
            var trimmedId = classId.Trim();
            var removed = module.CharacterClasses.Remove(trimmedId);

            if (removed)
            {
                await SaveModuleAsync(module);
                _logger.LogDebug("{Operation} removed class {ClassId}", operation, trimmedId);
            }
            else
            {
                _logger.LogDebug("{Operation} found no class {ClassId} to remove", operation, trimmedId);
            }

            var response = JsonSerializer.Serialize(new
            {
                success = removed,
                classId = trimmedId,
                moduleClassCount = module.CharacterClasses.Count
            }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("list_character_classes")]
    [Description("List all character classes currently defined in the module.")]
    public async Task<string> ListCharacterClasses()
    {
        const string operation = nameof(ListCharacterClasses);
        LogOperationStart(operation, null);

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
                startingPerks = pair.Value.StartingPerks,
                levelUpAbilities = pair.Value.LevelUpAbilities,
                levelUpPerks = pair.Value.LevelUpPerks,
                tags = pair.Value.Tags
            });

            _logger.LogDebug("{Operation} returning {Count} classes", operation, module.CharacterClasses.Count);
            var response = JsonSerializer.Serialize(new { success = true, classes }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("set_player_class_choice")]
    [Description("Assign the player's chosen class based on a module class id and update their trainer data.")]
    public async Task<string> SetPlayerClassChoice([Description("Class id to assign")] string classId)
    {
        const string operation = nameof(SetPlayerClassChoice);
        LogOperationStart(operation, new { classId });

        try
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                _logger.LogWarning("{Operation} missing class id", operation);
                var missingResponse = JsonSerializer.Serialize(new { success = false, error = "Class id cannot be empty" }, _jsonOptions);
                LogOperationResult(operation, missingResponse);
                return missingResponse;
            }

            var module = await LoadModuleAsync();
            var trimmedId = classId.Trim();
            if (!module.CharacterClasses.TryGetValue(trimmedId, out var classData))
            {
                _logger.LogWarning("{Operation} could not find class {ClassId}", operation, trimmedId);
                var notFoundResponse = JsonSerializer.Serialize(new { success = false, error = $"Class '{classId}' not found in module." }, _jsonOptions);
                LogOperationResult(operation, notFoundResponse);
                return notFoundResponse;
            }

            var session = await _gameStateRepo.LoadLatestStateAsync();
            session.Player.CharacterDetails.Class = trimmedId;
            session.Player.TrainerClassData = ConvertModuleClass(trimmedId, classData);
            session.Player.Abilities = classData.StartingAbilities?.ToList() ?? new List<string>();
            session.Player.Perks = classData.StartingPerks?.ToList() ?? new List<string>();

            await _characterManagementService.SetPlayerClass(trimmedId);
            await SaveSessionAsync(session);

            _logger.LogDebug("{Operation} assigned class {ClassId} to player", operation, trimmedId);

            var response = JsonSerializer.Serialize(new
            {
                success = true,
                classId = trimmedId,
                className = classData.Name,
                startingAbilities = session.Player.Abilities,
                startingPerks = session.Player.Perks
            }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    #endregion

    #region Player Customization


    [KernelFunction("set_player_name")]
    [Description("Set the player's trainer name.")]
    public async Task<string> SetPlayerName([Description("Trainer name")] string name)
    {
        const string operation = nameof(SetPlayerName);
        LogOperationStart(operation, new { name });

        try
        {
            await _characterManagementService.SetPlayerName(name);
            var session = await _gameStateRepo.LoadLatestStateAsync();
            await SaveSessionAsync(session);
            _logger.LogDebug("{Operation} succeeded", operation);
            var response = JsonSerializer.Serialize(new { success = true, playerName = session.Player.Name }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = $"Error setting player name: {ex.Message}" }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("set_player_stats")]
    [Description("Set the player's ability scores using the order Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma.")]
    public async Task<string> SetPlayerStats([Description("Array of 6 ability scores")] int[] stats)
    {
        const string operation = nameof(SetPlayerStats);
        LogOperationStart(operation, stats);

        try
        {
            if (stats is null || stats.Length != 6)
            {
                _logger.LogWarning("{Operation} received invalid stat array length", operation);
                var lengthResponse = JsonSerializer.Serialize(new { success = false, error = "Must provide exactly 6 ability scores" }, _jsonOptions);
                LogOperationResult(operation, lengthResponse);
                return lengthResponse;
            }

            foreach (var stat in stats)
            {
                if (stat < 3 || stat > 20)
                {
                    _logger.LogWarning("{Operation} received out of range stat value {Stat}", operation, stat);
                    var rangeResponse = JsonSerializer.Serialize(new { success = false, error = $"Ability scores must be between 3 and 20. Invalid value: {stat}" }, _jsonOptions);
                    LogOperationResult(operation, rangeResponse);
                    return rangeResponse;
                }
            }

            await _characterManagementService.SetPlayerStats(stats);

            var statNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            var statDict = statNames.Zip(stats, (name, value) => new { name, value }).ToDictionary(x => x.name, x => x.value);

            _logger.LogDebug("{Operation} succeeded", operation);
            var response = JsonSerializer.Serialize(new { success = true, stats = statDict }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = $"Error setting player stats: {ex.Message}" }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("generate_random_stats")]
    [Description("Provide random ability scores using the 4d6 drop lowest method.")]
    public async Task<string> GenerateRandomStats()
    {
        const string operation = nameof(GenerateRandomStats);
        LogOperationStart(operation, null);

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

            _logger.LogDebug("{Operation} succeeded", operation);
            var response = JsonSerializer.Serialize(result, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("generate_standard_stats")]
    [Description("Provide the standard ability score array (15, 14, 13, 12, 10, 8).")]
    public async Task<string> GenerateStandardStats()
    {
        const string operation = nameof(GenerateStandardStats);
        LogOperationStart(operation, null);

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

            _logger.LogDebug("{Operation} succeeded", operation);
            var response = JsonSerializer.Serialize(result, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    [KernelFunction("mark_setup_complete")]
    [Description("Mark setup as complete after confirming the player and module metadata are ready. This transitions to WorldGeneration.")]
    public async Task<string> MarkSetupComplete([Description("Summary of setup choices")] string setupSummary)
    {
        const string operation = nameof(MarkSetupComplete);
        LogOperationStart(operation, new { setupSummary });

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
                _logger.LogWarning("{Operation} cannot complete setup; missing {Missing}", operation, string.Join(", ", missing));
                var missingResponse = JsonSerializer.Serialize(new { success = false, error = "Setup incomplete", missing }, _jsonOptions);
                LogOperationResult(operation, missingResponse);
                return missingResponse;
            }

            var invalidClasses = module.CharacterClasses
                .Select(pair => new { pair.Key, Issues = ValidateCharacterClassStructure(pair.Value) })
                .Where(result => result.Issues.Count > 0)
                .ToDictionary(result => result.Key, result => (IReadOnlyCollection<string>)result.Issues);

            if (invalidClasses.Count > 0)
            {
                _logger.LogWarning(
                    "{Operation} cannot complete setup; {Count} classes have structural issues",
                    operation,
                    invalidClasses.Count);

                var invalidResponse = JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Class definitions must include starting abilities/perks and level 1-20 rewards before completing setup.",
                    invalidClasses
                }, _jsonOptions);
                LogOperationResult(operation, invalidResponse);
                return invalidResponse;
            }

            await SaveModuleAsync(module, session);

            if (!string.IsNullOrWhiteSpace(module.World.Setting))
            {
                session.Region = module.World.Setting;
            }

            session.IsSetupComplete = true;
            session.Metadata.PhaseChangeSummary = string.IsNullOrWhiteSpace(setupSummary)
                ? "Game setup completed."
                : setupSummary;
            session.CurrentPhase = GamePhase.WorldGeneration;
            session.AdventureSummary = string.IsNullOrWhiteSpace(setupSummary)
                ? module.Metadata.Summary
                : setupSummary;

            await SaveSessionAsync(session);

            _logger.LogDebug("{Operation} succeeded", operation);

            var successResponse = JsonSerializer.Serialize(new
            {
                success = true,
                message = "Setup complete. Transitioning to WorldGeneration phase.",
                session.SessionName,
                session.CurrentPhase
            }, _jsonOptions);
            LogOperationResult(operation, successResponse);
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed", operation);
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    #endregion

    #region Internal Helpers

    private void LogOperationStart(string operation, object? payload)
    {
        var serialized = SerializeForLog(payload);
        Debug.WriteLine($"[GameSetupPhasePlugin] {operation} input: {serialized}");
        _logger.LogDebug("{Operation} input: {Payload}", operation, serialized);
    }

    private void LogOperationResult(string operation, string response)
    {
        Debug.WriteLine($"[GameSetupPhasePlugin] {operation} output: {response}");
        _logger.LogDebug("{Operation} output: {Payload}", operation, response);
    }

    private string SerializeForLog(object? payload)
    {
        if (payload is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(payload, _jsonOptions);
        }
        catch
        {
            return payload.ToString() ?? "<unserializable>";
        }
    }

    private void ApplySessionDisplayName(AdventureSessionState session)
    {
        session.SessionName = _gameStateRepo.GenerateSessionDisplayName(session);
    }

    private Task SaveSessionAsync(AdventureSessionState session)
    {
        ApplySessionDisplayName(session);
        return _gameStateRepo.SaveStateAsync(session);
    }

    private async Task<AdventureModule> LoadModuleAsync()
    {
        var session = await _gameStateRepo.LoadLatestStateAsync();
        var moduleFileName = !string.IsNullOrWhiteSpace(session.Module.ModuleFileName)
            ? session.Module.ModuleFileName
            : session.Module.ModuleId;

        _logger.LogDebug("Loading module from file {ModuleFileName}", moduleFileName);
        var module = await _moduleRepository.LoadByFileNameAsync(moduleFileName);
        _logger.LogDebug("Loaded module {ModuleId}", module.Metadata.ModuleId);
        return module;
    }

    private async Task SaveModuleAsync(AdventureModule module, AdventureSessionState? sessionOverride = null)
    {
        var session = sessionOverride ?? await _gameStateRepo.LoadLatestStateAsync();
        var moduleFileName = !string.IsNullOrWhiteSpace(session.Module.ModuleFileName)
            ? session.Module.ModuleFileName
            : module.Metadata.ModuleId;
        var modulePath = _moduleRepository.GetModuleFilePath(moduleFileName);

        _logger.LogDebug("Persisting module {ModuleId} to {ModulePath}", module.Metadata.ModuleId, modulePath);
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

        _logger.LogDebug("Module {ModuleId} persisted", module.Metadata.ModuleId);
    }

    private static List<string> NormalizeStringList(IEnumerable<string>? source)
    {
        return source?
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .ToList()
            ?? new List<string>();
    }

    private static Dictionary<int, List<string>> NormalizeLevelTable(Dictionary<int, List<string>>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new Dictionary<int, List<string>>();
        }

        var result = new Dictionary<int, List<string>>();
        foreach (var entry in source)
        {
            result[entry.Key] = NormalizeStringList(entry.Value);
        }

        return result;
    }

    private static List<string> ValidateCharacterClassStructure(AdventureModuleCharacterClass classData)
    {
        var issues = new List<string>();

        if (classData.StartingAbilities is null || classData.StartingAbilities.Count == 0)
        {
            issues.Add("At least one starting ability is required.");
        }

        if (classData.StartingPerks is null || classData.StartingPerks.Count == 0)
        {
            issues.Add("At least one starting perk is required.");
        }

        var invalidLevels = GetInvalidClassLevels(classData);
        if (invalidLevels.Count > 0)
        {
            issues.Add($"Level-up entries must fall between levels {MinClassLevel}-{MaxClassLevel}; invalid levels: {string.Join(", ", invalidLevels)}.");
        }

        return issues;
    }

    private static List<int> GetInvalidClassLevels(AdventureModuleCharacterClass classData)
    {
        var invalid = new HashSet<int>();

        static void CaptureInvalid(Dictionary<int, List<string>>? table, HashSet<int> accumulator)
        {
            if (table is null)
            {
                return;
            }

            foreach (var level in table.Keys)
            {
                if (level < MinClassLevel || level > MaxClassLevel)
                {
                    accumulator.Add(level);
                }
            }
        }

        CaptureInvalid(classData.LevelUpAbilities, invalid);
        CaptureInvalid(classData.LevelUpPerks, invalid);

        return invalid.OrderBy(level => level).ToList();
    }

    private static bool HasRewardAtLevel(AdventureModuleCharacterClass classData, int level)
    {
        return TableHasEntries(classData.LevelUpAbilities, level) || TableHasEntries(classData.LevelUpPerks, level);
    }

    private static bool TableHasEntries(Dictionary<int, List<string>>? table, int level)
    {
        if (table is null)
        {
            return false;
        }

        return table.TryGetValue(level, out var entries) && entries.Any();
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
            StartingPerks = classData.StartingPerks?.ToList() ?? new List<string>(),
            StartingItems = new List<string>(),
            Tags = classData.Tags?.ToList() ?? new List<string>(),
            LevelUpTable = ConvertLevelUpTable(classData.LevelUpAbilities),
            LevelUpPerks = ConvertLevelUpTable(classData.LevelUpPerks)
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

        [JsonPropertyName("startingPerks")]
        public List<string>? StartingPerks { get; set; }

        [JsonPropertyName("levelUpAbilities")]
        public Dictionary<int, List<string>>? LevelUpAbilities { get; set; }

        [JsonPropertyName("levelUpPerks")]
        public Dictionary<int, List<string>>? LevelUpPerks { get; set; }


        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    #endregion
}
