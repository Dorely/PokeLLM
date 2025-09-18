using System.Text.Json.Serialization;

namespace PokeLLM.GameState.Models;

/// <summary>
/// Represents the canonical adventure module produced during world generation.
/// </summary>
public class AdventureModule
{
    [JsonPropertyName("metadata")]
    public AdventureModuleMetadata Metadata { get; set; } = new();

    [JsonPropertyName("world")]
    public AdventureModuleWorldOverview World { get; set; } = new();

    [JsonPropertyName("locations")]
    public Dictionary<string, AdventureModuleLocation> Locations { get; set; } = new();

    [JsonPropertyName("npcs")]
    public Dictionary<string, AdventureModuleNpc> Npcs { get; set; } = new();

    [JsonPropertyName("bestiary")]
    public Dictionary<string, AdventureModuleCreatureSpecies> Bestiary { get; set; } = new();

    [JsonPropertyName("creatureInstances")]
    public Dictionary<string, AdventureModuleCreatureInstance> CreatureInstances { get; set; } = new();

    [JsonPropertyName("items")]
    public Dictionary<string, AdventureModuleItem> Items { get; set; } = new();

    [JsonPropertyName("factions")]
    public Dictionary<string, AdventureModuleFaction> Factions { get; set; } = new();

    [JsonPropertyName("loreEntries")]
    public Dictionary<string, AdventureModuleLoreEntry> LoreEntries { get; set; } = new();

    [JsonPropertyName("events")]
    public Dictionary<string, AdventureModuleScriptedEvent> ScriptedEvents { get; set; } = new();

    [JsonPropertyName("quests")]
    public Dictionary<string, AdventureModuleQuestLine> QuestLines { get; set; } = new();

    [JsonPropertyName("mechanics")]
    public AdventureModuleMechanicalReferences MechanicalReferences { get; set; } = new();

    [JsonPropertyName("scenarioScripts")]
    public List<AdventureModuleScenarioScript> ScenarioScripts { get; set; } = new();

    [JsonPropertyName("characterClasses")]
    public Dictionary<string, AdventureModuleCharacterClass> CharacterClasses { get; set; } = new();

    [JsonPropertyName("moves")]
    public Dictionary<string, AdventureModuleMove> Moves { get; set; } = new();

    [JsonPropertyName("abilities")]
    public Dictionary<string, AdventureModuleAbility> Abilities { get; set; } = new();
}

public class AdventureModuleMetadata
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("generator")]
    public AdventureModuleGeneratorMetadata Generator { get; set; } = new();

    [JsonPropertyName("recommendedLevelRange")]
    public string RecommendedLevelRange { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("isSetupComplete")]
    public bool IsSetupComplete { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class AdventureModuleGeneratorMetadata
{
    [JsonPropertyName("llmProvider")]
    public string LlmProvider { get; set; } = string.Empty;

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("seedPrompt")]
    public string SeedPrompt { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class AdventureModuleWorldOverview
{
    [JsonPropertyName("setting")]
    public string Setting { get; set; } = string.Empty;

    [JsonPropertyName("themes")]
    public List<string> Themes { get; set; } = new();

    [JsonPropertyName("startingContext")]
    public string StartingContext { get; set; } = string.Empty;

    [JsonPropertyName("tone")]
    public string Tone { get; set; } = string.Empty;

    [JsonPropertyName("timePeriod")]
    public string TimePeriod { get; set; } = string.Empty;

    [JsonPropertyName("maturityRating")]
    public string MaturityRating { get; set; } = string.Empty;

    [JsonPropertyName("hooks")]
    public List<string> AdventureHooks { get; set; } = new();

    [JsonPropertyName("safetyConsiderations")]
    public List<string> SafetyConsiderations { get; set; } = new();
}

public class AdventureModuleLocation
{
    [JsonPropertyName("locationId")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("factionsPresent")]
    public List<string> FactionsPresent { get; set; } = new();

    [JsonPropertyName("pointsOfInterest")]
    public List<AdventureModulePointOfInterest> PointsOfInterest { get; set; } = new();

    [JsonPropertyName("encounters")]
    public List<AdventureModuleEncounter> Encounters { get; set; } = new();

    [JsonPropertyName("connectedLocations")]
    public List<AdventureModuleLocationConnection> ConnectedLocations { get; set; } = new();
}

public class AdventureModulePointOfInterest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("relatedNpcIds")]
    public List<string> RelatedNpcIds { get; set; } = new();

    [JsonPropertyName("relatedItemIds")]
    public List<string> RelatedItemIds { get; set; } = new();
}

