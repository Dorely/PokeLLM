using PokeLLM.Game.Data;

namespace PokeLLM.Game.VectorStore.Interfaces;

public interface IVectorStoreService
{
    Task<Guid> StoreLocationAsync(string name, string description, string type, string[] connectedLocations = null, string[] tags = null, string[] connectedNpcs = null, string[] connectedItems = null);
    Task<Guid> StoreNPCAsync(string name, string description, string role, string location, int level = 1, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedQuests = null);
    Task<Guid> StoreItemAsync(string name, string description, string category, string rarity, int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null);
    Task<Guid> StoreLoreAsync(string name, string description, string category, string timePeriod, string importance, string region, string[] relatedEvents = null, string[] relatedNpcs = null, string[] relatedLocations = null);
    Task<Guid> StoreQuestAsync(string name, string description, string type, string status, string giverNPC, int level = 1, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null);
    Task<Guid> StoreEventAsync(string name, string description, string type, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedEvents = null);
    Task<Guid> StoreDialogueAsync(string speaker, string content, string topic, string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedQuests = null);

    Task<IEnumerable<VectorSearchResult>> SearchLocationsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchNPCsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchItemsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchLoreAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchQuestsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchEventsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchDialogueAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchAllAsync(string query, int limit = 10);
}
