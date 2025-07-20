using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.Diagnostics;

namespace PokeLLM.Game.Plugins;

public class VectorStorePlugin
{
    private readonly IVectorStoreService _vectorStoreService;

    public VectorStorePlugin(IVectorStoreService vectorStoreService)
    {
        _vectorStoreService = vectorStoreService;
    }

    [KernelFunction("search_all")]
    [Description("Search all adventure reference data using semantic similarity. Returns contextually relevant information about locations, NPCs, items, lore, storylines, historical events, past dialogue, points of interest, and game rules/mechanics.")]
    public async Task<string> SearchAll(string query, int limit = 10)
    {
        Debug.WriteLine($"[VectorStorePlugin] SearchAll called with query: '{query}', limit: {limit}");
        var results = await _vectorStoreService.SearchAllAsync(query, limit);
        return System.Text.Json.JsonSerializer.Serialize(results);
    }

    [KernelFunction("store_location")]
    [Description("Store a new location reference - places in the world with atmospheric descriptions, environment types, and connections to other world elements. Use for cities, dungeons, wilderness areas, buildings, etc.")]
    public async Task<string> StoreLocation(string name, string description, string type, string environment = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedPointsOfInterest = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreLocation called with name: '{name}', type: '{type}'");
        var id = await _vectorStoreService.StoreLocationAsync(name, description, type, environment, relatedLocations, relatedNpcs, relatedItems, relatedPointsOfInterest);
        return id.ToString();
    }

    [KernelFunction("store_npc")]
    [Description("Store a new NPC reference - non-player characters with personality, motivations, abilities, and relationships. Include their role, faction affiliations, and what they're capable of for consistent character interactions.")]
    public async Task<string> StoreNPC(string name, string description, string role, string location, string faction = "", string motivations = "", string abilities = "", string challengeLevel = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedQuests = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreNPC called with name: '{name}', role: '{role}', location: '{location}'");
        var id = await _vectorStoreService.StoreNPCAsync(name, description, role, location, faction, motivations, abilities, challengeLevel, relatedLocations, relatedNpcs, relatedItems, relatedQuests);
        return id.ToString();
    }

    [KernelFunction("store_item")]
    [Description("Store a new item reference - equipment, treasures, consumables, and magical items. Include mechanical effects, usage requirements, and value for consistent item interactions and reward systems.")]
    public async Task<string> StoreItem(string name, string description, string category, string rarity, string mechanicalEffects = "", string requirements = "", int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreItem called with name: '{name}', category: '{category}', rarity: '{rarity}'");
        var id = await _vectorStoreService.StoreItemAsync(name, description, category, rarity, mechanicalEffects, requirements, value, relatedLocations, relatedNpcs, relatedItems);
        return id.ToString();
    }

    [KernelFunction("store_lore")]
    [Description("Store world-building lore - history, mythology, cultural information, and background knowledge that enriches the setting. Use for legends, historical events, cultural practices, and world context.")]
    public async Task<string> StoreLore(string name, string description, string category, string timePeriod, string importance, string region, string[] relatedEvents = null, string[] relatedNpcs = null, string[] relatedLocations = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreLore called with name: '{name}', category: '{category}', region: '{region}'");
        var id = await _vectorStoreService.StoreLoreAsync(name, description, category, timePeriod, importance, region, relatedEvents, relatedNpcs, relatedLocations);
        return id.ToString();
    }

    [KernelFunction("store_storyline")]
    [Description("Store storyline reference information - narrative threads, plot arcs, and story developments. Include plot hooks, potential outcomes, and complexity levels for managing interconnected story elements.")]
    public async Task<string> StoreStoryline(string name, string description, string plothooks = "", string potentialOutcomes = "", int complexityLevel = 3, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStorylines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreStoryline called with name: '{name}', complexityLevel: {complexityLevel}");
        var id = await _vectorStoreService.StoreStorylinesAsync(name, description, plothooks, potentialOutcomes, complexityLevel, relatedLocations, relatedNpcs, relatedItems, relatedStorylines);
        return id.ToString();
    }

    [KernelFunction("store_event_history")]
    [Description("Store historical events that occurred during the adventure - player actions, story developments, and world changes. Include consequences and player choices for narrative consistency and continuity.")]
    public async Task<string> StoreEventHistory(string name, string description, string type, string consequences = "", string playerChoices = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreEvent called with name: '{name}', type: '{type}'");
        var id = await _vectorStoreService.StoreEventHistoryAsync(name, description, type, consequences, playerChoices, relatedLocations, relatedNpcs, relatedItems, relatedStoryLines);
        return id.ToString();
    }

    [KernelFunction("store_dialogue_history")]
    [Description("Store dialogue history - conversations between characters for reference and consistency. Include speaker, content, context, and timestamp to maintain character voice and track information revealed.")]
    public async Task<string> StoreDialogueHistory(string speaker, string content, string topic, string context = "", string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedStoryLines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreDialogue called with speaker: '{speaker}', topic: '{topic}'");
        var id = await _vectorStoreService.StoreDialogueHistoryAsync(speaker, content, topic, context, relatedNpcs, relatedLocations, relatedStoryLines);
        return id.ToString();
    }

    [KernelFunction("store_point_of_interest")]
    [Description("Store interactive challenges and encounters - puzzles, skill challenges, hazards, and obstacles. Include challenge type, difficulty, required skills, and potential outcomes for consistent mechanical interactions.")]
    public async Task<string> StorePointOfInterest(string name, string description, string challengeType, int difficultyClass, string requiredSkills, string potentialOutcomes, string environmentalFactors, string rewards, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StorePointOfInterest called with name: '{name}', challengeType: '{challengeType}', difficultyClass: {difficultyClass}");
        var id = await _vectorStoreService.StorePointOfInterestAsync(name, description, challengeType, difficultyClass, requiredSkills, potentialOutcomes, environmentalFactors, rewards, relatedLocations, relatedNpcs, relatedItems, relatedStoryLines);
        return id.ToString();
    }

    [KernelFunction("store_rules_mechanics")]
    [Description("Store game rules and mechanical procedures - spell descriptions, ability explanations, rule interpretations, and mechanical precedents. Include usage examples and related rules for consistent game mechanics application.")]
    public async Task<string> StoreRulesMechanics(string name, string description, string category, string ruleSet, string usage, string examples, string[] relatedRules = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] StoreRulesMechanics called with name: '{name}', category: '{category}'");
        var id = await _vectorStoreService.StoreRulesMechanicsAsync(name, description, category, ruleSet, usage, examples, relatedRules);
        return id.ToString();
    }
}