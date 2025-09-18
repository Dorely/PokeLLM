using System.Collections.Generic;
using System.ComponentModel;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins.Models;

public class ModuleMetadataUpdateDto
{
    [Description("Optional new title for the adventure module.")]
    public string? Title { get; set; }

    [Description("Optional module summary update.")]
    public string? Summary { get; set; }

    [Description("Optional semantic version string for the module.")]
    public string? Version { get; set; }

    [Description("Optional recommended level range text.")]
    public string? RecommendedLevelRange { get; set; }

    [Description("Optional tag list describing the module.")]
    public List<string>? Tags { get; set; }
}

public class WorldOverviewUpdateDto
{
    [Description("World setting text describing the overall region.")]
    public string? Setting { get; set; }

    [Description("Narrative tone for the adventure.")]
    public string? Tone { get; set; }

    [Description("Immediate starting context for players entering the world.")]
    public string? StartingContext { get; set; }

    [Description("Historical or time-period framing.")]
    public string? TimePeriod { get; set; }

    [Description("Maturity rating guidance for content.")]
    public string? MaturityRating { get; set; }

    [Description("Primary themes the world explores.")]
    public List<string>? Themes { get; set; }

    [Description("Adventure hooks that draw the players into the story.")]
    public List<string>? Hooks { get; set; }

    [Description("Safety or calibration considerations flagged during setup.")]
    public List<string>? SafetyConsiderations { get; set; }
}

public class MechanicalReferencesUpdateDto
{
    [Description("Encounter tables to upsert.")]
    public List<AdventureModuleEncounterTable>? EncounterTables { get; set; }

    [Description("Weather profiles to upsert.")]
    public List<AdventureModuleWeatherProfile>? WeatherProfiles { get; set; }

    [Description("Travel rules to upsert.")]
    public List<AdventureModuleTravelRule>? TravelRules { get; set; }
}

public class WorldGenerationUpdateBatch
{
    [Description("Optional metadata updates to apply.")]
    public ModuleMetadataUpdateDto? Metadata { get; set; }

    [Description("Optional world overview updates to apply.")]
    public WorldOverviewUpdateDto? World { get; set; }

    [Description("Locations to upsert, keyed by locationId.")]
    public Dictionary<string, AdventureModuleLocation>? Locations { get; set; }

    [Description("NPCs to upsert, keyed by npcId.")]
    public Dictionary<string, AdventureModuleNpc>? Npcs { get; set; }

    [Description("Creature species to upsert, keyed by speciesId.")]
    public Dictionary<string, AdventureModuleCreatureSpecies>? CreatureSpecies { get; set; }

    [Description("Creature instances to upsert, keyed by instanceId.")]
    public Dictionary<string, AdventureModuleCreatureInstance>? CreatureInstances { get; set; }

    [Description("Items to upsert, keyed by itemId.")]
    public Dictionary<string, AdventureModuleItem>? Items { get; set; }

    [Description("Factions to upsert, keyed by factionId.")]
    public Dictionary<string, AdventureModuleFaction>? Factions { get; set; }

    [Description("Lore entries to upsert, keyed by loreId.")]
    public Dictionary<string, AdventureModuleLoreEntry>? LoreEntries { get; set; }

    [Description("Scripted events to upsert, keyed by eventId.")]
    public Dictionary<string, AdventureModuleScriptedEvent>? ScriptedEvents { get; set; }

    [Description("Quest lines to upsert, keyed by questId.")]
    public Dictionary<string, AdventureModuleQuestLine>? QuestLines { get; set; }

    [Description("Moves to upsert, keyed by moveId.")]
    public Dictionary<string, AdventureModuleMove>? Moves { get; set; }

    [Description("Abilities to upsert, keyed by abilityId.")]
    public Dictionary<string, AdventureModuleAbility>? Abilities { get; set; }

    [Description("Scenario script records to upsert.")]
    public List<AdventureModuleScenarioScript>? ScenarioScripts { get; set; }

    [Description("Mechanical reference updates to merge.")]
    public MechanicalReferencesUpdateDto? MechanicalReferences { get; set; }

    [Description("IDs of locations to remove from the module.")]
    public List<string>? RemoveLocationIds { get; set; }

    [Description("IDs of NPCs to remove from the module.")]
    public List<string>? RemoveNpcIds { get; set; }

    [Description("IDs of species to remove from the module.")]
    public List<string>? RemoveSpeciesIds { get; set; }

    [Description("IDs of creature instances to remove from the module.")]
    public List<string>? RemoveCreatureInstanceIds { get; set; }

    [Description("IDs of items to remove from the module.")]
    public List<string>? RemoveItemIds { get; set; }

    [Description("IDs of factions to remove from the module.")]
    public List<string>? RemoveFactionIds { get; set; }

    [Description("IDs of lore entries to remove from the module.")]
    public List<string>? RemoveLoreEntryIds { get; set; }

    [Description("IDs of scripted events to remove from the module.")]
    public List<string>? RemoveScriptedEventIds { get; set; }

    [Description("IDs of quests to remove from the module.")]
    public List<string>? RemoveQuestIds { get; set; }

    [Description("IDs of moves to remove from the module.")]
    public List<string>? RemoveMoveIds { get; set; }

    [Description("IDs of abilities to remove from the module.")]
    public List<string>? RemoveAbilityIds { get; set; }

    [Description("IDs of scenario scripts to remove from the module.")]
    public List<string>? RemoveScenarioScriptIds { get; set; }

    [Description("Whether to reapply the module baseline to the active session after saving.")]
    public bool ReapplyBaseline { get; set; } = true;

    public bool HasContent()
    {
        return Metadata is not null
            || World is not null
            || HasDictionaryContent(Locations)
            || HasDictionaryContent(Npcs)
            || HasDictionaryContent(CreatureSpecies)
            || HasDictionaryContent(CreatureInstances)
            || HasDictionaryContent(Items)
            || HasDictionaryContent(Factions)
            || HasDictionaryContent(LoreEntries)
            || HasDictionaryContent(ScriptedEvents)
            || HasDictionaryContent(QuestLines)
            || HasDictionaryContent(Moves)
            || HasDictionaryContent(Abilities)
            || (ScenarioScripts is not null && ScenarioScripts.Count > 0)
            || (MechanicalReferences is not null && (
                (MechanicalReferences.EncounterTables?.Count ?? 0) > 0 ||
                (MechanicalReferences.WeatherProfiles?.Count ?? 0) > 0 ||
                (MechanicalReferences.TravelRules?.Count ?? 0) > 0))
            || HasListContent(RemoveLocationIds)
            || HasListContent(RemoveNpcIds)
            || HasListContent(RemoveSpeciesIds)
            || HasListContent(RemoveCreatureInstanceIds)
            || HasListContent(RemoveItemIds)
            || HasListContent(RemoveFactionIds)
            || HasListContent(RemoveLoreEntryIds)
            || HasListContent(RemoveScriptedEventIds)
            || HasListContent(RemoveQuestIds)
            || HasListContent(RemoveMoveIds)
            || HasListContent(RemoveAbilityIds)
            || HasListContent(RemoveScenarioScriptIds);
    }

    private static bool HasDictionaryContent<T>(Dictionary<string, T>? dictionary)
        => dictionary is not null && dictionary.Count > 0;

    private static bool HasListContent(List<string>? list)
        => list is not null && list.Count > 0;
}
