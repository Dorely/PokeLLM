using System.Collections.Generic;
using System.Linq;
using PokeLLM.GameState.Models;

namespace PokeLLM.GameState;

public sealed class AdventureModuleValidator
{
    private static readonly HashSet<string> ReservedParticipantIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "player",
        "rival",
        "environment",
        "opponent",
        "gm",
        "party",
        "villain",
        "ally"
    };

    public AdventureModuleValidationResult Validate(AdventureModule? module)
    {
        var result = new AdventureModuleValidationResult();
        if (module is null)
        {
            result.Errors.Add("Module cannot be null.");
            return result;
        }

        var errors = result.Errors;

        if (module.Metadata is null)
        {
            errors.Add("Module metadata is missing.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(module.Metadata.ModuleId))
            {
                errors.Add("Module metadata must include a non-empty moduleId.");
            }

            if (string.IsNullOrWhiteSpace(module.Metadata.Title))
            {
                errors.Add("Module metadata must include a non-empty title.");
            }
        }

        var locationIds = CollectIds(module.Locations, errors, "locations", l => l.LocationId, "locationId");
        var npcIds = CollectIds(module.Npcs, errors, "npcs", n => n.NpcId, "npcId");
        var speciesIds = CollectIds(module.Bestiary, errors, "bestiary");
        var creatureInstanceIds = CollectIds(module.CreatureInstances, errors, "creatureInstances");
        var itemIds = CollectIds(module.Items, errors, "items", i => i.ItemId, "itemId");
        var factionIds = CollectIds(module.Factions, errors, "factions", f => f.FactionId, "factionId");
        var loreIds = CollectIds(module.LoreEntries, errors, "loreEntries", entry => entry.EntryId, "entryId");
        var eventIds = CollectIds(module.ScriptedEvents, errors, "scriptedEvents", e => e.EventId, "eventId");
        var questIds = CollectIds(module.QuestLines, errors, "questLines", q => q.QuestId, "questId");
        var moveIds = CollectIds(module.Moves, errors, "moves");
        var abilityIds = CollectIds(module.Abilities, errors, "abilities");
        var classIds = CollectIds(module.CharacterClasses, errors, "characterClasses");
        var scenarioScriptIds = CollectIds(module.ScenarioScripts, errors, "scenarioScripts", s => s.ScriptId, "scriptId");
        var questStageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var travelRuleIds = CollectTravelRuleIds(module);

        if (module.QuestLines is not null)
        {
            foreach (var quest in module.QuestLines.Values)
            {
                if (quest?.Stages is null)
                {
                    continue;
                }

                foreach (var stage in quest.Stages)
                {
                    if (!string.IsNullOrWhiteSpace(stage.StageId))
                    {
                        questStageIds.Add(stage.StageId.Trim());
                    }
                }
            }
        }

        ValidateLocations(module, locationIds, npcIds, itemIds, questIds, questStageIds, loreIds, travelRuleIds, speciesIds, creatureInstanceIds, factionIds, moveIds, abilityIds, errors);
        ValidateNpcs(module, npcIds, classIds, itemIds, factionIds, questIds, errors);
        ValidateCreatureSpecies(module, speciesIds, moveIds, abilityIds, errors);
        ValidateCreatureInstances(module, creatureInstanceIds, speciesIds, locationIds, npcIds, moveIds, factionIds, errors);
        ValidateItems(module, itemIds, locationIds, npcIds, errors);
        ValidateFactions(module, factionIds, npcIds, errors);
        ValidateQuestLines(module, questIds, questStageIds, locationIds, npcIds, errors);
        ValidateLoreEntries(module, errors);
        ValidateScriptedEvents(module, eventIds, npcIds, questIds, questStageIds, loreIds, locationIds, itemIds, factionIds, creatureInstanceIds, speciesIds, moveIds, abilityIds, travelRuleIds, errors);
        ValidateScenarioScripts(module, scenarioScriptIds, questIds, questStageIds, errors);
        ValidateMechanicalReferences(module, locationIds, speciesIds, creatureInstanceIds, travelRuleIds, errors);

        result.IsValid = errors.Count == 0;
        return result;
    }

    private static HashSet<string> CollectIds<T>(Dictionary<string, T>? dictionary, List<string> errors, string category, Func<T, string?>? idSelector = null, string? propertyName = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dictionary is null)
        {
            return set;
        }

        foreach (var (key, value) in dictionary)
        {
            var trimmedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedKey))
            {
                errors.Add($"{category} contains an entry with an empty key.");
                continue;
            }

            if (value is null)
            {
                errors.Add($"{category} entry '{trimmedKey}' has null data.");
                continue;
            }

            var id = idSelector?.Invoke(value)?.Trim();
            if (idSelector is not null)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"{category} entry '{trimmedKey}' must define {propertyName ?? "an id"}.");
                    continue;
                }

                if (!string.Equals(trimmedKey, id, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{category} entry key '{trimmedKey}' does not match {propertyName ?? "id"} '{id}'.");
                }

                if (!set.Add(id))
                {
                    errors.Add($"Duplicate {category} id detected: '{id}'.");
                }
            }
            else
            {
                if (!set.Add(trimmedKey))
                {
                    errors.Add($"Duplicate {category} id detected: '{trimmedKey}'.");
                }
            }
        }

        return set;
    }

    private static HashSet<string> CollectIds<T>(IEnumerable<T>? records, List<string> errors, string category, Func<T, string?> idSelector, string propertyName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (records is null)
        {
            return set;
        }

        var index = 0;
        foreach (var record in records)
        {
            var id = idSelector(record)?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{category}[{index}] must define {propertyName}.");
            }
            else if (!set.Add(id))
            {
                errors.Add($"Duplicate {category} id detected: '{id}'.");
            }

            index++;
        }

        return set;
    }

    private static void ValidateLocations(
        AdventureModule module,
        HashSet<string> locationIds,
        HashSet<string> npcIds,
        HashSet<string> itemIds,
        HashSet<string> questIds,
        HashSet<string> questStageIds,
        HashSet<string> loreIds,
        HashSet<string> travelRuleIds,
        HashSet<string> speciesIds,
        HashSet<string> creatureInstanceIds,
        HashSet<string> factionIds,
        HashSet<string> moveIds,
        HashSet<string> abilityIds,
        List<string> errors)
    {
        if (module.Locations is null)
        {
            return;
        }

        foreach (var (locationId, location) in module.Locations)
        {
            if (location is null)
            {
                continue;
            }

            if (!string.Equals(location.LocationId?.Trim(), locationId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Location key '{locationId}' does not match locationId '{location.LocationId}'.");
            }

            if (location.PointsOfInterest is not null)
            {
                foreach (var poi in location.PointsOfInterest)
                {
                    var poiId = poi.Id?.Trim() ?? string.Empty;
                    EnsureAllExist(poi.RelatedNpcIds, $"locations.{locationId}.pointsOfInterest.{poiId}.relatedNpcIds", errors, npcIds);
                    EnsureAllExist(poi.RelatedItemIds, $"locations.{locationId}.pointsOfInterest.{poiId}.relatedItemIds", errors, itemIds);
                }
            }

            if (location.Encounters is not null)
            {
                foreach (var encounter in location.Encounters)
                {
                    var encounterId = encounter.EncounterId?.Trim() ?? string.Empty;
                    EnsureParticipants(encounter.Participants, $"locations.{locationId}.encounters.{encounterId}.participants", errors, npcIds, creatureInstanceIds, speciesIds, factionIds);

                    if (encounter.Outcomes is not null)
                    {
                        foreach (var outcome in encounter.Outcomes)
                        {
                            if (outcome.ResultingChanges is null)
                            {
                                continue;
                            }

                            foreach (var change in outcome.ResultingChanges)
                            {
                                ValidateStateChange(
                                    change,
                                    $"locations.{locationId}.encounters.{encounterId}.outcomes",
                                    errors,
                                    locationIds,
                                    npcIds,
                                    itemIds,
                                    questIds,
                                    questStageIds,
                                    loreIds,
                                    speciesIds,
                                    creatureInstanceIds,
                                    factionIds,
                                    moveIds,
                                    abilityIds,
                                    travelRuleIds);
                            }
                        }
                    }
                }
            }

            if (location.ConnectedLocations is not null)
            {
                foreach (var connection in location.ConnectedLocations)
                {
                    EnsureExists(connection.TargetLocationId, locationIds, $"locations.{locationId}.connectedLocations -> {connection.TargetLocationId}", "location", errors);
                }
            }
        }
    }

    private static void ValidateNpcs(
        AdventureModule module,
        HashSet<string> npcIds,
        HashSet<string> classIds,
        HashSet<string> itemIds,
        HashSet<string> factionIds,
        HashSet<string> questIds,
        List<string> errors)
    {
        if (module.Npcs is null)
        {
            return;
        }

        foreach (var (npcId, npc) in module.Npcs)
        {
            if (npc is null)
            {
                continue;
            }

            if (!string.Equals(npc.NpcId?.Trim(), npcId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"NPC key '{npcId}' does not match npcId '{npc.NpcId}'.");
            }

            if (!string.IsNullOrWhiteSpace(npc.CharacterDetails?.Class) && !classIds.Contains(npc.CharacterDetails.Class.Trim()))
            {
                errors.Add($"NPC '{npcId}' references unknown class '{npc.CharacterDetails.Class}'.");
            }

            if (npc.CharacterDetails?.Inventory is not null)
            {
                foreach (var item in npc.CharacterDetails.Inventory)
                {
                    if (string.IsNullOrWhiteSpace(item.ItemId))
                    {
                        errors.Add($"NPC '{npcId}' inventory entry is missing itemId.");
                        continue;
                    }

                    EnsureExists(item.ItemId, itemIds, $"npcs.{npcId}.inventory -> {item.ItemId}", "item", errors);
                }
            }

            EnsureAllExist(npc.Factions, $"npcs.{npcId}.factions", errors, factionIds);

            if (npc.Relationships is not null)
            {
                foreach (var relationship in npc.Relationships)
                {
                    if (string.IsNullOrWhiteSpace(relationship.TargetId))
                    {
                        continue;
                    }

                    EnsureExists(relationship.TargetId, npcIds, factionIds, $"npcs.{npcId}.relationships -> {relationship.TargetId}", errors);
                }
            }

            if (npc.DialogueScripts is not null)
            {
                foreach (var script in npc.DialogueScripts)
                {
                    if (script.Lines is null)
                    {
                        continue;
                    }

                    foreach (var line in script.Lines)
                    {
                        if (string.IsNullOrWhiteSpace(line.Speaker))
                        {
                            continue;
                        }

                        if (IsReservedId(line.Speaker))
                        {
                            continue;
                        }

                        EnsureExists(line.Speaker, npcIds, factionIds, $"npcs.{npcId}.dialogueScripts.{script.ScriptId}.lines -> speaker '{line.Speaker}'", errors);
                    }
                }
            }
        }
    }

    private static void ValidateCreatureSpecies(
        AdventureModule module,
        HashSet<string> speciesIds,
        HashSet<string> moveIds,
        HashSet<string> abilityIds,
        List<string> errors)
    {
        if (module.Bestiary is null)
        {
            return;
        }

        foreach (var (speciesId, species) in module.Bestiary)
        {
            if (species is null)
            {
                continue;
            }

            EnsureAllExist(species.DefaultMoves, $"bestiary.{speciesId}.defaultMoves", errors, moveIds);

            if (species.LevelUpMoves is not null)
            {
                foreach (var (level, moves) in species.LevelUpMoves)
                {
                    var path = $"bestiary.{speciesId}.levelUpMoves[{level}]";
                    EnsureAllExist(moves, path, errors, moveIds);
                }
            }

            EnsureAllExist(species.AbilityIds, $"bestiary.{speciesId}.abilityIds", errors, abilityIds);
        }
    }

    private static void ValidateCreatureInstances(
        AdventureModule module,
        HashSet<string> creatureInstanceIds,
        HashSet<string> speciesIds,
        HashSet<string> locationIds,
        HashSet<string> npcIds,
        HashSet<string> moveIds,
        HashSet<string> factionIds,
        List<string> errors)
    {
        if (module.CreatureInstances is null)
        {
            return;
        }

        foreach (var (instanceId, instance) in module.CreatureInstances)
        {
            if (instance is null)
            {
                continue;
            }

            EnsureExists(instance.SpeciesId, speciesIds, $"creatureInstances.{instanceId}.speciesId", "creature species", errors);
            if (!string.IsNullOrWhiteSpace(instance.LocationId))
            {
                EnsureExists(instance.LocationId, locationIds, $"creatureInstances.{instanceId}.locationId", "location", errors);
            }

            if (!string.IsNullOrWhiteSpace(instance.OwnerNpcId))
            {
                EnsureExists(instance.OwnerNpcId, npcIds, $"creatureInstances.{instanceId}.ownerNpcId", "npc", errors);
            }

            EnsureAllExist(instance.Moves, $"creatureInstances.{instanceId}.moves", errors, moveIds);
            EnsureAllExist(instance.FactionIds, $"creatureInstances.{instanceId}.factionIds", errors, factionIds);
        }
    }

    private static void ValidateItems(
        AdventureModule module,
        HashSet<string> itemIds,
        HashSet<string> locationIds,
        HashSet<string> npcIds,
        List<string> errors)
    {
        if (module.Items is null)
        {
            return;
        }

        foreach (var (itemId, item) in module.Items)
        {
            if (item is null)
            {
                continue;
            }

            if (!string.Equals(item.ItemId?.Trim(), itemId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Item key '{itemId}' does not match itemId '{item.ItemId}'.");
            }

            if (item.Placement is null)
            {
                continue;
            }

            foreach (var placement in item.Placement)
            {
                EnsureExists(placement.LocationId, locationIds, $"items.{itemId}.placement -> locationId '{placement.LocationId}'", "location", errors);
                if (!string.IsNullOrWhiteSpace(placement.NpcId))
                {
                    EnsureExists(placement.NpcId, npcIds, $"items.{itemId}.placement -> npcId '{placement.NpcId}'", "npc", errors);
                }
            }
        }
    }

    private static void ValidateFactions(
        AdventureModule module,
        HashSet<string> factionIds,
        HashSet<string> npcIds,
        List<string> errors)
    {
        if (module.Factions is null)
        {
            return;
        }

        foreach (var (factionId, faction) in module.Factions)
        {
            if (faction is null)
            {
                continue;
            }

            if (!string.Equals(faction.FactionId?.Trim(), factionId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Faction key '{factionId}' does not match factionId '{faction.FactionId}'.");
            }

            EnsureAllExist(faction.Leaders, $"factions.{factionId}.leaders", errors, npcIds);

            if (faction.Relationships is not null)
            {
                foreach (var relationship in faction.Relationships)
                {
                    if (string.IsNullOrWhiteSpace(relationship.TargetId))
                    {
                        continue;
                    }

                    EnsureExists(relationship.TargetId, npcIds, factionIds, $"factions.{factionId}.relationships -> {relationship.TargetId}", errors);
                }
            }
        }
    }

    private static void ValidateQuestLines(
        AdventureModule module,
        HashSet<string> questIds,
        HashSet<string> questStageIds,
        HashSet<string> locationIds,
        HashSet<string> npcIds,
        List<string> errors)
    {
        if (module.QuestLines is null)
        {
            return;
        }

        foreach (var (questId, quest) in module.QuestLines)
        {
            if (quest is null)
            {
                continue;
            }

            if (!string.Equals(quest.QuestId?.Trim(), questId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Quest key '{questId}' does not match questId '{quest.QuestId}'.");
            }

            if (quest.Stages is null)
            {
                continue;
            }

            var stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stage in quest.Stages)
            {
                if (string.IsNullOrWhiteSpace(stage.StageId))
                {
                    errors.Add($"Quest '{questId}' has a stage with a missing stageId.");
                }
                else if (!stageIds.Add(stage.StageId.Trim()))
                {
                    errors.Add($"Quest '{questId}' has duplicate stageId '{stage.StageId}'.");
                }
                else
                {
                    questStageIds.Add(stage.StageId.Trim());
                }

                EnsureAllExist(stage.RecommendedNpcIds, $"questLines.{questId}.stages.{stage.StageId}.recommendedNpcIds", errors, npcIds);
                EnsureAllExist(stage.RecommendedLocationIds, $"questLines.{questId}.stages.{stage.StageId}.recommendedLocationIds", errors, locationIds);
            }
        }
    }

    private static void ValidateLoreEntries(AdventureModule module, List<string> errors)
    {
        if (module.LoreEntries is null)
        {
            return;
        }

        foreach (var (loreId, entry) in module.LoreEntries)
        {
            if (entry is null)
            {
                continue;
            }

            if (!string.Equals(entry.EntryId?.Trim(), loreId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Lore entry key '{loreId}' does not match entryId '{entry.EntryId}'.");
            }
        }
    }

    private static void ValidateScriptedEvents(
        AdventureModule module,
        HashSet<string> eventIds,
        HashSet<string> npcIds,
        HashSet<string> questIds,
        HashSet<string> questStageIds,
        HashSet<string> loreIds,
        HashSet<string> locationIds,
        HashSet<string> itemIds,
        HashSet<string> factionIds,
        HashSet<string> creatureInstanceIds,
        HashSet<string> speciesIds,
        HashSet<string> moveIds,
        HashSet<string> abilityIds,
        HashSet<string> travelRuleIds,
        List<string> errors)
    {
        if (module.ScriptedEvents is null)
        {
            return;
        }

        foreach (var (eventId, scriptedEvent) in module.ScriptedEvents)
        {
            if (scriptedEvent is null)
            {
                continue;
            }

            if (!string.Equals(scriptedEvent.EventId?.Trim(), eventId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Scripted event key '{eventId}' does not match eventId '{scriptedEvent.EventId}'.");
            }

            if (scriptedEvent.Scenes is not null)
            {
                foreach (var scene in scriptedEvent.Scenes)
                {
                    if (scene.DialogueScripts is null)
                    {
                        continue;
                    }

                    foreach (var script in scene.DialogueScripts)
                    {
                        if (script.Lines is null)
                        {
                            continue;
                        }

                        foreach (var line in script.Lines)
                        {
                            if (string.IsNullOrWhiteSpace(line.Speaker))
                            {
                                continue;
                            }

                            if (IsReservedId(line.Speaker))
                            {
                                continue;
                            }

                            EnsureExists(line.Speaker, npcIds, factionIds, $"scriptedEvents.{eventId}.scenes.{scene.SceneId}.dialogueScripts.{script.ScriptId}.speaker", errors);
                        }
                    }
                }
            }

            if (scriptedEvent.Outcomes is not null)
            {
                foreach (var outcome in scriptedEvent.Outcomes)
                {
                    if (outcome.ResultingChanges is null)
                    {
                        continue;
                    }

                    foreach (var change in outcome.ResultingChanges)
                    {
                        ValidateStateChange(
                            change,
                            $"scriptedEvents.{eventId}.outcomes",
                            errors,
                            locationIds,
                            npcIds,
                            itemIds,
                            questIds,
                            questStageIds,
                            loreIds,
                            speciesIds,
                            creatureInstanceIds,
                            factionIds,
                            moveIds,
                            abilityIds,
                            travelRuleIds);
                    }
                }
            }
        }
    }

    private static void ValidateScenarioScripts(AdventureModule module, HashSet<string> scenarioScriptIds, HashSet<string> questIds, HashSet<string> questStageIds, List<string> errors)
    {
        if (module.ScenarioScripts is null)
        {
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in module.ScenarioScripts)
        {
            if (script is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(script.ScriptId))
            {
                errors.Add("scenarioScripts contains a script without scriptId.");
                continue;
            }

            if (!seenIds.Add(script.ScriptId.Trim()))
            {
                errors.Add($"scenarioScripts contains duplicate scriptId '{script.ScriptId}'.");
            }

            EnsureAllExist(script.LinkedQuestIds, $"scenarioScripts.{script.ScriptId}.linkedQuestIds", errors, questIds, questStageIds);
        }
    }

    private static HashSet<string> CollectTravelRuleIds(AdventureModule module)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (module.MechanicalReferences?.TravelRules is null)
        {
            return set;
        }

        foreach (var rule in module.MechanicalReferences.TravelRules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
            {
                continue;
            }

            set.Add(rule.RuleId.Trim());
        }

        return set;
    }

    private static void ValidateMechanicalReferences(
        AdventureModule module,
        HashSet<string> locationIds,
        HashSet<string> speciesIds,
        HashSet<string> creatureInstanceIds,
        HashSet<string> travelRuleIds,
        List<string> errors)
    {
        if (module.MechanicalReferences is null)
        {
            return;
        }

        if (module.MechanicalReferences.EncounterTables is not null)
        {
            foreach (var table in module.MechanicalReferences.EncounterTables)
            {
                if (table is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(table.TableId))
                {
                    errors.Add("Encounter table is missing tableId.");
                    continue;
                }

                EnsureExists(table.LocationId, locationIds, $"mechanics.encounterTables.{table.TableId}.locationId", "location", errors);

                if (table.Entries is null)
                {
                    continue;
                }

                foreach (var entry in table.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.CreatureId))
                    {
                        errors.Add($"Encounter table '{table.TableId}' has an entry with missing creatureId.");
                        continue;
                    }

                    EnsureExists(entry.CreatureId, speciesIds, creatureInstanceIds, $"mechanics.encounterTables.{table.TableId}.entries -> {entry.CreatureId}", errors);
                }
            }
        }

        if (module.MechanicalReferences.TravelRules is not null)
        {
            var seenRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in module.MechanicalReferences.TravelRules)
            {
                if (rule is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.RuleId))
                {
                    errors.Add("Travel rule is missing ruleId.");
                    continue;
                }

                var ruleId = rule.RuleId.Trim();
                if (!seenRules.Add(ruleId))
                {
                    errors.Add($"Duplicate travel rule id detected: '{ruleId}'.");
                }

                travelRuleIds.Add(ruleId);
            }
        }
    }

    private static void ValidateStateChange(
        AdventureModuleStateChange change,
        string path,
        List<string> errors,
        HashSet<string> locationIds,
        HashSet<string> npcIds,
        HashSet<string>? itemIds,
        HashSet<string> questIds,
        HashSet<string> questStageIds,
        HashSet<string> loreIds,
        HashSet<string> speciesIds,
        HashSet<string> creatureInstanceIds,
        HashSet<string>? factionIds,
        HashSet<string>? moveIds,
        HashSet<string>? abilityIds,
        HashSet<string> travelRuleIds)
    {
        if (change is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(change.TargetType) || string.IsNullOrWhiteSpace(change.TargetId))
        {
            return;
        }

        var targetType = change.TargetType.Trim().ToLowerInvariant();
        var targetId = change.TargetId.Trim();

        switch (targetType)
        {
            case "location":
                EnsureExists(targetId, locationIds, $"{path}.stateChange -> {targetId}", "location", errors);
                break;
            case "npc":
                EnsureExists(targetId, npcIds, $"{path}.stateChange -> {targetId}", "npc", errors);
                break;
            case "item":
                if (itemIds is not null)
                {
                    EnsureExists(targetId, itemIds, $"{path}.stateChange -> {targetId}", "item", errors);
                }
                break;
            case "quest":
            case "queststage":
                EnsureExists(targetId, questIds, questStageIds, $"{path}.stateChange -> {targetId}", errors);
                break;
            case "lore":
                EnsureExists(targetId, loreIds, $"{path}.stateChange -> {targetId}", "lore entry", errors);
                break;
            case "creature":
            case "pokemon":
                EnsureExists(targetId, speciesIds, creatureInstanceIds, $"{path}.stateChange -> {targetId}", errors);
                break;
            case "faction":
                if (factionIds is not null)
                {
                    EnsureExists(targetId, factionIds, $"{path}.stateChange -> {targetId}", "faction", errors);
                }
                break;
            case "move":
                if (moveIds is not null)
                {
                    EnsureExists(targetId, moveIds, $"{path}.stateChange -> {targetId}", "move", errors);
                }
                break;
            case "ability":
                if (abilityIds is not null)
                {
                    EnsureExists(targetId, abilityIds, $"{path}.stateChange -> {targetId}", "ability", errors);
                }
                break;
            case "rule":
            case "travelrule":
                EnsureExists(targetId, travelRuleIds, $"{path}.stateChange -> {targetId}", "travel rule", errors);
                break;
            case "player":
            case "session":
                // Player/session scoped IDs are validated in runtime systems; skip module enforcement.
                break;
            default:
                // For unknown types, ensure the ID exists in any known set to prevent typos.
                if (!ExistsInAny(targetId, locationIds, npcIds, itemIds, questIds, questStageIds, loreIds, speciesIds, creatureInstanceIds, factionIds, moveIds, abilityIds, travelRuleIds))
                {
                    errors.Add($"{path}.stateChange references unknown {change.TargetType} id '{targetId}'.");
                }
                break;
        }
    }

    private static void EnsureParticipants(
        IEnumerable<string>? participants,
        string path,
        List<string> errors,
        params HashSet<string>[] allowedSets)
    {
        if (participants is null)
        {
            return;
        }

        foreach (var rawId in participants)
        {
            var id = rawId?.Trim();
            if (string.IsNullOrWhiteSpace(id) || IsReservedId(id))
            {
                continue;
            }

            if (!allowedSets.Any(set => set.Contains(id)))
            {
                errors.Add($"{path} references unknown participant '{id}'.");
            }
        }
    }

    private static void EnsureExists(string? rawId, HashSet<string> validSet, string path, string category, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rawId))
        {
            errors.Add($"{path} is missing a {category} id.");
            return;
        }

        if (!validSet.Contains(rawId.Trim()))
        {
            errors.Add($"{path} references unknown {category} id '{rawId}'.");
        }
    }

    private static void EnsureExists(string? rawId, HashSet<string> setA, HashSet<string>? setB, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rawId))
        {
            errors.Add($"{path} is missing an id.");
            return;
        }

        var id = rawId.Trim();
        var exists = setA.Contains(id) || (setB is not null && setB.Contains(id));
        if (!exists)
        {
            errors.Add($"{path} references unknown id '{rawId}'.");
        }
    }

    private static void EnsureAllExist(IEnumerable<string>? ids, string path, List<string> errors, params HashSet<string>[] validSets)
    {
        if (ids is null)
        {
            return;
        }

        foreach (var rawId in ids)
        {
            var id = rawId?.Trim();
            if (string.IsNullOrWhiteSpace(id) || IsReservedId(id))
            {
                continue;
            }

            if (!validSets.Any(set => set.Contains(id)))
            {
                errors.Add($"{path} references unknown id '{rawId}'.");
            }
        }
    }

    private static bool ExistsInAny(string id, params HashSet<string>?[] sets)
    {
        foreach (var set in sets)
        {
            if (set is not null && set.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReservedId(string? id)
        => id is not null && ReservedParticipantIds.Contains(id.Trim());
}

public sealed class AdventureModuleValidationResult
{
    public bool IsValid { get; set; }

    public List<string> Errors { get; } = new();
}
