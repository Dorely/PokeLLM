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

    #region Module Abilities

    [KernelFunction("list_module_abilities")]
    [Description("List the abilities currently available in the module catalog.")]
    public async Task<string> ListModuleAbilities()
    {
        const string operation = nameof(ListModuleAbilities);
        LogOperationStart(operation, null);

        try
        {
            var module = await LoadModuleAsync();
            module.Abilities ??= new Dictionary<string, AdventureModuleAbility>(StringComparer.OrdinalIgnoreCase);

            var abilities = module.Abilities.Select(pair => new
            {
                id = pair.Key,
                pair.Value.Name,
                pair.Value.Description,
                pair.Value.Effects
            }).ToList();

            _logger.LogDebug("{Operation} returning {Count} abilities", operation, abilities.Count);
            var response = JsonSerializer.Serialize(new { success = true, abilities }, _jsonOptions);
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

    [KernelFunction("upsert_module_ability")]
    [Description("Create or update an ability definition in the module catalog.")]
    public async Task<string> UpsertModuleAbility([Description("Ability definition to create or update")] AbilityDefinition definition)
    {
        const string operation = nameof(UpsertModuleAbility);
        LogOperationStart(operation, definition);

        try
        {
            if (definition is null || string.IsNullOrWhiteSpace(definition.Id))
            {
                _logger.LogWarning("{Operation} missing ability id", operation);
                var missingResponse = JsonSerializer.Serialize(new { success = false, error = "Ability id is required" }, _jsonOptions);
                LogOperationResult(operation, missingResponse);
                return missingResponse;
            }

            var module = await LoadModuleAsync();
            module.Abilities ??= new Dictionary<string, AdventureModuleAbility>(StringComparer.OrdinalIgnoreCase);

            var abilityId = definition.Id.Trim();
            var isNew = !module.Abilities.TryGetValue(abilityId, out var abilityData);
            if (abilityData is null)
            {
                abilityData = new AdventureModuleAbility();
                module.Abilities[abilityId] = abilityData;
            }

            if (definition.Name is not null)
            {
                abilityData.Name = definition.Name.Trim();
            }

            if (definition.Description is not null)
            {
                abilityData.Description = definition.Description.Trim();
            }

            if (definition.Effects is not null)
            {
                abilityData.Effects = definition.Effects.Trim();
            }

            if (string.IsNullOrWhiteSpace(abilityData.Name))
            {
                abilityData.Name = abilityId;
            }

            abilityData.Description ??= string.Empty;
            abilityData.Effects ??= string.Empty;

            await SaveModuleAsync(module);

            _logger.LogDebug("{Operation} {Result} ability {AbilityId}", operation, isNew ? "created" : "updated", abilityId);
            var response = JsonSerializer.Serialize(new
            {
                success = true,
                abilityId,
                createdNew = isNew
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

    [KernelFunction("remove_module_ability")]
    [Description("Remove an ability from the module catalog.")]
    public async Task<string> RemoveModuleAbility([Description("Ability id to remove")] string abilityId)
    {
        const string operation = nameof(RemoveModuleAbility);
        LogOperationStart(operation, new { abilityId });

        try
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                _logger.LogWarning("{Operation} missing ability id", operation);
                var missingResponse = JsonSerializer.Serialize(new { success = false, error = "Ability id cannot be empty" }, _jsonOptions);
                LogOperationResult(operation, missingResponse);
                return missingResponse;
            }

            var module = await LoadModuleAsync();
            module.Abilities ??= new Dictionary<string, AdventureModuleAbility>(StringComparer.OrdinalIgnoreCase);

            var trimmedId = abilityId.Trim();
            var removed = module.Abilities.Remove(trimmedId);
            if (removed)
            {
                await SaveModuleAsync(module);
                _logger.LogDebug("{Operation} removed ability {AbilityId}", operation, trimmedId);
            }
            else
            {
                _logger.LogDebug("{Operation} found no ability {AbilityId} to remove", operation, trimmedId);
            }

            var response = JsonSerializer.Serialize(new
            {
                success = removed,
                abilityId = trimmedId,
                catalogCount = module.Abilities.Count
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

    [KernelFunction("upsert_character_class")]
    [Description("Create or update a character class definition in the module. Supply levelUpChart entries keyed by 1-20 with `abilities` and `passiveAbilities` lists (empty lists if nothing new unlocks). Only supplied fields are modified.")]
    public async Task<string> UpsertCharacterClass([Description("Partial or complete class details")] CharacterClassDefinition definition)
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
            module.CharacterClasses ??= new Dictionary<string, AdventureModuleCharacterClass>(StringComparer.OrdinalIgnoreCase);

            var isNew = !module.CharacterClasses.TryGetValue(classId, out var classData);
            if (classData is null)
            {
                classData = new AdventureModuleCharacterClass
                {
                    Name = classId,
                    Description = string.Empty,
                    StatModifiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    StartingAbilities = new List<string>(),
                    StartingPassiveAbilities = new List<string>(),
                    LevelUpChart = new Dictionary<int, AdventureModuleClassLevelProgression>(),
                    Tags = new List<string>()
                };
                module.CharacterClasses[classId] = classData;
            }

            if (definition.Name is not null)
            {
                classData.Name = string.IsNullOrWhiteSpace(definition.Name)
                    ? classId
                    : definition.Name.Trim();
            }
            else if (isNew && string.IsNullOrWhiteSpace(classData.Name))
            {
                classData.Name = classId;
            }

            if (definition.Description is not null)
            {
                classData.Description = definition.Description.Trim();
            }

            if (definition.StatModifiers is not null)
            {
                classData.StatModifiers = definition.StatModifiers
                    .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .ToDictionary(
                        pair => pair.Key.Trim(),
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase);
            }

            if (definition.StartingAbilities is not null)
            {
                classData.StartingAbilities = NormalizeIdentifierList(definition.StartingAbilities);
            }

            if (definition.StartingPassiveAbilities is not null)
            {
                classData.StartingPassiveAbilities = NormalizeIdentifierList(definition.StartingPassiveAbilities);
            }

            if (definition.LevelUpChart is not null)
            {
                ApplyLevelUpChartUpdates(classData, definition.LevelUpChart);
            }

            if (definition.Tags is not null)
            {
                classData.Tags = NormalizeIdentifierList(definition.Tags);
            }

            var validationErrors = ValidateCharacterClassStructure(module, classId, classData);

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
            module.CharacterClasses ??= new Dictionary<string, AdventureModuleCharacterClass>(StringComparer.OrdinalIgnoreCase);

            var classes = module.CharacterClasses.Select(pair => new
            {
                id = pair.Key,
                pair.Value.Name,
                pair.Value.Description,
                statModifiers = pair.Value.StatModifiers,
                startingAbilities = pair.Value.StartingAbilities,
                startingPassiveAbilities = pair.Value.StartingPassiveAbilities,
                levelUpChart = pair.Value.LevelUpChart,
                tags = pair.Value.Tags,
                validationIssues = ValidateCharacterClassStructure(module, pair.Key, pair.Value)
            }).ToList();

            _logger.LogDebug("{Operation} returning {Count} classes", operation, classes.Count);
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
            module.CharacterClasses ??= new Dictionary<string, AdventureModuleCharacterClass>(StringComparer.OrdinalIgnoreCase);

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
            session.Player.PassiveAbilities = classData.StartingPassiveAbilities?.ToList() ?? new List<string>();

            await _characterManagementService.SetPlayerClass(trimmedId);
            await SaveSessionAsync(session);

            _logger.LogDebug("{Operation} assigned class {ClassId} to player", operation, trimmedId);

            var response = JsonSerializer.Serialize(new
            {
                success = true,
                classId = trimmedId,
                className = classData.Name,
                startingAbilities = session.Player.Abilities,
                startingPassiveAbilities = session.Player.PassiveAbilities
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

            module.CharacterClasses ??= new Dictionary<string, AdventureModuleCharacterClass>(StringComparer.OrdinalIgnoreCase);

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
                .Select(pair => new { pair.Key, Issues = ValidateCharacterClassStructure(module, pair.Key, pair.Value) })
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
                    error = "Class definitions must include complete starting abilities, passive abilities, and level 1-20 progression before completing setup.",
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

    private static List<string> NormalizeIdentifierList(IEnumerable<string>? source)
    {
        return source?
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static void ApplyLevelUpChartUpdates(
        AdventureModuleCharacterClass classData,
        Dictionary<int, CharacterClassLevelEntry> updates)
    {
        if (updates is null || updates.Count == 0)
        {
            return;
        }

        classData.LevelUpChart ??= new Dictionary<int, AdventureModuleClassLevelProgression>();
        foreach (var (level, entry) in updates)
        {
            if (entry is null)
            {
                classData.LevelUpChart.Remove(level);
                continue;
            }

            if (!classData.LevelUpChart.TryGetValue(level, out var progression) || progression is null)
            {
                progression = new AdventureModuleClassLevelProgression();
                classData.LevelUpChart[level] = progression;
            }

            if (entry.Abilities is not null)
            {
                progression.Abilities = NormalizeIdentifierList(entry.Abilities);
            }

            if (entry.PassiveAbilities is not null)
            {
                progression.PassiveAbilities = NormalizeIdentifierList(entry.PassiveAbilities);
            }
        }
    }

    private static List<string> ValidateCharacterClassStructure(
        AdventureModule module,
        string classId,
        AdventureModuleCharacterClass classData)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(classData.Name))
        {
            issues.Add($"Class '{classId}' requires a name.");
        }

        if (string.IsNullOrWhiteSpace(classData.Description))
        {
            issues.Add($"Class '{classId}' requires a description.");
        }

        if (classData.StartingAbilities is null || classData.StartingAbilities.Count == 0)
        {
            issues.Add($"Class '{classId}' must include at least one starting ability.");
        }

        if (classData.StartingPassiveAbilities is null || classData.StartingPassiveAbilities.Count == 0)
        {
            issues.Add($"Class '{classId}' must include at least one starting passive ability.");
        }

        var invalidLevels = classData.LevelUpChart?
            .Where(entry => entry.Key < MinClassLevel || entry.Key > MaxClassLevel)
            .Select(entry => entry.Key)
            .OrderBy(level => level)
            .ToList() ?? new List<int>();

        if (invalidLevels.Count > 0)
        {
            issues.Add($"Class '{classId}' level-up entries must fall between levels {MinClassLevel}-{MaxClassLevel}; invalid levels: {string.Join(", ", invalidLevels)}.");
        }

        var missingLevels = Enumerable.Range(MinClassLevel, MaxClassLevel - MinClassLevel + 1)
            .Where(level => !HasRewardAtLevel(classData, level))
            .ToList();

        if (missingLevels.Count > 0)
        {
            issues.Add($"Class '{classId}' must define at least one ability or passive ability choice for every level. Missing levels: {string.Join(", ", missingLevels)}.");
        }

        var knownAbilityIds = module.Abilities != null
            ? new HashSet<string>(module.Abilities.Keys.Select(key => key.Trim()), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var invalidAbilityRefs = new List<string>();

        void ValidateAbilities(IEnumerable<string>? ids, string context)
        {
            if (ids is null)
            {
                return;
            }

            foreach (var raw in ids)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var abilityId = raw.Trim();
                if (!knownAbilityIds.Contains(abilityId))
                {
                    invalidAbilityRefs.Add($"{abilityId} ({context})");
                }
            }
        }

        ValidateAbilities(classData.StartingAbilities, "startingAbilities");
        ValidateAbilities(classData.StartingPassiveAbilities, "startingPassiveAbilities");

        if (classData.LevelUpChart is not null)
        {
            foreach (var (level, progression) in classData.LevelUpChart)
            {
                ValidateAbilities(progression?.Abilities, $"level {level} abilities");
                ValidateAbilities(progression?.PassiveAbilities, $"level {level} passiveAbilities");
            }
        }

        if (invalidAbilityRefs.Count > 0)
        {
            issues.Add($"Class '{classId}' references unknown ability ids: {string.Join(", ", invalidAbilityRefs)}.");
        }

        return issues;
    }

    private static bool HasRewardAtLevel(AdventureModuleCharacterClass classData, int level)
    {
        if (classData.LevelUpChart is null)
        {
            return false;
        }

        if (!classData.LevelUpChart.TryGetValue(level, out var progression) || progression is null)
        {
            return false;
        }

        var hasAbilities = progression.Abilities is { Count: > 0 } && progression.Abilities.Any(id => !string.IsNullOrWhiteSpace(id));
        var hasPassives = progression.PassiveAbilities is { Count: > 0 } && progression.PassiveAbilities.Any(id => !string.IsNullOrWhiteSpace(id));

        return hasAbilities || hasPassives;
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
            StartingPassiveAbilities = classData.StartingPassiveAbilities?.ToList() ?? new List<string>(),
            StartingItems = new List<string>(),
            Tags = classData.Tags?.ToList() ?? new List<string>(),
            LevelUpChoices = ConvertLevelUpChart(classData.LevelUpChart)
        };
    }

    private static Dictionary<int, TrainerClassLevelChoices> ConvertLevelUpChart(
        Dictionary<int, AdventureModuleClassLevelProgression>? source)
    {
        var result = new Dictionary<int, TrainerClassLevelChoices>();
        if (source is null)
        {
            return result;
        }

        foreach (var (level, progression) in source)
        {
            var abilities = progression?.Abilities?.Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .ToList() ?? new List<string>();
            var passiveAbilities = progression?.PassiveAbilities?.Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .ToList() ?? new List<string>();

            result[level] = new TrainerClassLevelChoices
            {
                Abilities = abilities,
                PassiveAbilities = passiveAbilities
            };
        }

        return result;
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

    public class AbilityDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("effects")]
        public string? Effects { get; set; }
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

        [JsonPropertyName("startingPassiveAbilities")]
        public List<string>? StartingPassiveAbilities { get; set; }

        [JsonPropertyName("levelUpChart")]
        public Dictionary<int, CharacterClassLevelEntry>? LevelUpChart { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    public class CharacterClassLevelEntry
    {
        [JsonPropertyName("abilities")]
        [Description("Ability ids granted at this level. Reference ids created via the ability catalog functions.")]
        public List<string>? Abilities { get; set; }

        [JsonPropertyName("passiveAbilities")]
        [Description("Passive ability ids granted at this level. Use empty lists when no new passives unlock.")]
        public List<string>? PassiveAbilities { get; set; }
    }

    #endregion
}