public class AdventureModuleEncounter
{
    [JsonPropertyName("encounterId")]
    public string EncounterId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    [JsonPropertyName("narrative")]
    public string Narrative { get; set; } = string.Empty;

    [JsonPropertyName("participants")]
    public List<string> Participants { get; set; } = new();

    [JsonPropertyName("outcomes")]
    public List<AdventureModuleOutcome> Outcomes { get; set; } = new();
}

public class AdventureModuleOutcome
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("resultingChanges")]
    public List<AdventureModuleStateChange> ResultingChanges { get; set; } = new();
}

public class AdventureModuleLocationConnection
{
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("targetLocationId")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class AdventureModuleNpc
{
    [JsonPropertyName("npcId")]
    public string NpcId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("motivation")]
    public string Motivation { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("characterDetails")]
    public CharacterDetails CharacterDetails { get; set; } = new();

    [JsonPropertyName("factions")]
    public List<string> Factions { get; set; } = new();

    [JsonPropertyName("relationships")]
    public List<AdventureModuleRelationship> Relationships { get; set; } = new();

    [JsonPropertyName("dialogueScripts")]
    public List<AdventureModuleDialogueScript> DialogueScripts { get; set; } = new();
}

public class AdventureModuleCreatureSpecies
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("habitats")]
    public List<string> Habitats { get; set; } = new();

    [JsonPropertyName("defaultMoves")]
    public List<string> DefaultMoves { get; set; } = new();

    [JsonPropertyName("baseLevel")]
    public int BaseLevel { get; set; }

    [JsonPropertyName("baseStats")]
    public Stats BaseStats { get; set; } = new();

    [JsonPropertyName("levelUpMoves")]
    public Dictionary<int, List<string>> LevelUpMoves { get; set; } = new();

    [JsonPropertyName("evolutionConditions")]
    public List<string> EvolutionConditions { get; set; } = new();

    [JsonPropertyName("behaviorNotes")]
    public string BehaviorNotes { get; set; } = string.Empty;

    [JsonPropertyName("abilityIds")]
    public List<string> AbilityIds { get; set; } = new();
}

public class AdventureModuleCreatureInstance
{
    [JsonPropertyName("speciesId")]
    public string SpeciesId { get; set; } = string.Empty;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("heldItem")]
    public string HeldItem { get; set; } = string.Empty;

    [JsonPropertyName("moves")]
    public List<string> Moves { get; set; } = new();

    [JsonPropertyName("locationId")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("ownerNpcId")]
    public string OwnerNpcId { get; set; } = string.Empty;

    [JsonPropertyName("factionIds")]
    public List<string> FactionIds { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}


public class AdventureModuleItem
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("effects")]
    public string Effects { get; set; } = string.Empty;

    [JsonPropertyName("defaultQuantity")]
    public int DefaultQuantity { get; set; }

    [JsonPropertyName("placement")]
    public List<AdventureModuleItemPlacement> Placement { get; set; } = new();
}

