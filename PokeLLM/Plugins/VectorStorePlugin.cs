using Microsoft.SemanticKernel;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.Data;
using System.ComponentModel;

namespace PokeLLM.Game.Plugins;

public class VectorStorePlugin
{
    private readonly IVectorStoreService _vectorStoreService;

    public VectorStorePlugin(IVectorStoreService vectorStoreService)
    {
        _vectorStoreService = vectorStoreService;
    }

    [KernelFunction("search_all")]
    [Description("Search all adventure data (locations, NPCs, items, lore, quests, events, dialogue) using a semantic query")] 
    public async Task<string> SearchAll(string query, int limit = 10)
    {
        var results = await _vectorStoreService.SearchAllAsync(query, limit);
        return System.Text.Json.JsonSerializer.Serialize(results);
    }

    [KernelFunction("store_location")]
    [Description("Store a new location in the vector database")] 
    public async Task<string> StoreLocation(string name, string description, string type, string[] connectedLocations = null, string[] tags = null, string[] connectedNpcs = null, string[] connectedItems = null)
    {
        var id = await _vectorStoreService.StoreLocationAsync(name, description, type, connectedLocations, tags, connectedNpcs, connectedItems);
        return id.ToString();
    }

    [KernelFunction("store_npc")]
    [Description("Store a new NPC in the vector database")] 
    public async Task<string> StoreNPC(string name, string description, string role, string location, int level = 1, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedQuests = null)
    {
        var id = await _vectorStoreService.StoreNPCAsync(name, description, role, location, level, relatedLocations, relatedNpcs, relatedItems, relatedQuests);
        return id.ToString();
    }

    [KernelFunction("store_item")]
    [Description("Store a new item in the vector database")] 
    public async Task<string> StoreItem(string name, string description, string category, string rarity, int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
    {
        var id = await _vectorStoreService.StoreItemAsync(name, description, category, rarity, value, relatedLocations, relatedNpcs, relatedItems);
        return id.ToString();
    }

    [KernelFunction("store_lore")]
    [Description("Store a new lore entry in the vector database")] 
    public async Task<string> StoreLore(string name, string description, string category, string timePeriod, string importance, string region, string[] relatedEvents = null, string[] relatedNpcs = null, string[] relatedLocations = null)
    {
        var id = await _vectorStoreService.StoreLoreAsync(name, description, category, timePeriod, importance, region, relatedEvents, relatedNpcs, relatedLocations);
        return id.ToString();
    }

    [KernelFunction("store_quest")]
    [Description("Store a new quest in the vector database")] 
    public async Task<string> StoreQuest(string name, string description, string type, string status, string giverNPC, int level = 1, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null)
    {
        var id = await _vectorStoreService.StoreQuestAsync(name, description, type, status, giverNPC, level, relatedLocations, relatedNpcs, relatedItems);
        return id.ToString();
    }

    [KernelFunction("store_event")]
    [Description("Store a new event in the vector database")] 
    public async Task<string> StoreEvent(string name, string description, string type, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedEvents = null)
    {
        var id = await _vectorStoreService.StoreEventAsync(name, description, type, relatedLocations, relatedNpcs, relatedItems, relatedEvents);
        return id.ToString();
    }

    [KernelFunction("store_dialogue")]
    [Description("Store a new dialogue entry in the vector database")] 
    public async Task<string> StoreDialogue(string speaker, string content, string topic, string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedQuests = null)
    {
        var id = await _vectorStoreService.StoreDialogueAsync(speaker, content, topic, relatedNpcs, relatedLocations, relatedQuests);
        return id.ToString();
    }
}
