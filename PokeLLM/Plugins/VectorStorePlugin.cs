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

    [KernelFunction("upsert_location")]
    [Description("Create or update a location reference in the vector store. Use this whenever a new location is introduced or when existing location details are expanded. Include rich atmospheric descriptions, environment types, and connections to other world elements. Use for cities, dungeons, wilderness areas, buildings, etc.")]
    public async Task<string> UpsertLocation(string name, string description, string type, string environment = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedPointsOfInterest = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertLocation called with name: '{name}', type: '{type}'");
        var id = await _vectorStoreService.UpsertLocationAsync(name, description, type, environment, relatedLocations, relatedNpcs, relatedItems, relatedPointsOfInterest);
        return id.ToString();
    }

    [KernelFunction("upsert_npc")]
    [Description("Create or update an NPC reference in the vector store. Use this the FIRST TIME any NPC is mentioned in conversation or narrative, and whenever their details are expanded. Include comprehensive personality, motivations, abilities, relationships, and background. Essential for maintaining character consistency across scenes.")]
    public async Task<string> UpsertNPC(string name, string description, string role, string location, string faction = "", string motivations = "", string abilities = "", string challengeLevel = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedQuests = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertNPC called with name: '{name}', role: '{role}', location: '{location}'");
        var id = await _vectorStoreService.UpsertNPCAsync(name, description, role, location, faction, motivations, abilities, challengeLevel, relatedLocations, relatedNpcs, relatedItems, relatedQuests);
        return id.ToString();
    }

    [KernelFunction("upsert_item")]
    [Description("Create or update an item reference in the vector store. Use whenever new equipment, treasures, consumables, or special items are introduced. Include mechanical effects, usage requirements, and value for consistent item interactions and reward systems.")]
    public async Task<string> UpsertItem(string name, string description, string category, string rarity, string mechanicalEffects = "", string requirements = "", int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertItem called with name: '{name}', category: '{category}', rarity: '{rarity}'");
        var id = await _vectorStoreService.UpsertItemAsync(name, description, category, rarity, mechanicalEffects, requirements, value, relatedLocations, relatedNpcs, relatedItems);
        return id.ToString();
    }

    [KernelFunction("upsert_lore")]
    [Description("Create or update world-building lore in the vector store. Use when introducing history, mythology, cultural information, legends, or background knowledge. Essential for maintaining world consistency and providing rich context for future scenes.")]
    public async Task<string> UpsertLore(string name, string description, string category, string timePeriod, string importance, string region, string[] relatedEvents = null, string[] relatedNpcs = null, string[] relatedLocations = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertLore called with name: '{name}', category: '{category}', region: '{region}'");
        var id = await _vectorStoreService.UpsertLoreAsync(name, description, category, timePeriod, importance, region, relatedEvents, relatedNpcs, relatedLocations);
        return id.ToString();
    }

    [KernelFunction("upsert_storyline")]
    [Description("Create or update storyline reference information in the vector store. Use when introducing new narrative threads, plot arcs, quests, or story developments. Include plot hooks, potential outcomes, and complexity levels for managing interconnected story elements.")]
    public async Task<string> UpsertStoryline(string name, string description, string plothooks = "", string potentialOutcomes = "", int complexityLevel = 3, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStorylines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertStoryline called with name: '{name}', complexityLevel: {complexityLevel}");
        var id = await _vectorStoreService.UpsertStorylinesAsync(name, description, plothooks, potentialOutcomes, complexityLevel, relatedLocations, relatedNpcs, relatedItems, relatedStorylines);
        return id.ToString();
    }

    [KernelFunction("upsert_event_history")]
    [Description("Create or update historical event records in the vector store. Use IMMEDIATELY after significant player actions, story developments, battles, and world changes. Include consequences and player choices for narrative consistency and continuity tracking.")]
    public async Task<string> UpsertEventHistory(string name, string description, string type, string consequences = "", string playerChoices = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertEventHistory called with name: '{name}', type: '{type}'");
        var id = await _vectorStoreService.UpsertEventHistoryAsync(name, description, type, consequences, playerChoices, relatedLocations, relatedNpcs, relatedItems, relatedStoryLines);
        return id.ToString();
    }

    [KernelFunction("upsert_dialogue_history")]
    [Description("Create or update dialogue history in the vector store. Use after meaningful conversations to track character voice, relationships, and information revealed. Include speaker, content, context, and relationships to maintain consistency across future interactions.")]
    public async Task<string> UpsertDialogueHistory(string speaker, string content, string topic, string context = "", string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedStoryLines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertDialogueHistory called with speaker: '{speaker}', topic: '{topic}'");
        var id = await _vectorStoreService.UpsertDialogueHistoryAsync(speaker, content, topic, context, relatedNpcs, relatedLocations, relatedStoryLines);
        return id.ToString();
    }

    [KernelFunction("upsert_point_of_interest")]
    [Description("Create or update interactive challenges and encounters in the vector store. Use when introducing puzzles, skill challenges, hazards, obstacles, or mechanical encounters. Include challenge type, difficulty, required skills, and potential outcomes for consistent mechanical interactions.")]
    public async Task<string> UpsertPointOfInterest(string name, string description, string challengeType, int difficultyClass, string requiredSkills, string potentialOutcomes, string environmentalFactors, string rewards, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertPointOfInterest called with name: '{name}', challengeType: '{challengeType}', difficultyClass: {difficultyClass}");
        var id = await _vectorStoreService.UpsertPointOfInterestAsync(name, description, challengeType, difficultyClass, requiredSkills, potentialOutcomes, environmentalFactors, rewards, relatedLocations, relatedNpcs, relatedItems, relatedStoryLines);
        return id.ToString();
    }

    [KernelFunction("upsert_rules_mechanics")]
    [Description("Create or update game rules and mechanical procedures in the vector store. Use when establishing precedents for Pokemon abilities, move effects, battle mechanics, or rule interpretations. Include usage examples and related rules for consistent game mechanics application.")]
    public async Task<string> UpsertRulesMechanics(string name, string description, string category, string ruleSet, string usage, string examples, string[] relatedRules = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertRulesMechanics called with name: '{name}', category: '{category}'");
        var id = await _vectorStoreService.UpsertRulesMechanicsAsync(name, description, category, ruleSet, usage, examples, relatedRules);
        return id.ToString();
    }
}