public class AdventureModuleItemPlacement
{
    [JsonPropertyName("locationId")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("npcId")]
    public string NpcId { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class AdventureModuleFaction
{
    [JsonPropertyName("factionId")]
    public string FactionId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ideology")]
    public string Ideology { get; set; } = string.Empty;

    [JsonPropertyName("leaders")]
    public List<string> Leaders { get; set; } = new();

    [JsonPropertyName("fullDescription")]
    public string FullDescription { get; set; } = string.Empty;

    [JsonPropertyName("relationships")]
    public List<AdventureModuleRelationship> Relationships { get; set; } = new();
}

public class AdventureModuleLoreEntry
{
    [JsonPropertyName("entryId")]
    public string EntryId { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("fullText")]
    public string FullText { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class AdventureModuleScriptedEvent
{
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("triggerConditions")]
    public List<string> TriggerConditions { get; set; } = new();

    [JsonPropertyName("scenes")]
    public List<AdventureModuleScene> Scenes { get; set; } = new();

    [JsonPropertyName("outcomes")]
    public List<AdventureModuleOutcome> Outcomes { get; set; } = new();
}

public class AdventureModuleQuestLine
{
    [JsonPropertyName("questId")]
    public string QuestId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("stages")]
    public List<AdventureModuleQuestStage> Stages { get; set; } = new();
}

public class AdventureModuleQuestStage
{
    [JsonPropertyName("stageId")]
    public string StageId { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("recommendedNpcIds")]
    public List<string> RecommendedNpcIds { get; set; } = new();

    [JsonPropertyName("recommendedLocationIds")]
    public List<string> RecommendedLocationIds { get; set; } = new();

    [JsonPropertyName("rewards")]
    public List<string> Rewards { get; set; } = new();
}

public class AdventureModuleRelationship
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public class AdventureModuleDialogueScript
{
    [JsonPropertyName("scriptId")]
    public string ScriptId { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    [JsonPropertyName("lines")]
    public List<AdventureModuleDialogueLine> Lines { get; set; } = new();
}

public class AdventureModuleDialogueLine
{
    [JsonPropertyName("speaker")]
    public string Speaker { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class AdventureModuleScene
{
    [JsonPropertyName("sceneId")]
    public string SceneId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dialogueScripts")]
    public List<AdventureModuleDialogueScript> DialogueScripts { get; set; } = new();
}

public class AdventureModuleStateChange
{
    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; } = string.Empty;

    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
}

public class AdventureModuleMechanicalReferences
{
    [JsonPropertyName("encounterTables")]
    public List<AdventureModuleEncounterTable> EncounterTables { get; set; } = new();

    [JsonPropertyName("weatherProfiles")]
    public List<AdventureModuleWeatherProfile> WeatherProfiles { get; set; } = new();

    [JsonPropertyName("travelRules")]
    public List<AdventureModuleTravelRule> TravelRules { get; set; } = new();
}

public class AdventureModuleEncounterTable
{
    [JsonPropertyName("tableId")]
    public string TableId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("locationId")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public List<AdventureModuleEncounterTableEntry> Entries { get; set; } = new();
}

public class AdventureModuleEncounterTableEntry
{
    [JsonPropertyName("rollRange")]
    public string RollRange { get; set; } = string.Empty;

    [JsonPropertyName("creatureId")]
    public string CreatureId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class AdventureModuleWeatherProfile
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("modifiers")]
    public Dictionary<string, string> Modifiers { get; set; } = new();
}

public class AdventureModuleTravelRule
{
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("effects")]
    public Dictionary<string, string> Effects { get; set; } = new();
}

public class AdventureModuleScenarioScript
{
    [JsonPropertyName("scriptId")]
    public string ScriptId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("goals")]
    public List<string> Goals { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("linkedQuestIds")]
    public List<string> LinkedQuestIds { get; set; } = new();
}

public class AdventureModuleCharacterClass
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("statModifiers")]
    public Dictionary<string, int> StatModifiers { get; set; } = new();

    [JsonPropertyName("startingAbilities")]
    public List<string> StartingAbilities { get; set; } = new();

    [JsonPropertyName("levelUpAbilities")]
    public Dictionary<int, List<string>> LevelUpAbilities { get; set; } = new();

    [JsonPropertyName("startingPerks")]
    public List<string> StartingPerks { get; set; } = new();

    [JsonPropertyName("levelUpPerks")]
    public Dictionary<int, List<string>> LevelUpPerks { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class AdventureModuleMove
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("damageDice")]
    public string DamageDice { get; set; } = string.Empty;

    [JsonPropertyName("vigorCost")]
    public int VigorCost { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class AdventureModuleAbility
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("effects")]
    public string Effects { get; set; } = string.Empty;
}
