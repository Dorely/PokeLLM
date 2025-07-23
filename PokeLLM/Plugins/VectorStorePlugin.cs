using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace PokeLLM.Game.Plugins;

public class VectorStorePlugin
{
    private readonly IVectorStoreService _vectorStoreService;

    public VectorStorePlugin(IVectorStoreService vectorStoreService)
    {
        _vectorStoreService = vectorStoreService;
    }

    [KernelFunction("search_world_knowledge")]
    [Description("Search all adventure reference data using semantic similarity. " +
        "Returns contextually relevant information about locations, NPCs, items, lore, storylines, historical events, past dialogue, points of interest, and game rules/mechanics. " +
        "The results are ordered by score in descending order.")]
    public async Task<string> SearchInformation(string query, int limit = 5)
    {
        Debug.WriteLine($"[VectorStorePlugin] SearchAll called with query: '{query}', limit: {limit}");
        var results = await _vectorStoreService.SearchInformationAsync(query, limit);
        return System.Text.Json.JsonSerializer.Serialize(results);
    }

    [KernelFunction("upsert_information")]
    [Description("Upsert a generic adventure information entry. Stores any adventure-related data in the vector store. Use flexible parameters: name, description, content, type, tags, relatedEntries.")]
    public async Task<string> UpsertInformation(
        [Description("The display name of the entry. Or the name of the Entity being described")] string name,
        [Description("A detailed description of the entry.")] string description,
        [Description("The main content or narrative for the entry. Should contain detailed information such as stats, factions, functions, where it should be found, etc.")] string content,
        [Description("The type/category of the entry (e.g., location, npc, pokemon, item, lore, mechanical rules, dialogue history, event history, etc.).")] string type,
        [Description("Tags or keywords for flexible metadata and search.")] string[] tags = null,
        [Description("IDs of related entries for linking references.")] string[] relatedEntries = null)
    {
        Debug.WriteLine($"[VectorStorePlugin] UpsertInformation called with name: '{name}', type: '{type}'");
        var id = await _vectorStoreService.UpsertInformationAsync(name, description, content, type, tags, relatedEntries);
        return id.ToString();
    }
}