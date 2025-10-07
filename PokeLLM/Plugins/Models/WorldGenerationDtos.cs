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

public class WorldDictionaryBatch<T>
{
    [Description("Entries keyed by unique identifier.")]
    public Dictionary<string, T>? Entries { get; set; }

    [Description("Set true to treat the entries as removals instead of upserts.")]
    public bool IsRemoval { get; set; }

    public bool HasContent() => Entries is not null && Entries.Count > 0;
}

public class WorldListBatch<T>
{
    [Description("Entries in the batch.")]
    public List<T>? Entries { get; set; }

    [Description("Set true to treat the entries as removals instead of upserts.")]
    public bool IsRemoval { get; set; }

    public bool HasContent() => Entries is not null && Entries.Count > 0;
}

public class WorldGenerationUpdateBatch
{
    [Description("Optional metadata updates to apply.")]
    public ModuleMetadataUpdateDto? Metadata { get; set; }

    [Description("Optional world overview updates to apply.")]
    public WorldOverviewUpdateDto? World { get; set; }

    [Description("Locations batch keyed by locationId.")]
    public WorldDictionaryBatch<AdventureModuleLocation>? Locations { get; set; }

    [Description("NPCs batch keyed by npcId.")]
    public WorldDictionaryBatch<AdventureModuleNpc>? Npcs { get; set; }

    [Description("Creature species batch keyed by speciesId.")]
    public WorldDictionaryBatch<AdventureModuleCreatureSpecies>? CreatureSpecies { get; set; }

    [Description("Creature instances batch keyed by instanceId.")]
    public WorldDictionaryBatch<AdventureModuleCreatureInstance>? CreatureInstances { get; set; }

    [Description("Items batch keyed by itemId.")]
    public WorldDictionaryBatch<AdventureModuleItem>? Items { get; set; }

    [Description("Factions batch keyed by factionId.")]
    public WorldDictionaryBatch<AdventureModuleFaction>? Factions { get; set; }

    [Description("Lore entries batch keyed by loreId.")]
    public WorldDictionaryBatch<AdventureModuleLoreEntry>? LoreEntries { get; set; }

    [Description("Scripted events batch keyed by eventId.")]
    public WorldDictionaryBatch<AdventureModuleScriptedEvent>? ScriptedEvents { get; set; }

    [Description("Quest lines batch keyed by questId.")]
    public WorldDictionaryBatch<AdventureModuleQuestLine>? QuestLines { get; set; }

    [Description("Moves batch keyed by moveId.")]
    public WorldDictionaryBatch<AdventureModuleMove>? Moves { get; set; }

    [Description("Abilities batch keyed by abilityId.")]
    public WorldDictionaryBatch<AdventureModuleAbility>? Abilities { get; set; }

    [Description("Scenario script batch.")]
    public WorldListBatch<AdventureModuleScenarioScript>? ScenarioScripts { get; set; }

    [Description("Mechanical reference updates to merge.")]
    public MechanicalReferencesUpdateDto? MechanicalReferences { get; set; }

    [Description("Whether to reapply the module baseline to the active session after saving.")]
    public bool ReapplyBaseline { get; set; } = true;

    public bool HasContent()
    {
        return Metadata is not null
            || World is not null
            || Locations?.HasContent() == true
            || Npcs?.HasContent() == true
            || CreatureSpecies?.HasContent() == true
            || CreatureInstances?.HasContent() == true
            || Items?.HasContent() == true
            || Factions?.HasContent() == true
            || LoreEntries?.HasContent() == true
            || ScriptedEvents?.HasContent() == true
            || QuestLines?.HasContent() == true
            || Moves?.HasContent() == true
            || Abilities?.HasContent() == true
            || ScenarioScripts?.HasContent() == true
            || HasMechanicalReferenceContent(MechanicalReferences);
    }

    private static bool HasMechanicalReferenceContent(MechanicalReferencesUpdateDto? references)
    {
        if (references is null)
        {
            return false;
        }

        return (references.EncounterTables?.Count ?? 0) > 0
            || (references.WeatherProfiles?.Count ?? 0) > 0
            || (references.TravelRules?.Count ?? 0) > 0;
    }
}
