using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.Game.Plugins.Models;
using PokeLLM.GameState;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin that manages adventure module population during the world generation phase.
/// Focuses on manipulating the module/session data directly and enforcing structural integrity.
/// </summary>
public class WorldGenerationPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly IAdventureModuleRepository _moduleRepository;
    private readonly ILogger<WorldGenerationPhasePlugin> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly AdventureModuleValidator _validator;

    public WorldGenerationPhasePlugin(
        IGameStateRepository gameStateRepo,
        IAdventureModuleRepository moduleRepository,
        ILogger<WorldGenerationPhasePlugin> logger)
    {
        _gameStateRepo = gameStateRepo;
        _moduleRepository = moduleRepository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _validator = new AdventureModuleValidator();
    }

    #region Context Functions

    [KernelFunction("get_world_generation_context")]
    [Description("Fetch the current world-generation context including session state, module metadata, content counts, and outstanding validation issues.")]
    public async Task<string> GetWorldGenerationContext()
    {
        const string operation = nameof(GetWorldGenerationContext);
        LogOperationStart(operation, null);

        try
        {
            var (session, module) = await LoadSessionAndModuleAsync();
            var validation = _validator.Validate(module);

            var summary = new
            {
                session = new
                {
                    session.SessionId,
                    session.SessionName,
                    session.CurrentPhase,
                    session.Region,
                    session.CurrentContext,
                    session.Metadata.LastUpdatedTime,
                    session.Metadata.GameTurnNumber
                },
                module = new
                {
                    module.Metadata.ModuleId,
                    module.Metadata.Title,
                    module.Metadata.Summary,
                    module.Metadata.RecommendedLevelRange,
                    module.Metadata.Tags,
                    world = module.World,
                    counts = new
                    {
                        locations = module.Locations.Count,
                        npcs = module.Npcs.Count,
                        species = module.Bestiary.Count,
                        creatures = module.CreatureInstances.Count,
                        items = module.Items.Count,
                        factions = module.Factions.Count,
                        loreEntries = module.LoreEntries.Count,
                        quests = module.QuestLines.Count,
                        scriptedEvents = module.ScriptedEvents.Count,
                        scenarioScripts = module.ScenarioScripts.Count
                    }
                },
                validation = new
                {
                    validation.IsValid,
                    validation.Errors
                }
            };

            var response = JsonSerializer.Serialize(new { success = true, data = summary }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex, "GetWorldGenerationContext failed");
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    #endregion

    #region Module Mutation

    [KernelFunction("apply_world_generation_updates")]
    [Description("Apply a batch of world-building updates to the active adventure module. All referenced IDs must exist or be provided in the same batch.")]
    public async Task<string> ApplyWorldGenerationUpdates([Description("The batch of updates to merge into the module")] WorldGenerationUpdateBatch updates)
    {
        const string operation = nameof(ApplyWorldGenerationUpdates);
        LogOperationStart(operation, updates);

        try
        {
            if (updates is null)
            {
                var nullResponse = JsonSerializer.Serialize(new { success = false, error = "Update payload cannot be null." }, _jsonOptions);
                LogOperationResult(operation, nullResponse);
                return nullResponse;
            }

            if (!updates.HasContent())
            {
                var emptyResponse = JsonSerializer.Serialize(new { success = false, error = "At least one update or removal must be specified." }, _jsonOptions);
                LogOperationResult(operation, emptyResponse);
                return emptyResponse;
            }

            var (session, module) = await LoadSessionAndModuleAsync();
            var originalModuleJson = JsonSerializer.Serialize(module, _jsonOptions);

            _moduleRepository.ApplyChanges(module, m => ApplyModuleUpdates(m, updates));

            var validation = _validator.Validate(module);
            if (!validation.IsValid)
            {
                // Reload module from original snapshot to discard invalid mutations
                module = JsonSerializer.Deserialize<AdventureModule>(originalModuleJson, _jsonOptions)
                          ?? throw new InvalidOperationException("Failed to revert module after validation failure.");

                var invalidResponse = JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Module updates failed validation.",
                    validation = new
                    {
                        validation.IsValid,
                        validation.Errors
                    }
                }, _jsonOptions);
                LogOperationResult(operation, invalidResponse);
                return invalidResponse;
            }

            await SaveModuleAsync(module, session);

            if (updates.ReapplyBaseline)
            {
                _moduleRepository.ApplyModuleBaseline(module, session, preservePlayer: true);
                session.Region = string.IsNullOrWhiteSpace(module.World.Setting) ? session.Region : module.World.Setting;
                await SaveSessionAsync(session);
            }

            var response = JsonSerializer.Serialize(new
            {
                success = true,
                moduleId = module.Metadata.ModuleId,
                counts = new
                {
                    locations = module.Locations.Count,
                    npcs = module.Npcs.Count,
                    species = module.Bestiary.Count,
                    creatures = module.CreatureInstances.Count,
                    items = module.Items.Count,
                    factions = module.Factions.Count,
                    loreEntries = module.LoreEntries.Count,
                    quests = module.QuestLines.Count,
                    scriptedEvents = module.ScriptedEvents.Count,
                    scenarioScripts = module.ScenarioScripts.Count
                }
            }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex, "ApplyWorldGenerationUpdates failed");
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    private void ApplyModuleUpdates(AdventureModule module, WorldGenerationUpdateBatch updates)
    {
        NormalizeModule(module);

        if (updates.Metadata is not null)
        {
            ApplyMetadataUpdates(module.Metadata, updates.Metadata);
        }

        if (updates.World is not null)
        {
            ApplyWorldOverviewUpdates(module.World, updates.World);
        }

        ApplyDictionaryUpdates(module.Locations, updates.Locations, NormalizeLocation);
        ApplyDictionaryUpdates(module.Npcs, updates.Npcs, NormalizeNpc);
        ApplyDictionaryUpdates(module.Bestiary, updates.CreatureSpecies, NormalizeSpecies);
        ApplyDictionaryUpdates(module.CreatureInstances, updates.CreatureInstances, NormalizeCreatureInstance);
        ApplyDictionaryUpdates(module.Items, updates.Items, NormalizeItem);
        ApplyDictionaryUpdates(module.Factions, updates.Factions, NormalizeFaction);
        ApplyDictionaryUpdates(module.LoreEntries, updates.LoreEntries, NormalizeLoreEntry);
        ApplyDictionaryUpdates(module.ScriptedEvents, updates.ScriptedEvents, NormalizeScriptedEvent);
        ApplyDictionaryUpdates(module.QuestLines, updates.QuestLines, NormalizeQuestLine);
        ApplyDictionaryUpdates(module.Moves, updates.Moves, NormalizeMove);
        ApplyDictionaryUpdates(module.Abilities, updates.Abilities, NormalizeAbility);

        if (updates.ScenarioScripts is not null)
        {
            module.ScenarioScripts ??= new List<AdventureModuleScenarioScript>();
            foreach (var script in updates.ScenarioScripts)
            {
                if (string.IsNullOrWhiteSpace(script.ScriptId))
                {
                    continue;
                }

                var existingIndex = module.ScenarioScripts.FindIndex(s => string.Equals(s.ScriptId, script.ScriptId, StringComparison.OrdinalIgnoreCase));
                var normalized = NormalizeScenarioScript(script);
                if (existingIndex >= 0)
                {
                    module.ScenarioScripts[existingIndex] = normalized;
                }
                else
                {
                    module.ScenarioScripts.Add(normalized);
                }
            }
        }

        RemoveDictionaryEntries(module.Locations, updates.RemoveLocationIds);
        RemoveDictionaryEntries(module.Npcs, updates.RemoveNpcIds);
        RemoveDictionaryEntries(module.Bestiary, updates.RemoveSpeciesIds);
        RemoveDictionaryEntries(module.CreatureInstances, updates.RemoveCreatureInstanceIds);
        RemoveDictionaryEntries(module.Items, updates.RemoveItemIds);
        RemoveDictionaryEntries(module.Factions, updates.RemoveFactionIds);
        RemoveDictionaryEntries(module.LoreEntries, updates.RemoveLoreEntryIds);
        RemoveDictionaryEntries(module.ScriptedEvents, updates.RemoveScriptedEventIds);
        RemoveDictionaryEntries(module.QuestLines, updates.RemoveQuestIds);
        RemoveDictionaryEntries(module.Moves, updates.RemoveMoveIds);
        RemoveDictionaryEntries(module.Abilities, updates.RemoveAbilityIds);

        if (updates.RemoveScenarioScriptIds is not null && updates.RemoveScenarioScriptIds.Count > 0 && module.ScenarioScripts is not null)
        {
            var removals = updates.RemoveScenarioScriptIds
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            module.ScenarioScripts.RemoveAll(script => removals.Contains(script.ScriptId));
        }

        if (updates.MechanicalReferences is not null)
        {
            module.MechanicalReferences ??= new AdventureModuleMechanicalReferences();
            ApplyMechanicalReferenceUpdates(module.MechanicalReferences, updates.MechanicalReferences);
        }

        NormalizeModule(module);
    }

    #endregion

    #region Validation Helpers

    [KernelFunction("validate_module_integrity")]
    [Description("Run structural validation over the active adventure module. Returns all detected issues.")]
    public async Task<string> ValidateModuleIntegrity()
    {
        const string operation = nameof(ValidateModuleIntegrity);
        LogOperationStart(operation, null);

        try
        {
            var module = await LoadModuleAsync();
            var validation = _validator.Validate(module);

            var response = JsonSerializer.Serialize(new
            {
                success = validation.IsValid,
                validation = new
                {
                    validation.IsValid,
                    validation.Errors
                }
            }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex, "ValidateModuleIntegrity failed");
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    #endregion

    #region Finalization

    [KernelFunction("finalize_world_generation")]
    [Description("Finalize world generation after the module is fully populated and validated.")]
    public async Task<string> FinalizeWorldGeneration(
        [Description("Opening scenario narrative that should greet the player when exploration begins")] string openingScenario)
    {
        const string operation = nameof(FinalizeWorldGeneration);
        LogOperationStart(operation, new { openingScenario });

        try
        {
            if (string.IsNullOrWhiteSpace(openingScenario))
            {
                var nullResponse = JsonSerializer.Serialize(new { success = false, error = "Opening scenario is required." }, _jsonOptions);
                LogOperationResult(operation, nullResponse);
                return nullResponse;
            }

            var (session, module) = await LoadSessionAndModuleAsync();
            var validation = _validator.Validate(module);
            if (!validation.IsValid)
            {
                var invalidResponse = JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Module failed validation. Resolve all issues before finalizing.",
                    validation = new
                    {
                        validation.IsValid,
                        validation.Errors
                    }
                }, _jsonOptions);
                LogOperationResult(operation, invalidResponse);
                return invalidResponse;
            }

            module.Metadata.IsSetupComplete = true;
            module.Metadata.Generator ??= new AdventureModuleGeneratorMetadata();
            module.Metadata.Generator.SeedPrompt = "WorldGenerationPhase";
            module.Metadata.Generator.Notes = "World generation finalized via autonomous phase.";

            _moduleRepository.ApplyModuleBaseline(module, session, preservePlayer: true);
            session.Region = string.IsNullOrWhiteSpace(module.World.Setting) ? session.Region : module.World.Setting;
            session.Metadata.CurrentPhase = GamePhase.Exploration;
            session.Metadata.PhaseChangeSummary = "World generation completed successfully.";
            session.Metadata.CurrentContext = openingScenario.Trim();
            session.AdventureSummary = string.IsNullOrWhiteSpace(module.Metadata.Summary) ? session.AdventureSummary : module.Metadata.Summary;
            session.RecentEvents.Add(new EventLog
            {
                TurnNumber = session.GameTurnNumber,
                EventDescription = $"World Generation Completed: {openingScenario.Trim()}"
            });
            session.LastSaveTime = DateTime.UtcNow;

            await SaveModuleAsync(module, session);
            await SaveSessionAsync(session);

            var response = JsonSerializer.Serialize(new
            {
                success = true,
                message = "World generation finalized. Exploration phase unlocked.",
                nextPhase = session.CurrentPhase.ToString(),
                sessionId = session.SessionId,
                region = session.Region
            }, _jsonOptions);
            LogOperationResult(operation, response);
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex, "FinalizeWorldGeneration failed");
            var errorResponse = JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
            LogOperationResult(operation, errorResponse);
            return errorResponse;
        }
    }

    #endregion

    #region Persistence Helpers

    private async Task<(AdventureSessionState session, AdventureModule module)> LoadSessionAndModuleAsync()
    {
        var session = await _gameStateRepo.LoadLatestStateAsync();
        var module = await LoadModuleAsync(session);
        return (session, module);
    }

    private Task<AdventureModule> LoadModuleAsync()
        => LoadModuleAsync(null);

    private async Task<AdventureModule> LoadModuleAsync(AdventureSessionState? sessionOverride)
    {
        var session = sessionOverride ?? await _gameStateRepo.LoadLatestStateAsync();
        var moduleFileName = !string.IsNullOrWhiteSpace(session.Module.ModuleFileName)
            ? session.Module.ModuleFileName
            : session.Module.ModuleId;

        LogDebug($"Loading module from {moduleFileName}");
        var module = await _moduleRepository.LoadByFileNameAsync(moduleFileName);
        NormalizeModule(module);
        return module;
    }

    private async Task SaveModuleAsync(AdventureModule module, AdventureSessionState session)
    {
        var moduleFileName = !string.IsNullOrWhiteSpace(session.Module.ModuleFileName)
            ? session.Module.ModuleFileName
            : module.Metadata.ModuleId;
        var modulePath = _moduleRepository.GetModuleFilePath(moduleFileName);

        LogDebug($"Persisting module {module.Metadata.ModuleId} to {modulePath}");
        await _moduleRepository.SaveAsync(module, modulePath);

        var resolvedFileName = Path.GetFileName(modulePath);
        if (!string.Equals(session.Module.ModuleFileName, resolvedFileName, StringComparison.OrdinalIgnoreCase))
        {
            session.Module.ModuleFileName = resolvedFileName;
            await SaveSessionAsync(session);
        }
    }

    private async Task SaveSessionAsync(AdventureSessionState session)
    {
        session.SessionName = _gameStateRepo.GenerateSessionDisplayName(session);
        session.Metadata.LastUpdatedTime = DateTime.UtcNow;
        await _gameStateRepo.SaveStateAsync(session);
    }

    #endregion

    #region Normalization Helpers

    private static void NormalizeModule(AdventureModule module)
    {
        module.Locations ??= new Dictionary<string, AdventureModuleLocation>(StringComparer.OrdinalIgnoreCase);
        module.Npcs ??= new Dictionary<string, AdventureModuleNpc>(StringComparer.OrdinalIgnoreCase);
        module.Bestiary ??= new Dictionary<string, AdventureModuleCreatureSpecies>(StringComparer.OrdinalIgnoreCase);
        module.CreatureInstances ??= new Dictionary<string, AdventureModuleCreatureInstance>(StringComparer.OrdinalIgnoreCase);
        module.Items ??= new Dictionary<string, AdventureModuleItem>(StringComparer.OrdinalIgnoreCase);
        module.Factions ??= new Dictionary<string, AdventureModuleFaction>(StringComparer.OrdinalIgnoreCase);
        module.LoreEntries ??= new Dictionary<string, AdventureModuleLoreEntry>(StringComparer.OrdinalIgnoreCase);
        module.ScriptedEvents ??= new Dictionary<string, AdventureModuleScriptedEvent>(StringComparer.OrdinalIgnoreCase);
        module.QuestLines ??= new Dictionary<string, AdventureModuleQuestLine>(StringComparer.OrdinalIgnoreCase);
        module.Moves ??= new Dictionary<string, AdventureModuleMove>(StringComparer.OrdinalIgnoreCase);
        module.Abilities ??= new Dictionary<string, AdventureModuleAbility>(StringComparer.OrdinalIgnoreCase);
        module.ScenarioScripts ??= new List<AdventureModuleScenarioScript>();
        module.Metadata ??= new AdventureModuleMetadata();
        module.World ??= new AdventureModuleWorldOverview();
        module.MechanicalReferences ??= module.MechanicalReferences ?? new AdventureModuleMechanicalReferences();
    }

    private void ApplyMetadataUpdates(AdventureModuleMetadata target, ModuleMetadataUpdateDto source)
    {
        if (!string.IsNullOrWhiteSpace(source.Title))
        {
            target.Title = source.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.Summary))
        {
            target.Summary = source.Summary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.Version))
        {
            target.Version = source.Version.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.RecommendedLevelRange))
        {
            target.RecommendedLevelRange = source.RecommendedLevelRange.Trim();
        }

        if (source.Tags is not null)
        {
            target.Tags = source.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private void ApplyWorldOverviewUpdates(AdventureModuleWorldOverview target, WorldOverviewUpdateDto source)
    {
        if (!string.IsNullOrWhiteSpace(source.Setting))
        {
            target.Setting = source.Setting.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.Tone))
        {
            target.Tone = source.Tone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.StartingContext))
        {
            target.StartingContext = source.StartingContext.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.TimePeriod))
        {
            target.TimePeriod = source.TimePeriod.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.MaturityRating))
        {
            target.MaturityRating = source.MaturityRating.Trim();
        }

        if (source.Themes is not null)
        {
            target.Themes = source.Themes
                .Where(theme => !string.IsNullOrWhiteSpace(theme))
                .Select(theme => theme.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (source.Hooks is not null)
        {
            target.AdventureHooks = source.Hooks
                .Where(hook => !string.IsNullOrWhiteSpace(hook))
                .Select(hook => hook.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (source.SafetyConsiderations is not null)
        {
            target.SafetyConsiderations = source.SafetyConsiderations
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private void ApplyMechanicalReferenceUpdates(AdventureModuleMechanicalReferences target, MechanicalReferencesUpdateDto source)
    {
        if (source.EncounterTables is not null)
        {
            target.EncounterTables ??= new List<AdventureModuleEncounterTable>();
            foreach (var table in source.EncounterTables)
            {
                if (string.IsNullOrWhiteSpace(table.TableId))
                {
                    continue;
                }

                var normalized = NormalizeEncounterTable(table);
                var index = target.EncounterTables.FindIndex(t => string.Equals(t.TableId, normalized.TableId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    target.EncounterTables[index] = normalized;
                }
                else
                {
                    target.EncounterTables.Add(normalized);
                }
            }
        }

        if (source.WeatherProfiles is not null)
        {
            target.WeatherProfiles ??= new List<AdventureModuleWeatherProfile>();
            foreach (var profile in source.WeatherProfiles)
            {
                if (string.IsNullOrWhiteSpace(profile.ProfileId))
                {
                    continue;
                }

                var normalized = NormalizeWeatherProfile(profile);
                var index = target.WeatherProfiles.FindIndex(p => string.Equals(p.ProfileId, normalized.ProfileId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    target.WeatherProfiles[index] = normalized;
                }
                else
                {
                    target.WeatherProfiles.Add(normalized);
                }
            }
        }

        if (source.TravelRules is not null)
        {
            target.TravelRules ??= new List<AdventureModuleTravelRule>();
            foreach (var rule in source.TravelRules)
            {
                if (string.IsNullOrWhiteSpace(rule.RuleId))
                {
                    continue;
                }

                var normalized = NormalizeTravelRule(rule);
                var index = target.TravelRules.FindIndex(r => string.Equals(r.RuleId, normalized.RuleId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    target.TravelRules[index] = normalized;
                }
                else
                {
                    target.TravelRules.Add(normalized);
                }
            }
        }
    }

    private static void ApplyDictionaryUpdates<T>(Dictionary<string, T> target, Dictionary<string, T>? updates, Func<T, T> normalizer)
    {
        if (updates is null || updates.Count == 0)
        {
            return;
        }

        foreach (var (key, value) in updates)
        {
            var normalizedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            if (value is null)
            {
                continue;
            }

            var normalizedValue = normalizer(value);
            target[normalizedKey] = normalizedValue;
        }
    }

    private static void RemoveDictionaryEntries<T>(Dictionary<string, T> target, IReadOnlyCollection<string>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return;
        }

        foreach (var id in ids)
        {
            var normalizedId = id?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                continue;
            }

            target.Remove(normalizedId);
        }
    }

    private static AdventureModuleLocation NormalizeLocation(AdventureModuleLocation source)
    {
        source.LocationId = source.LocationId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Summary ??= string.Empty;
        source.FullDescription ??= string.Empty;
        source.Region ??= string.Empty;
        source.Tags ??= new List<string>();
        source.FactionsPresent ??= new List<string>();
        source.PointsOfInterest ??= new List<AdventureModulePointOfInterest>();
        foreach (var poi in source.PointsOfInterest)
        {
            poi.Id = poi.Id?.Trim() ?? string.Empty;
            poi.Name = poi.Name?.Trim() ?? string.Empty;
            poi.Description ??= string.Empty;
            poi.RelatedNpcIds ??= new List<string>();
            poi.RelatedItemIds ??= new List<string>();
        }

        source.Encounters ??= new List<AdventureModuleEncounter>();
        foreach (var encounter in source.Encounters)
        {
            encounter.EncounterId = encounter.EncounterId?.Trim() ?? string.Empty;
            encounter.Type = encounter.Type?.Trim() ?? string.Empty;
            encounter.Trigger = encounter.Trigger?.Trim() ?? string.Empty;
            encounter.Difficulty = encounter.Difficulty?.Trim() ?? string.Empty;
            encounter.Narrative ??= string.Empty;
            encounter.Participants ??= new List<string>();
            encounter.Outcomes ??= new List<AdventureModuleOutcome>();
            foreach (var outcome in encounter.Outcomes)
            {
                outcome.Description ??= string.Empty;
                outcome.ResultingChanges ??= new List<AdventureModuleStateChange>();
                foreach (var change in outcome.ResultingChanges)
                {
                    change.ChangeType = change.ChangeType?.Trim() ?? string.Empty;
                    change.TargetType = change.TargetType?.Trim() ?? string.Empty;
                    change.TargetId = change.TargetId?.Trim() ?? string.Empty;
                    change.Payload ??= string.Empty;
                }
            }
        }

        source.ConnectedLocations ??= new List<AdventureModuleLocationConnection>();
        foreach (var connection in source.ConnectedLocations)
        {
            connection.Direction = connection.Direction?.Trim() ?? string.Empty;
            connection.TargetLocationId = connection.TargetLocationId?.Trim() ?? string.Empty;
            connection.Notes ??= string.Empty;
        }

        return source;
    }

    private static AdventureModuleNpc NormalizeNpc(AdventureModuleNpc source)
    {
        source.NpcId = source.NpcId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Role ??= string.Empty;
        source.Motivation ??= string.Empty;
        source.FullDescription ??= string.Empty;
        source.Factions ??= new List<string>();
        source.Relationships ??= new List<AdventureModuleRelationship>();
        foreach (var relationship in source.Relationships)
        {
            relationship.TargetId = relationship.TargetId?.Trim() ?? string.Empty;
            relationship.Type = relationship.Type?.Trim() ?? string.Empty;
            relationship.Summary ??= string.Empty;
        }

        source.DialogueScripts ??= new List<AdventureModuleDialogueScript>();
        foreach (var script in source.DialogueScripts)
        {
            script.ScriptId = script.ScriptId?.Trim() ?? string.Empty;
            script.Context ??= string.Empty;
            script.Lines ??= new List<AdventureModuleDialogueLine>();
            foreach (var line in script.Lines)
            {
                line.Speaker = line.Speaker?.Trim() ?? string.Empty;
                line.Content ??= string.Empty;
                line.Notes ??= string.Empty;
            }
        }

        source.Stats ??= source.Stats ?? new Stats();
        source.CharacterDetails ??= source.CharacterDetails ?? new CharacterDetails();
        return source;
    }

    private static AdventureModuleCreatureSpecies NormalizeSpecies(AdventureModuleCreatureSpecies source)
    {
        source.Description ??= string.Empty;
        source.Habitats ??= new List<string>();
        source.DefaultMoves ??= new List<string>();
        source.BaseStats ??= source.BaseStats ?? new Stats();
        source.LevelUpMoves ??= new Dictionary<int, List<string>>();
        foreach (var level in source.LevelUpMoves.Keys.ToList())
        {
            source.LevelUpMoves[level] ??= new List<string>();
        }

        source.EvolutionConditions ??= new List<string>();
        source.BehaviorNotes ??= string.Empty;
        source.AbilityIds ??= new List<string>();
        return source;
    }

    private static AdventureModuleCreatureInstance NormalizeCreatureInstance(AdventureModuleCreatureInstance source)
    {
        source.SpeciesId = source.SpeciesId?.Trim() ?? string.Empty;
        source.Nickname = source.Nickname?.Trim() ?? string.Empty;
        source.HeldItem = source.HeldItem?.Trim() ?? string.Empty;
        source.Moves ??= new List<string>();
        source.LocationId = source.LocationId?.Trim() ?? string.Empty;
        source.OwnerNpcId = source.OwnerNpcId?.Trim() ?? string.Empty;
        source.FactionIds ??= new List<string>();
        source.Tags ??= new List<string>();
        source.FullDescription ??= string.Empty;
        source.Notes ??= string.Empty;
        return source;
    }

    private static AdventureModuleItem NormalizeItem(AdventureModuleItem source)
    {
        source.ItemId = source.ItemId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Rarity ??= string.Empty;
        source.FullDescription ??= string.Empty;
        source.Effects ??= string.Empty;
        source.Placement ??= new List<AdventureModuleItemPlacement>();
        foreach (var placement in source.Placement)
        {
            placement.LocationId = placement.LocationId?.Trim() ?? string.Empty;
            placement.NpcId = placement.NpcId?.Trim() ?? string.Empty;
            placement.Notes ??= string.Empty;
        }

        return source;
    }

    private static AdventureModuleFaction NormalizeFaction(AdventureModuleFaction source)
    {
        source.FactionId = source.FactionId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Ideology ??= string.Empty;
        source.Leaders ??= new List<string>();
        source.FullDescription ??= string.Empty;
        source.Relationships ??= new List<AdventureModuleRelationship>();
        foreach (var relationship in source.Relationships)
        {
            relationship.TargetId = relationship.TargetId?.Trim() ?? string.Empty;
            relationship.Type = relationship.Type?.Trim() ?? string.Empty;
            relationship.Summary ??= string.Empty;
        }

        return source;
    }

    private static AdventureModuleLoreEntry NormalizeLoreEntry(AdventureModuleLoreEntry source)
    {
        source.EntryId = source.EntryId?.Trim() ?? string.Empty;
        source.Category = source.Category?.Trim() ?? string.Empty;
        source.Title = source.Title?.Trim() ?? string.Empty;
        source.FullText ??= string.Empty;
        source.Tags ??= new List<string>();
        return source;
    }

    private static AdventureModuleScriptedEvent NormalizeScriptedEvent(AdventureModuleScriptedEvent source)
    {
        source.EventId = source.EventId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.TriggerConditions ??= new List<string>();
        source.Scenes ??= new List<AdventureModuleScene>();
        foreach (var scene in source.Scenes)
        {
            scene.SceneId = scene.SceneId?.Trim() ?? string.Empty;
            scene.Description ??= string.Empty;
            scene.DialogueScripts ??= new List<AdventureModuleDialogueScript>();
            foreach (var script in scene.DialogueScripts)
            {
                script.ScriptId = script.ScriptId?.Trim() ?? string.Empty;
                script.Context ??= string.Empty;
                script.Lines ??= new List<AdventureModuleDialogueLine>();
                foreach (var line in script.Lines)
                {
                    line.Speaker = line.Speaker?.Trim() ?? string.Empty;
                    line.Content ??= string.Empty;
                    line.Notes ??= string.Empty;
                }
            }
        }

        source.Outcomes ??= new List<AdventureModuleOutcome>();
        foreach (var outcome in source.Outcomes)
        {
            outcome.Description ??= string.Empty;
            outcome.ResultingChanges ??= new List<AdventureModuleStateChange>();
            foreach (var change in outcome.ResultingChanges)
            {
                change.ChangeType = change.ChangeType?.Trim() ?? string.Empty;
                change.TargetType = change.TargetType?.Trim() ?? string.Empty;
                change.TargetId = change.TargetId?.Trim() ?? string.Empty;
                change.Payload ??= string.Empty;
            }
        }

        return source;
    }

    private static AdventureModuleQuestLine NormalizeQuestLine(AdventureModuleQuestLine source)
    {
        source.QuestId = source.QuestId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Summary ??= string.Empty;
        source.Stages ??= new List<AdventureModuleQuestStage>();
        foreach (var stage in source.Stages)
        {
            stage.StageId = stage.StageId?.Trim() ?? string.Empty;
            stage.Objective ??= string.Empty;
            stage.Description ??= string.Empty;
            stage.RecommendedNpcIds ??= new List<string>();
            stage.RecommendedLocationIds ??= new List<string>();
            stage.Rewards ??= new List<string>();
        }

        return source;
    }

    private static AdventureModuleMove NormalizeMove(AdventureModuleMove source)
    {
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Type = source.Type?.Trim() ?? string.Empty;
        source.Category = source.Category?.Trim() ?? string.Empty;
        source.DamageDice = source.DamageDice?.Trim() ?? string.Empty;
        source.Description ??= string.Empty;
        return source;
    }

    private static AdventureModuleAbility NormalizeAbility(AdventureModuleAbility source)
    {
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.Description ??= string.Empty;
        source.Effects ??= string.Empty;
        return source;
    }

    private static AdventureModuleScenarioScript NormalizeScenarioScript(AdventureModuleScenarioScript source)
    {
        source.ScriptId = source.ScriptId?.Trim() ?? string.Empty;
        source.Title = source.Title?.Trim() ?? string.Empty;
        source.Scope = source.Scope?.Trim() ?? string.Empty;
        source.Goals ??= new List<string>();
        source.Summary ??= string.Empty;
        source.LinkedQuestIds ??= new List<string>();
        return source;
    }

    private static AdventureModuleEncounterTable NormalizeEncounterTable(AdventureModuleEncounterTable source)
    {
        source.TableId = source.TableId?.Trim() ?? string.Empty;
        source.Name = source.Name?.Trim() ?? string.Empty;
        source.LocationId = source.LocationId?.Trim() ?? string.Empty;
        source.Entries ??= new List<AdventureModuleEncounterTableEntry>();
        foreach (var entry in source.Entries)
        {
            entry.RollRange = entry.RollRange?.Trim() ?? string.Empty;
            entry.CreatureId = entry.CreatureId?.Trim() ?? string.Empty;
            entry.Description ??= string.Empty;
        }

        return source;
    }

    private static AdventureModuleWeatherProfile NormalizeWeatherProfile(AdventureModuleWeatherProfile source)
    {
        source.ProfileId = source.ProfileId?.Trim() ?? string.Empty;
        source.Description ??= string.Empty;
        source.Modifiers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return source;
    }

    private static AdventureModuleTravelRule NormalizeTravelRule(AdventureModuleTravelRule source)
    {
        source.RuleId = source.RuleId?.Trim() ?? string.Empty;
        source.Description ??= string.Empty;
        source.Effects ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return source;
    }

    #endregion

    private void LogDebug(string message)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] {message}");
        _logger.LogDebug(message);
    }

    private void LogError(Exception exception, string message)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin][Error] {message} :: {exception.Message}");
        _logger.LogError(exception, message);
    }

    private void LogOperationStart(string operation, object? payload)
    {
        var serialized = SerializeForLog(payload);
        Debug.WriteLine($"[WorldGenerationPhasePlugin] {operation} input: {serialized}");
        _logger.LogDebug("{Operation} input: {Payload}", operation, serialized);
    }

    private void LogOperationResult(string operation, string response)
    {
        Debug.WriteLine($"[WorldGenerationPhasePlugin] {operation} output: {response}");
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
}
